using Microsoft.EntityFrameworkCore;
using ProFak.DB;
using ProFak.IO.KSEF2;

namespace ProFak.UI;

class KSeFZakupInboxSpis : Spis<KSeFZakupInbox>
{
	private readonly System.Windows.Forms.Timer timerSynchronizacji;
	private bool pierwszeLadowanie = true;
	private bool trwaSynchronizacjaWTle;
	private KSeFZakupInboxStan? stan;

	public override string Podsumowanie
	{
		get
		{
			var wszystkie = Rekordy.ToList();
			var nowe = wszystkie.Count(e => e.CzyNowa);
			var zweryfikowane = wszystkie.Count(e => e.Status == KSeFZakupInboxStatus.Zweryfikowana);
			var dodane = wszystkie.Count(e => e.Status == KSeFZakupInboxStatus.DodanaJakoZakup);
			var rozliczone = wszystkie.Count(e => e.Status == KSeFZakupInboxStatus.Rozliczona);
			var tekst = wszystkie.Count == 0 ? "Inbox KSeF nie zawiera danych" : $"Liczba dokumentów: <{wszystkie.Count}>";
			if (nowe > 0) tekst += $"\nNowe: <{nowe}>";
			if (zweryfikowane > 0) tekst += $"\nZweryfikowane: <{zweryfikowane}>";
			if (dodane > 0) tekst += $"\nDodane jako zakup: <{dodane}>";
			if (rozliczone > 0) tekst += $"\nRozliczone: <{rozliczone}>";
			if (stan?.DataOstatniejSynchronizacji.HasValue == true) tekst += $"\nOstatnia synchronizacja: <{stan.DataOstatniejSynchronizacji.Value.ToString(Wyglad.FormatCzasu)}>";
			if (stan?.CzyAutoSynchronizacja == true && stan.DataNastepnejSynchronizacji.HasValue) tekst += $"\nNastępna synchronizacja: <{stan.DataNastepnejSynchronizacji.Value.ToString(Wyglad.FormatCzasu)}>";
			return tekst;
		}
	}

	public KSeFZakupInboxSpis()
	{
		DodajKolumneBool(nameof(KSeFZakupInbox.CzyNowa), "Nowe", szerokosc: 55);
		DodajKolumne(nameof(KSeFZakupInbox.StatusFmt), "Status", szerokosc: 130);
		DodajKolumneData(nameof(KSeFZakupInbox.DataKSeF), "Data KSeF", format: Wyglad.FormatCzasu, szerokosc: 145);
		DodajKolumneData(nameof(KSeFZakupInbox.DataPobrania), "Pobrano", format: Wyglad.FormatCzasu, szerokosc: 145);
		DodajKolumneData(nameof(KSeFZakupInbox.DataWeryfikacji), "Zweryfikowano", format: Wyglad.FormatCzasu, szerokosc: 145);
		DodajKolumneData(nameof(KSeFZakupInbox.DataDodaniaJakoZakup), "Dodano do zakupu", format: Wyglad.FormatCzasu, szerokosc: 145);
		DodajKolumneData(nameof(KSeFZakupInbox.DataRozliczenia), "Rozliczono", format: Wyglad.FormatCzasu, szerokosc: 145);
		DodajKolumne(nameof(KSeFZakupInbox.Numer), "Numer", szerokosc: 180);
		DodajKolumne(nameof(KSeFZakupInbox.TypDokumentu), "Rodzaj", szerokosc: 130);
		DodajKolumne(nameof(KSeFZakupInbox.NazwaSprzedawcy), "Sprzedawca", rozciagnij: true);
		DodajKolumne(nameof(KSeFZakupInbox.NIPSprzedawcy), "NIP sprzedawcy", szerokosc: 120);
		DodajKolumneKwota(nameof(KSeFZakupInbox.RazemNetto), "Netto");
		DodajKolumneKwota(nameof(KSeFZakupInbox.RazemVat), "VAT");
		DodajKolumneKwota(nameof(KSeFZakupInbox.RazemBrutto), "Brutto");
		DodajKolumne(nameof(KSeFZakupInbox.Waluta), "Waluta", szerokosc: 70);
		DodajKolumne(nameof(KSeFZakupInbox.NumerZakupu), "Dokument zakupu", szerokosc: 180);
		DodajKolumne(nameof(KSeFZakupInbox.NumerKSeF), "Id KSeF", szerokosc: 230);
		Komunikat = "Otwórz inbox lub użyj F5, aby zsynchronizować dokumenty z KSeF";

		timerSynchronizacji = new System.Windows.Forms.Timer();
		timerSynchronizacji.Interval = 60_000;
		timerSynchronizacji.Tick += async (_, _) => await SynchronizujWTleJesliPotrzebaAsync();
	}

	protected override void OnCreateControl()
	{
		base.OnCreateControl();
		SkonfigurujTimer();
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			timerSynchronizacji.Stop();
			timerSynchronizacji.Dispose();
		}
		base.Dispose(disposing);
	}

	protected override void Przeladuj()
	{
		OdswiezLokalnie();

		if (pierwszeLadowanie)
		{
			pierwszeLadowanie = false;
			if (KSeFZakupInboxService.CzyNalezySynchronizowac(Kontekst.Baza)) _ = SynchronizujWTleJesliPotrzebaAsync(wymus: true);
			return;
		}

		Komunikat = "Synchronizacja danych z KSeF";
		OknoPostepu.Uruchom(async cancellationToken =>
		{
			await KSeFZakupInboxService.SynchronizujAsync(Kontekst.Baza, cancellationToken);
		});
		OdswiezLokalnie();
		Komunikat = null;
	}

	public void OdswiezLokalnie()
	{
		KSeFZakupInboxService.OdswiezPowiazaniaIStatusy(Kontekst.Baza);
		ZaladujZBazy();
		SkonfigurujTimer();
	}

	protected override void UstawStylWiersza(KSeFZakupInbox rekord, string kolumna, DataGridViewCellStyle styl)
	{
		base.UstawStylWiersza(rekord, kolumna, styl);
		if (rekord.CzyNowa)
		{
			styl.BackColor = Color.FromArgb(224, 239, 255);
		}

		if (rekord.Status == KSeFZakupInboxStatus.Pominieta)
		{
			styl.ForeColor = Color.DimGray;
		}
		else if (rekord.Status == KSeFZakupInboxStatus.Rozliczona)
		{
			styl.ForeColor = Color.FromArgb(20, 130, 40);
		}
		else if (rekord.Status == KSeFZakupInboxStatus.DodanaJakoZakup)
		{
			styl.ForeColor = Color.FromArgb(25, 95, 180);
		}
	}

	private void ZaladujZBazy()
	{
		stan = KSeFZakupInboxService.PobierzLubUtworzStan(Kontekst.Baza);
		Rekordy = Kontekst.Baza.KSeFZakupyInbox
			.Include(e => e.Faktura)
			.OrderByDescending(e => e.DataKSeF)
			.ThenByDescending(e => e.Id)
			.ToList();
	}

	private void SkonfigurujTimer()
	{
		if (Kontekst == null || IsDisposed) return;
		stan = KSeFZakupInboxService.PobierzLubUtworzStan(Kontekst.Baza);
		timerSynchronizacji.Enabled = stan.CzyAutoSynchronizacja;
	}

	private async Task SynchronizujWTleJesliPotrzebaAsync(bool wymus = false)
	{
		if (IsDisposed || !IsHandleCreated) return;
		if (trwaSynchronizacjaWTle) return;

		try
		{
			trwaSynchronizacjaWTle = true;
			await ThreadSwitcher.ResumeBackgroundAsync();
			using var kontekst = new Kontekst();
			if (!wymus && !KSeFZakupInboxService.CzyNalezySynchronizowac(kontekst.Baza)) return;
			if (kontekst.Baza.CzyZablokowana()) return;
			await KSeFZakupInboxService.SynchronizujAsync(kontekst.Baza, CancellationToken.None);
			KSeFZakupInboxService.OdswiezPowiazaniaIStatusy(kontekst.Baza);
		}
		catch
		{
		}
		finally
		{
			await ThreadSwitcher.ResumeForegroundAsync(this);
			if (!IsDisposed)
			{
				ZaladujZBazy();
				Komunikat = null;
				Invalidate();
				SkonfigurujTimer();
			}
			trwaSynchronizacjaWTle = false;
		}
	}
}
