using Microsoft.EntityFrameworkCore;
using ProFak.DB;

namespace ProFak.IO.KSEF2;

public class KSeFZakupInboxSynchronizacjaWynik
{
	public int LiczbaNowych { get; set; }
	public int LiczbaZaktualizowanych { get; set; }
}

static class KSeFZakupInboxService
{
	private static readonly RodzajFaktury[] RodzajeZakupowe = [RodzajFaktury.Zakup, RodzajFaktury.KorektaZakupu, RodzajFaktury.DowódWewnętrzny];

	public static KSeFZakupInboxStan PobierzLubUtworzStan(Baza baza)
	{
		var stan = baza.KSeFZakupyInboxStan.FirstOrDefault();
		if (stan != null) return stan;

		stan = new KSeFZakupInboxStan
		{
			DataNastepnejSynchronizacji = DateTime.Now
		};
		baza.Zapisz(stan);
		return stan;
	}

	public static bool CzyNalezySynchronizowac(Baza baza)
	{
		var stan = PobierzLubUtworzStan(baza);
		if (!stan.CzyAutoSynchronizacja) return false;
		if (!stan.DataOstatniejSynchronizacji.HasValue) return true;
		return !stan.DataNastepnejSynchronizacji.HasValue || stan.DataNastepnejSynchronizacji.Value <= DateTime.Now;
	}

	public static void OdswiezPowiazaniaIStatusy(Baza baza)
	{
		ZasiejZIstniejacychFaktur(baza);

		var inbox = baza.KSeFZakupyInbox
			.Include(e => e.Faktura)
			.ThenInclude(faktura => faktura!.Wplaty)
			.Where(e => !String.IsNullOrEmpty(e.NumerKSeF))
			.ToList();
		if (inbox.Count == 0)
		{
			AktualizujStan(baza);
			return;
		}

		var zakupy = baza.Faktury
			.Where(e => RodzajeZakupowe.Contains(e.Rodzaj) && !String.IsNullOrEmpty(e.NumerKSeF) && e.Rodzaj != RodzajFaktury.Usunięta)
			.Include(e => e.Wplaty)
			.ToList()
			.GroupBy(e => e.NumerKSeF)
			.ToDictionary(e => e.Key, e => e.OrderByDescending(f => f.Id).First());

		var zmienione = new List<KSeFZakupInbox>();
		foreach (var rekord in inbox)
		{
			if (!zakupy.TryGetValue(rekord.NumerKSeF, out var faktura))
			{
				if (rekord.Status == KSeFZakupInboxStatus.Rozliczona || rekord.Status == KSeFZakupInboxStatus.DodanaJakoZakup)
				{
					rekord.Status = rekord.DataWeryfikacji.HasValue ? KSeFZakupInboxStatus.Zweryfikowana : KSeFZakupInboxStatus.Pobrana;
					rekord.FakturaId = null;
					rekord.DataDodaniaJakoZakup = null;
					rekord.DataRozliczenia = null;
					zmienione.Add(rekord);
				}
				continue;
			}

			var czyZmiana = false;
			if (rekord.FakturaId != faktura.Id)
			{
				rekord.FakturaId = faktura.Id;
				czyZmiana = true;
			}
			if (!rekord.DataDodaniaJakoZakup.HasValue)
			{
				rekord.DataDodaniaJakoZakup = faktura.DataWprowadzenia;
				czyZmiana = true;
			}

			var docelowyStatus = faktura.CzyZaplacona ? KSeFZakupInboxStatus.Rozliczona : KSeFZakupInboxStatus.DodanaJakoZakup;
			if (rekord.Status != docelowyStatus)
			{
				rekord.Status = docelowyStatus;
				czyZmiana = true;
			}

			if (docelowyStatus == KSeFZakupInboxStatus.Rozliczona)
			{
				var dataRozliczenia = faktura.DataWplywu;
				if (rekord.DataRozliczenia != dataRozliczenia)
				{
					rekord.DataRozliczenia = dataRozliczenia;
					czyZmiana = true;
				}
			}
			else if (rekord.DataRozliczenia != null)
			{
				rekord.DataRozliczenia = null;
				czyZmiana = true;
			}

			if (czyZmiana)
			{
				rekord.CzyNowa = false;
				zmienione.Add(rekord);
			}
		}

		if (zmienione.Count > 0) baza.Zapisz(zmienione);
		AktualizujStan(baza);
	}

	public static async Task<KSeFZakupInboxSynchronizacjaWynik> SynchronizujAsync(Baza baza, CancellationToken cancellationToken, bool przyrostowo = true, DateTime? odDaty = null)
	{
		OdswiezPowiazaniaIStatusy(baza);

		var stan = PobierzLubUtworzStan(baza);
		var podmiot = baza.Kontrahenci.First(kontrahent => kontrahent.CzyPodmiot);
		if (String.IsNullOrEmpty(podmiot.TokenKSeF))
			throw new ApplicationException("Brak tokena dostępowego do KSeF w danych firmy.\nNadaj dostęp do KSeF w oknie \"Kontrahenci\" -> \"Moja firma\" -> \"Dane urzędowe\" -> \"Token KSeF\".");

		var wynik = new KSeFZakupInboxSynchronizacjaWynik();
		var teraz = DateTime.Now;
		var dataOd = UstalDatePoczatkowa(baza, stan, przyrostowo, odDaty);
		var dataDo = teraz;

		var istniejace = baza.KSeFZakupyInbox.ToDictionary(e => e.NumerKSeF);

		using var api = new API(podmiot.SrodowiskoKSeF);
		await api.UwierzytelnijAsync(podmiot.NIP, podmiot.TokenKSeF, cancellationToken);

		while (dataOd < dataDo)
		{
			cancellationToken.ThrowIfCancellationRequested();
			var koniecFragmentu = dataOd.AddMonths(3);
			if (koniecFragmentu > dataDo) koniecFragmentu = dataDo;
			var fragment = await api.PobierzFakturyZbiorczoAsync(przyrostowo, sprzedaz: false, dataOd, koniecFragmentu, cancellationToken);
			dataOd = koniecFragmentu;

			var nowe = new List<KSeFZakupInbox>();
			var zmienione = new List<KSeFZakupInbox>();

			foreach (var (naglowek, xml) in fragment)
			{
				var dane = api.WczytajNaglowek(naglowek, czySprzedaz: false);
				var urlKsef = String.IsNullOrEmpty(xml) ? "" : api.ZbudujUrl(xml, dane.NIPSprzedawcy, dane.DataWystawienia);

				if (!istniejace.TryGetValue(dane.NumerKSeF, out var rekord))
				{
					rekord = new KSeFZakupInbox
					{
						NumerKSeF = dane.NumerKSeF,
						DataPobrania = teraz,
						CzyNowa = true,
						Status = KSeFZakupInboxStatus.Pobrana
					};
					MapujDane(rekord, dane, xml, urlKsef);
					nowe.Add(rekord);
					istniejace[rekord.NumerKSeF] = rekord;
					wynik.LiczbaNowych++;
					continue;
				}

				if (MapujDane(rekord, dane, xml, urlKsef))
				{
					zmienione.Add(rekord);
					wynik.LiczbaZaktualizowanych++;
				}
			}

			if (nowe.Count > 0) baza.Zapisz(nowe);
			if (zmienione.Count > 0) baza.Zapisz(zmienione);
		}

		OdswiezPowiazaniaIStatusy(baza);
		stan.DataOstatniejSynchronizacji = teraz;
		stan.DataNastepnejSynchronizacji = teraz.AddMinutes(Math.Max(stan.InterwalSynchronizacjiMinuty, 5));
		AktualizujStan(baza, stan);

		return wynik;
	}

	public static async Task ZapewnijXmlAsync(Baza baza, KSeFZakupInbox rekord, CancellationToken cancellationToken)
	{
		if (!String.IsNullOrEmpty(rekord.XMLKSeF)) return;

		var podmiot = baza.Kontrahenci.First(kontrahent => kontrahent.CzyPodmiot);
		using var api = new API(podmiot.SrodowiskoKSeF);
		await api.UwierzytelnijAsync(podmiot.NIP, podmiot.TokenKSeF, cancellationToken);
		rekord.XMLKSeF = await api.PobierzFaktureAsync(rekord.NumerKSeF, cancellationToken);
		rekord.URLKSeF = api.ZbudujUrl(rekord.XMLKSeF, rekord.NIPSprzedawcy, rekord.DataWystawienia);
		baza.Zapisz(rekord);
	}

	private static DateTime UstalDatePoczatkowa(Baza baza, KSeFZakupInboxStan stan, bool przyrostowo, DateTime? odDaty)
	{
		if (odDaty.HasValue) return odDaty.Value;
		if (!przyrostowo) return stan.DataPoczatkowaSynchronizacji;

		var ostatniInbox = baza.KSeFZakupyInbox
			.Where(e => e.DataKSeF.HasValue)
			.OrderByDescending(e => e.DataKSeF)
			.Select(e => e.DataKSeF)
			.FirstOrDefault();
		if (ostatniInbox.HasValue) return ostatniInbox.Value;

		var ostatniZakup = baza.Faktury
			.Where(e => RodzajeZakupowe.Contains(e.Rodzaj) && !String.IsNullOrEmpty(e.NumerKSeF))
			.OrderByDescending(e => e.DataKSeF)
			.Select(e => e.DataKSeF)
			.FirstOrDefault();
		if (ostatniZakup.HasValue) return ostatniZakup.Value;

		return stan.DataPoczatkowaSynchronizacji;
	}

	private static bool MapujDane(KSeFZakupInbox rekord, Faktura dane, string xml, string urlKsef)
	{
		var zmiana = false;

		void Ustaw<T>(T obecnaWartosc, T nowaWartosc, Action<T> setter)
		{
			if (EqualityComparer<T>.Default.Equals(obecnaWartosc, nowaWartosc)) return;
			setter(nowaWartosc);
			zmiana = true;
		}

		Ustaw(rekord.Numer, dane.Numer, wartosc => rekord.Numer = wartosc);
		Ustaw(rekord.DataWystawienia, dane.DataWystawienia, wartosc => rekord.DataWystawienia = wartosc);
		Ustaw(rekord.DataSprzedazy, dane.DataSprzedazy, wartosc => rekord.DataSprzedazy = wartosc);
		Ustaw(rekord.DataKSeF, dane.DataKSeF, wartosc => rekord.DataKSeF = wartosc);
		Ustaw(rekord.NazwaSprzedawcy, dane.NazwaSprzedawcy, wartosc => rekord.NazwaSprzedawcy = wartosc);
		Ustaw(rekord.NIPSprzedawcy, dane.NIPSprzedawcy, wartosc => rekord.NIPSprzedawcy = wartosc);
		Ustaw(rekord.NazwaNabywcy, dane.NazwaNabywcy, wartosc => rekord.NazwaNabywcy = wartosc);
		Ustaw(rekord.NIPNabywcy, dane.NIPNabywcy, wartosc => rekord.NIPNabywcy = wartosc);
		Ustaw(rekord.RazemNetto, dane.RazemNetto, wartosc => rekord.RazemNetto = wartosc);
		Ustaw(rekord.RazemVat, dane.RazemVat, wartosc => rekord.RazemVat = wartosc);
		Ustaw(rekord.RazemBrutto, dane.RazemBrutto, wartosc => rekord.RazemBrutto = wartosc);
		Ustaw(rekord.Waluta, dane.WalutaFmt, wartosc => rekord.Waluta = wartosc);
		Ustaw(rekord.TypDokumentu, dane.RodzajFmt, wartosc => rekord.TypDokumentu = wartosc);
		if (!String.IsNullOrEmpty(xml)) Ustaw(rekord.XMLKSeF, xml, wartosc => rekord.XMLKSeF = wartosc);
		if (!String.IsNullOrEmpty(urlKsef)) Ustaw(rekord.URLKSeF, urlKsef, wartosc => rekord.URLKSeF = wartosc);

		return zmiana;
	}

	private static void ZasiejZIstniejacychFaktur(Baza baza)
	{
		var zakupy = baza.Faktury
			.Include(e => e.Wplaty)
			.Where(e => RodzajeZakupowe.Contains(e.Rodzaj) && !String.IsNullOrEmpty(e.NumerKSeF) && e.Rodzaj != RodzajFaktury.Usunięta)
			.ToList();
		if (zakupy.Count == 0) return;

		var inbox = baza.KSeFZakupyInbox.ToDictionary(e => e.NumerKSeF);
		var nowe = new List<KSeFZakupInbox>();
		var zmienione = new List<KSeFZakupInbox>();

		foreach (var faktura in zakupy)
		{
			if (!inbox.TryGetValue(faktura.NumerKSeF, out var rekord))
			{
				rekord = new KSeFZakupInbox
				{
					NumerKSeF = faktura.NumerKSeF,
					Numer = faktura.Numer,
					DataWystawienia = faktura.DataWystawienia,
					DataSprzedazy = faktura.DataSprzedazy,
					DataKSeF = faktura.DataKSeF,
					DataPobrania = faktura.DataKSeF ?? faktura.DataWprowadzenia,
					DataDodaniaJakoZakup = faktura.DataWprowadzenia,
					DataRozliczenia = faktura.CzyZaplacona ? faktura.DataWplywu : null,
					NazwaSprzedawcy = faktura.NazwaSprzedawcy,
					NIPSprzedawcy = faktura.NIPSprzedawcy,
					NazwaNabywcy = faktura.NazwaNabywcy,
					NIPNabywcy = faktura.NIPNabywcy,
					RazemNetto = faktura.RazemNetto,
					RazemVat = faktura.RazemVat,
					RazemBrutto = faktura.RazemBrutto,
					Waluta = faktura.WalutaFmt,
					TypDokumentu = faktura.RodzajFmt,
					XMLKSeF = faktura.XMLKSeF,
					URLKSeF = faktura.URLKSeF,
					CzyNowa = false,
					Status = faktura.CzyZaplacona ? KSeFZakupInboxStatus.Rozliczona : KSeFZakupInboxStatus.DodanaJakoZakup,
					FakturaId = faktura.Id
				};
				nowe.Add(rekord);
				inbox[rekord.NumerKSeF] = rekord;
				continue;
			}

			var czyZmiana = false;
			if (rekord.FakturaId != faktura.Id)
			{
				rekord.FakturaId = faktura.Id;
				czyZmiana = true;
			}
			if (String.IsNullOrEmpty(rekord.XMLKSeF) && !String.IsNullOrEmpty(faktura.XMLKSeF))
			{
				rekord.XMLKSeF = faktura.XMLKSeF;
				czyZmiana = true;
			}
			if (String.IsNullOrEmpty(rekord.URLKSeF) && !String.IsNullOrEmpty(faktura.URLKSeF))
			{
				rekord.URLKSeF = faktura.URLKSeF;
				czyZmiana = true;
			}
			if (czyZmiana) zmienione.Add(rekord);
		}

		if (nowe.Count > 0) baza.Zapisz(nowe);
		if (zmienione.Count > 0) baza.Zapisz(zmienione);
	}

	private static void AktualizujStan(Baza baza, KSeFZakupInboxStan? stan = null)
	{
		stan ??= PobierzLubUtworzStan(baza);
		stan.LiczbaNowychDokumentow = baza.KSeFZakupyInbox.Count(e => e.CzyNowa);
		if (stan.CzyAutoSynchronizacja && stan.DataOstatniejSynchronizacji.HasValue && (!stan.DataNastepnejSynchronizacji.HasValue || stan.DataNastepnejSynchronizacji <= stan.DataOstatniejSynchronizacji))
		{
			stan.DataNastepnejSynchronizacji = stan.DataOstatniejSynchronizacji.Value.AddMinutes(Math.Max(stan.InterwalSynchronizacjiMinuty, 5));
		}
		baza.Zapisz(stan);
	}
}
