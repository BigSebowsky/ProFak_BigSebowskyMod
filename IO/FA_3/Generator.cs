using Microsoft.EntityFrameworkCore;
using ProFak.DB;
using ProFak.IO.FA_3.DefinicjeTypy;
using System.Collections;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Serialization;
using DBFaktura = ProFak.DB.Faktura;
using KSEFFaktura = ProFak.IO.FA_3.Faktura;

namespace ProFak.IO.FA_3;

public class Generator
{
	public static string ZbudujXML(Baza baza, Ref<DBFaktura> dbFakturaRef)
	{
		var dbFaktura = baza.Faktury
			.Include(e => e.Wplaty)
			.Include(e => e.Pozycje).ThenInclude(e => e.JednostkaMiary)
			.Include(e => e.Pozycje).ThenInclude(e => e.StawkaVat)
			.Include(e => e.Sprzedawca!).ThenInclude(e => e.Kraj)
			.Include(e => e.Sprzedawca!).ThenInclude(e => e.RachunkiBankowe).ThenInclude(e => e.Kraj)
			.Include(e => e.Sprzedawca!).ThenInclude(e => e.RachunkiBankowe).ThenInclude(e => e.Waluta)
			.Include(e => e.Nabywca!).ThenInclude(e => e.Kraj)
			.Include(e => e.Waluta)
			.Include(e => e.FakturaKorygowana)
			.Include(e => e.FakturaPierwotna)
			.Include(e => e.DodatkowePodmioty)
			.Where(e => e.Id == dbFakturaRef.Id)
			.FirstOrDefault() ?? throw new ApplicationException($"Nie znaleziono faktury {dbFakturaRef}.");
		var bledyBiznesowe = ZweryfikujBiznesowo(dbFaktura);
		var konfiguracja = baza.Konfiguracja.FirstOrDefault() ?? Konfiguracja.Domyslna;
		var ksefFaktura = Zbuduj(dbFaktura, konfiguracja.WysylajPlatnoscDoKSeF);
		var xml = SerializujDoXml(ksefFaktura);
		ZweryfikujKSeF(ksefFaktura, xml, dbFaktura.Numer, bledyBiznesowe);
		return xml;
	}

	public static DBFaktura ZbudujDB(Baza baza, string xml)
	{
		if (xml.Contains("kodSystemowy=\"FA (2)\"") && xml.Contains("<WariantFormularza>2</WariantFormularza>")) return FA_2.Generator.ZbudujDB(baza, xml);
		var xo = new XmlAttributeOverrides();
		var xs = new XmlSerializer(typeof(KSEFFaktura), xo);
		using var xr = XmlReader.Create(new StringReader(xml), new XmlReaderSettings() { });
		var nss = new XmlSerializerNamespaces();
		var ksefFaktura = (KSEFFaktura?)xs.Deserialize(xr);
		if (ksefFaktura == null) throw new ApplicationException("Nieznany format faktury.");
		var dbFaktura = Zbuduj(ksefFaktura);
		dbFaktura.XMLKSeF = xml;
		PoprawPowiazaniaPoImporcie(baza, dbFaktura);
		return dbFaktura;
	}

	private static T ZbudujAdres<T>(string adres, TKodKraju kodKraju) where T : TAdres, new()
	{
		var linie = adres.JakoDwieLinie();
		var ksefAdres = new T();
		ksefAdres.KodKraju = kodKraju;
		if (!String.IsNullOrWhiteSpace(linie.linia1)) ksefAdres.AdresL1 = linie.linia1;
		if (!String.IsNullOrWhiteSpace(linie.linia2)) ksefAdres.AdresL2 = linie.linia2;
		return ksefAdres;
	}

	private static KSEFFaktura Zbuduj(DBFaktura dbFaktura, bool wysylajPlatnosc)
	{
		// Pobrane przez wywołanie w ZbudujXML
		ArgumentNullException.ThrowIfNull(dbFaktura.Sprzedawca);
		ArgumentNullException.ThrowIfNull(dbFaktura.Nabywca);
		ArgumentNullException.ThrowIfNull(dbFaktura.Waluta);
		var ksefFaktura = new KSEFFaktura();
		ksefFaktura.Naglowek = new TNaglowek();
		ksefFaktura.Naglowek.KodFormularza = new TNaglowekKodFormularza();
		ksefFaktura.Naglowek.WariantFormularza = TNaglowekWariantFormularza.Item3;
		ksefFaktura.Naglowek.DataWytworzeniaFa = DateTime.Now;
		ksefFaktura.Naglowek.SystemInfo = "PROFAKURA";
		ksefFaktura.Podmiot1 = new FakturaPodmiot1();
		ksefFaktura.Podmiot1.DaneIdentyfikacyjne = new TPodmiot1();
		var kodKrajuSprzedawcy = PobierzKodKraju(dbFaktura.Sprzedawca, dbFaktura.NIPSprzedawcy, TKodKraju.PL);
		var kodKrajuNabywcy = PobierzKodKraju(dbFaktura.Nabywca, dbFaktura.NIPNabywcy, TKodKraju.PL);
		ksefFaktura.Podmiot1.DaneIdentyfikacyjne.NIP = dbFaktura.NIPSprzedawcy.Replace("-", "");
		ksefFaktura.Podmiot1.DaneIdentyfikacyjne.Nazwa = dbFaktura.NazwaSprzedawcy;
		ksefFaktura.Podmiot1.Adres = ZbudujAdres<TAdres>(dbFaktura.DaneSprzedawcy, kodKrajuSprzedawcy);
		if (!String.IsNullOrEmpty(dbFaktura.Sprzedawca.AdresKorespondencyjny) && dbFaktura.Sprzedawca.AdresKorespondencyjny != dbFaktura.DaneSprzedawcy) ksefFaktura.Podmiot1.AdresKoresp = ZbudujAdres<FakturaPodmiot1AdresKoresp>(dbFaktura.Sprzedawca.AdresKorespondencyjny, kodKrajuSprzedawcy);
		ksefFaktura.Podmiot1.DaneKontaktowe.Add(new FakturaPodmiot1DaneKontaktowe { Email = String.IsNullOrWhiteSpace(dbFaktura.Sprzedawca.EMail) ? null : dbFaktura.Sprzedawca.EMail, Telefon = String.IsNullOrWhiteSpace(dbFaktura.Sprzedawca.Telefon) ? null : dbFaktura.Sprzedawca.Telefon });
		ksefFaktura.Podmiot2 = new FakturaPodmiot2();
		ksefFaktura.Podmiot2.DaneIdentyfikacyjne = new TPodmiot2();
		ksefFaktura.Podmiot2.Adres = ZbudujAdres<TAdres>(dbFaktura.DaneNabywcy, kodKrajuNabywcy);
		var nipNabywcy = (dbFaktura.NIPNabywcy ?? "").Replace("-", "").Trim().ToUpper();
		if (String.IsNullOrEmpty(nipNabywcy))
		{
			ksefFaktura.Podmiot2.DaneIdentyfikacyjne.BrakID = TWybor1.Item1;
		}
		else if (Regex.IsMatch(nipNabywcy, @"^\d{10}$"))
		{
			ksefFaktura.Podmiot2.DaneIdentyfikacyjne.NIP = nipNabywcy;
		}
		else if (Regex.IsMatch(nipNabywcy, @"^(PL)?\d{10}$"))
		{
			ksefFaktura.Podmiot2.DaneIdentyfikacyjne.NIP = nipNabywcy.Substring(2);
		}
		else if (Regex.IsMatch(nipNabywcy, @"^\w\w") && Enum.TryParse<TKodyKrajowUE>(nipNabywcy[0..2], out var kodUE))
		{
			ksefFaktura.Podmiot2.DaneIdentyfikacyjne.KodUE = kodUE;
			ksefFaktura.Podmiot2.DaneIdentyfikacyjne.NrVatUE = nipNabywcy.Substring(2);
			if (dbFaktura.Nabywca?.Kraj == null && Enum.TryParse<TKodKraju>(nipNabywcy[0..2], out var kodKraju)) ksefFaktura.Podmiot2.Adres.KodKraju = kodKraju;
		}
		else if (Regex.IsMatch(nipNabywcy, @"^\w\w") && Enum.TryParse<TKodKraju>(nipNabywcy[0..2], out var kodKraju))
		{
			ksefFaktura.Podmiot2.DaneIdentyfikacyjne.KodKraju = kodKraju;
			ksefFaktura.Podmiot2.DaneIdentyfikacyjne.NrID = nipNabywcy.Substring(2);
			if (dbFaktura.Nabywca?.Kraj == null) ksefFaktura.Podmiot2.Adres.KodKraju = kodKraju;
		}
		else
		{
			ksefFaktura.Podmiot2.DaneIdentyfikacyjne.NrID = nipNabywcy;
		}
		ksefFaktura.Podmiot2.DaneIdentyfikacyjne.Nazwa = dbFaktura.NazwaNabywcy;
		ksefFaktura.Podmiot2.GV = FakturaPodmiot2GV.Item2;
		ksefFaktura.Podmiot2.JST = FakturaPodmiot2JST.Item2;
		ksefFaktura.Fa = new FakturaFa();
		ksefFaktura.Fa.KodWaluty = Enum.Parse<TKodWaluty>(dbFaktura.Waluta.KodISO);
		ksefFaktura.Fa.P_1 = dbFaktura.DataWystawienia;
		ksefFaktura.Fa.P_2 = dbFaktura.Numer;
		ksefFaktura.Fa.P_6 = dbFaktura.DataSprzedazy;
		ksefFaktura.Fa.P_15 = dbFaktura.RazemBrutto;
		ksefFaktura.Fa.Adnotacje = new FakturaFaAdnotacje();
		ksefFaktura.Fa.Adnotacje.P_16 = TWybor1_2.Item2;
		ksefFaktura.Fa.Adnotacje.P_17 = TWybor1_2.Item2;
		ksefFaktura.Fa.Adnotacje.P_18 = TWybor1_2.Item2;
		ksefFaktura.Fa.Adnotacje.P_18A = dbFaktura.CzyMechanizmPodzielonejPlatnosci ? TWybor1_2.Item1 : TWybor1_2.Item2;
		ksefFaktura.Fa.Adnotacje.Zwolnienie = new FakturaFaAdnotacjeZwolnienie();
		ksefFaktura.Fa.Adnotacje.Zwolnienie.P_19N = TWybor1.Item1;
		ksefFaktura.Fa.Adnotacje.NoweSrodkiTransportu = new FakturaFaAdnotacjeNoweSrodkiTransportu();
		ksefFaktura.Fa.Adnotacje.NoweSrodkiTransportu.P_22N = TWybor1.Item1;
		ksefFaktura.Fa.Adnotacje.P_23 = TWybor1_2.Item2;
		ksefFaktura.Fa.Adnotacje.PMarzy = new FakturaFaAdnotacjePMarzy();
		if (dbFaktura.Rodzaj == RodzajFaktury.VatMarża || dbFaktura.Rodzaj == RodzajFaktury.KorektaVatMarży)
		{
			ksefFaktura.Fa.Adnotacje.PMarzy.P_PMarzy = TWybor1.Item1;
			if (dbFaktura.ProceduraMarzy == ProceduraMarży.TowaryUżywane) ksefFaktura.Fa.Adnotacje.PMarzy.P_PMarzy_3_1 = TWybor1.Item1;
			else if (dbFaktura.ProceduraMarzy == ProceduraMarży.DziełaSztuki) ksefFaktura.Fa.Adnotacje.PMarzy.P_PMarzy_3_2 = TWybor1.Item1;
			else if (dbFaktura.ProceduraMarzy == ProceduraMarży.BiuraPodróży) ksefFaktura.Fa.Adnotacje.PMarzy.P_PMarzy_2 = TWybor1.Item1;
			else if (dbFaktura.ProceduraMarzy == ProceduraMarży.PrzedmiotyKolekcjonerskie) ksefFaktura.Fa.Adnotacje.PMarzy.P_PMarzy_3_3 = TWybor1.Item1;
		}
		else
		{
			ksefFaktura.Fa.Adnotacje.PMarzy.P_PMarzyN = TWybor1.Item1;
		}
		ksefFaktura.Fa.RodzajFaktury = dbFaktura.Rodzaj == RodzajFaktury.Sprzedaż || dbFaktura.Rodzaj == RodzajFaktury.VatMarża ? TRodzajFaktury.VAT
			: dbFaktura.Rodzaj == DB.RodzajFaktury.KorektaSprzedaży || dbFaktura.Rodzaj == RodzajFaktury.KorektaVatMarży ? TRodzajFaktury.KOR
			: throw new ApplicationException("Nieobsługiwany rodzaj faktury: " + dbFaktura.RodzajFmt);
		if (dbFaktura.CzyTP) { ksefFaktura.Fa.TP = TWybor1.Item1; }
		var wplaty = dbFaktura.Wplaty.Where(e => !e.CzyRozliczenie).ToList();
		var obciazenia = dbFaktura.Wplaty.Where(e => e.CzyRozliczenie && e.Kwota < 0).ToList();
		var odliczenia = dbFaktura.Wplaty.Where(e => e.CzyRozliczenie && e.Kwota > 0).ToList();
		if (wysylajPlatnosc)
		{
			ksefFaktura.Fa.Platnosc = new FakturaFaPlatnosc();
			if (dbFaktura.PozostaloDoZaplaty == 0 && wplaty.Count > 0)
			{
				ksefFaktura.Fa.Platnosc.Zaplacono = TWybor1.Item1;
				ksefFaktura.Fa.Platnosc.DataZaplaty = wplaty.Last().Data;
			}
			else if (dbFaktura.PozostaloDoZaplaty < dbFaktura.RazemBrutto + obciazenia.Sum(e => e.Kwota) - odliczenia.Sum(e => e.Kwota))
			{
				ksefFaktura.Fa.Platnosc.ZnacznikZaplatyCzesciowej = TWybor1_2.Item1;
				foreach (var wplata in wplaty)
					ksefFaktura.Fa.Platnosc.ZaplataCzesciowa.Add(new FakturaFaPlatnoscZaplataCzesciowa { KwotaZaplatyCzesciowej = wplata.Kwota, DataZaplatyCzesciowej = wplata.Data });
			}
			if (dbFaktura.PozostaloDoZaplaty > 0) ksefFaktura.Fa.Platnosc.TerminPlatnosci.Add(new FakturaFaPlatnoscTerminPlatnosci { Termin = dbFaktura.TerminPlatnosci });
			if ((dbFaktura.OpisSposobuPlatnosci ?? "").Contains("gotówk", StringComparison.InvariantCultureIgnoreCase)) ksefFaktura.Fa.Platnosc.FormaPlatnosci = TFormaPlatnosci.Item1;
			else if ((dbFaktura.OpisSposobuPlatnosci ?? "").Contains("karta", StringComparison.InvariantCultureIgnoreCase)) ksefFaktura.Fa.Platnosc.FormaPlatnosci = TFormaPlatnosci.Item2;
			else if ((dbFaktura.OpisSposobuPlatnosci ?? "").Contains("kredyt", StringComparison.InvariantCultureIgnoreCase)) ksefFaktura.Fa.Platnosc.FormaPlatnosci = TFormaPlatnosci.Item5;
			else if ((dbFaktura.OpisSposobuPlatnosci ?? "").Contains("mobiln", StringComparison.InvariantCultureIgnoreCase)) ksefFaktura.Fa.Platnosc.FormaPlatnosci = TFormaPlatnosci.Item7;
			else ksefFaktura.Fa.Platnosc.FormaPlatnosci = TFormaPlatnosci.Item6;
			var ksefRachunek = ZbudujRachunekBankowyKSeF(dbFaktura);
			if (ksefRachunek != null) ksefFaktura.Fa.Platnosc.RachunekBankowy.Add(ksefRachunek);
		}

		if (obciazenia.Count != 0)
		{
			ksefFaktura.Fa.Rozliczenie ??= new FakturaFaRozliczenie();
			ksefFaktura.Fa.Rozliczenie.SumaObciazenValueSpecified = true;
			foreach (var obciazenie in obciazenia)
			{
				ksefFaktura.Fa.Rozliczenie.Obciazenia.Add(new FakturaFaRozliczenieObciazenia { Kwota = -obciazenie.Kwota, Powod = obciazenie.Uwagi });
				ksefFaktura.Fa.Rozliczenie.SumaObciazenValue += -obciazenie.Kwota;
			}
		}
		if (odliczenia.Count != 0)
		{
			ksefFaktura.Fa.Rozliczenie ??= new FakturaFaRozliczenie();
			ksefFaktura.Fa.Rozliczenie.SumaOdliczenValueSpecified = true;
			foreach (var odliczenie in odliczenia)
			{
				ksefFaktura.Fa.Rozliczenie.Odliczenia.Add(new FakturaFaRozliczenieOdliczenia {Kwota = odliczenie.Kwota, Powod = odliczenie.Uwagi });
				ksefFaktura.Fa.Rozliczenie.SumaOdliczenValue += odliczenie.Kwota;
			}
		}

		if (ksefFaktura.Fa.Rozliczenie != null)
		{
			var doZaplaty = ksefFaktura.Fa.P_15 + ksefFaktura.Fa.Rozliczenie.SumaObciazenValue - ksefFaktura.Fa.Rozliczenie.SumaOdliczenValue;
			if (doZaplaty > 0) ksefFaktura.Fa.Rozliczenie.DoZaplaty = doZaplaty;
			if (doZaplaty < 0) ksefFaktura.Fa.Rozliczenie.DoRozliczenia = -doZaplaty;
		}

		if (dbFaktura.FakturaKorygowana != null && dbFaktura.FakturaPierwotna != null)
		{
			ksefFaktura.Fa.TypKorekty = TTypKorekty.Item2;
			var ksefDaneKorygowanej = new FakturaFaDaneFaKorygowanej();
			ksefDaneKorygowanej.DataWystFaKorygowanej = dbFaktura.FakturaPierwotna.DataWystawienia;
			ksefDaneKorygowanej.NrFaKorygowanej = dbFaktura.FakturaPierwotna.Numer;
			if (String.IsNullOrEmpty(dbFaktura.FakturaPierwotna.NumerKSeF))
			{
				ksefDaneKorygowanej.NrKSeFN = TWybor1.Item1;
			}
			else
			{
				ksefDaneKorygowanej.NrKSeF = TWybor1.Item1;
				ksefDaneKorygowanej.NrKSeFFaKorygowanej = dbFaktura.FakturaPierwotna.NumerKSeF;
			}
			ksefFaktura.Fa.DaneFaKorygowanej.Add(ksefDaneKorygowanej);

			if (dbFaktura.FakturaKorygowana.NazwaSprzedawcy != dbFaktura.NazwaSprzedawcy || dbFaktura.FakturaKorygowana.DaneSprzedawcy != dbFaktura.DaneSprzedawcy)
			{
				ksefFaktura.Fa.Podmiot1K = new FakturaFaPodmiot1K();
				ksefFaktura.Fa.Podmiot1K.DaneIdentyfikacyjne = new TPodmiot1();
				ksefFaktura.Fa.Podmiot1K.DaneIdentyfikacyjne.Nazwa = dbFaktura.FakturaKorygowana.NazwaSprzedawcy;
				ksefFaktura.Fa.Podmiot1K.DaneIdentyfikacyjne.NIP = dbFaktura.FakturaKorygowana.NIPSprzedawcy.Replace("-", "");
				ksefFaktura.Fa.Podmiot1K.Adres = ZbudujAdres<TAdres>(dbFaktura.FakturaKorygowana.DaneSprzedawcy, kodKrajuSprzedawcy);
			}

			if (dbFaktura.FakturaKorygowana.NazwaNabywcy != dbFaktura.NazwaNabywcy || dbFaktura.FakturaKorygowana.DaneNabywcy != dbFaktura.DaneNabywcy)
			{
				var podmiot2k = new FakturaFaPodmiot2K();
				podmiot2k.DaneIdentyfikacyjne = new TPodmiot2();
				podmiot2k.DaneIdentyfikacyjne.Nazwa = dbFaktura.FakturaKorygowana.NazwaNabywcy;
				podmiot2k.DaneIdentyfikacyjne = ksefFaktura.Podmiot2.DaneIdentyfikacyjne;
				podmiot2k.Adres = ZbudujAdres<TAdres>(dbFaktura.FakturaKorygowana.DaneNabywcy, kodKrajuNabywcy);
				ksefFaktura.Fa.Podmiot2K.Add(podmiot2k);
			}
		}

		foreach (var dbPodmiot3 in dbFaktura.DodatkowePodmioty)
		{
			var ksefPodmiot3 = new FakturaPodmiot3();
			if (dbPodmiot3.Rodzaj == RodzajDodatkowegoPodmiotu.Inny) { ksefPodmiot3.RolaInna = TWybor1.Item1; ksefPodmiot3.OpisRoli = "Inny podmiot"; }
			else if (dbPodmiot3.Rodzaj == RodzajDodatkowegoPodmiotu.Faktor) ksefPodmiot3.Rola = TRolaPodmiotu3.Item1;
			else if (dbPodmiot3.Rodzaj == RodzajDodatkowegoPodmiotu.Odbiorca) ksefPodmiot3.Rola = TRolaPodmiotu3.Item2;
			else if (dbPodmiot3.Rodzaj == RodzajDodatkowegoPodmiotu.PodmiotPierwotny) ksefPodmiot3.Rola = TRolaPodmiotu3.Item3;
			else if (dbPodmiot3.Rodzaj == RodzajDodatkowegoPodmiotu.DodatkowyNabywca) ksefPodmiot3.Rola = TRolaPodmiotu3.Item4;
			else if (dbPodmiot3.Rodzaj == RodzajDodatkowegoPodmiotu.WystawcaFaktury) ksefPodmiot3.Rola = TRolaPodmiotu3.Item5;
			else if (dbPodmiot3.Rodzaj == RodzajDodatkowegoPodmiotu.DokonującyPłatności) ksefPodmiot3.Rola = TRolaPodmiotu3.Item6;
			else if (dbPodmiot3.Rodzaj == RodzajDodatkowegoPodmiotu.JSTWystawca) ksefPodmiot3.Rola = TRolaPodmiotu3.Item7;
			else if (dbPodmiot3.Rodzaj == RodzajDodatkowegoPodmiotu.JSTOdbiorca) ksefPodmiot3.Rola = TRolaPodmiotu3.Item8;
			else if (dbPodmiot3.Rodzaj == RodzajDodatkowegoPodmiotu.CzłonekGrupyVATWystawca) ksefPodmiot3.Rola = TRolaPodmiotu3.Item9;
			else if (dbPodmiot3.Rodzaj == RodzajDodatkowegoPodmiotu.CzłonekGrupyVATOdbiorca) ksefPodmiot3.Rola = TRolaPodmiotu3.Item10;
			else if (dbPodmiot3.Rodzaj == RodzajDodatkowegoPodmiotu.Pracownik) ksefPodmiot3.Rola = TRolaPodmiotu3.Item11;

			ksefPodmiot3.DaneIdentyfikacyjne = new TPodmiot3();
			if (!String.IsNullOrEmpty(dbPodmiot3.Nazwa)) ksefPodmiot3.DaneIdentyfikacyjne.Nazwa = dbPodmiot3.Nazwa;
			if (!String.IsNullOrEmpty(dbPodmiot3.NIP)) ksefPodmiot3.DaneIdentyfikacyjne.NIP = dbPodmiot3.NIP;
			if (!String.IsNullOrEmpty(dbPodmiot3.VatUE)) ksefPodmiot3.DaneIdentyfikacyjne.NrVatUE = dbPodmiot3.VatUE;
			if (!String.IsNullOrEmpty(dbPodmiot3.IDwew)) ksefPodmiot3.DaneIdentyfikacyjne.IDWew = dbPodmiot3.IDwew;
			if (!String.IsNullOrEmpty(dbPodmiot3.Adres)) ksefPodmiot3.Adres = ZbudujAdres<TAdres>(dbPodmiot3.Adres, TKodKraju.PL);
			dbPodmiot3.Udzial = ksefPodmiot3.Udzial;

			ksefFaktura.Podmiot3.Add(ksefPodmiot3);
		}

		foreach (var dbPozycja in dbFaktura.Pozycje)
		{
			// Pobrane przez wywołanie w ZbudujXML
			ArgumentNullException.ThrowIfNull(dbPozycja.StawkaVat);
			var ksefWiersz = new FakturaFaFaWiersz();
			ksefWiersz.NrWierszaFa = (ulong)dbPozycja.LP;
			//ksefWiersz.UU_ID = dbPozycja.Id.ToString();
			var opis = dbPozycja.Opis;
			opis = Regex.Replace(opis, @"PKWIU[: ]+(?<numer>[\d\.]+)", m => { ksefWiersz.PKWiU = m.Groups["numer"].Value; return ""; }, RegexOptions.IgnoreCase);
			ksefWiersz.P_7 = opis.Trim();
			//ksefWiersz.Indeks = dbPozycja.Towar == null ? ksefWiersz.UU_ID : dbPozycja.Towar.Id.ToString();

			ksefWiersz.P_8A = dbPozycja.JednostkaMiary?.Nazwa ?? "szt";
			ksefWiersz.P_8B = Math.Abs(dbPozycja.Ilosc);
			if (dbPozycja.RabatRazem != 0 && ksefWiersz.P_8B != 0) ksefWiersz.P_10 = Math.Min(dbPozycja.CzyWedlugCenBrutto ? dbPozycja.CenaBrutto : dbPozycja.CenaNetto, -dbPozycja.RabatRazem / ksefWiersz.P_8B.Value).Zaokragl();
			if (dbFaktura.KursWaluty != 0 && dbFaktura.KursWaluty != 1) ksefWiersz.KursWaluty = dbFaktura.KursWaluty;
			if (dbPozycja.CzyWedlugCenBrutto)
			{
				ksefWiersz.P_9B = dbPozycja.CenaBrutto;
				ksefWiersz.P_11A = Math.Abs(dbPozycja.WartoscBrutto);
			}
			else
			{
				ksefWiersz.P_9A = dbPozycja.CenaNetto;
				ksefWiersz.P_11 = Math.Abs(dbPozycja.WartoscNetto);
			}
			if (dbFaktura.ProceduraMarzy == ProceduraMarży.NieDotyczy) ksefWiersz.P_11Vat = Math.Abs(dbPozycja.WartoscVat);
			if (dbPozycja.GTU > 0) ksefWiersz.GTU = Enum.Parse<TGTU>("GTU_" + dbPozycja.GTU.ToString("00"));

			if (dbFaktura.ProceduraMarzy != ProceduraMarży.NieDotyczy)
			{
				ksefFaktura.Fa.P_13_11 ??= 0;
				ksefFaktura.Fa.P_13_11 += dbPozycja.WartoscBrutto;
			}
			else
			{
				var kodKSeF = PobierzKodKSeFDlaPozycji(dbFaktura, dbPozycja.StawkaVat);
				ksefWiersz.P_12 = MapujKodKSeFNaStawke(kodKSeF);
				DodajPodsumowanieStawki(ksefFaktura.Fa, kodKSeF, dbPozycja.WartoscNetto, dbPozycja.WartoscVat);
			}
			if (dbPozycja.CzyPrzedKorekta) ksefWiersz.StanPrzed = TWybor1.Item1;

			ksefFaktura.Fa.FaWiersz.Add(ksefWiersz);
		}

		var rejestry = new FakturaStopkaRejestry();
		var uwagi = dbFaktura.UwagiPubliczne;
		var zamowienie = new FakturaFaWarunkiTransakcjiZamowienia();
		uwagi = Regex.Replace(uwagi, @"BDO: (?<numer>\d{1,9})", m => { rejestry.BDO = m.Groups["numer"].Value; return ""; }, RegexOptions.IgnoreCase);
		uwagi = Regex.Replace(uwagi, @"KRS: (?<numer>\d{10})", m => { rejestry.KRS = m.Groups["numer"].Value; return ""; }, RegexOptions.IgnoreCase);
		uwagi = Regex.Replace(uwagi, @"(REGON|Regon|regon): (?<numer>(\d{9}|\d{14}))", m => { rejestry.REGON = m.Groups["numer"].Value; return ""; }, RegexOptions.IgnoreCase);
		uwagi = Regex.Replace(uwagi, @"(Zamówienie|Nr zamówienia|Numer zamówienia): (?<numer>.+)", m => { zamowienie.NrZamowienia = m.Groups["numer"].Value.Trim(); return ""; }, RegexOptions.IgnoreCase);
		uwagi = Regex.Replace(uwagi, @"Data zamówienia: (?<data>[0-9./\-]{8,10})", m => { if (!DateTime.TryParse(m.Groups["data"].Value, out var data)) return "Numer zamówienia: " + m.Groups["data"].Value; zamowienie.DataZamowienia = data; return ""; }, RegexOptions.IgnoreCase);
		uwagi = Regex.Replace(uwagi, @"Przyczyna korekty: (?<tekst>.+)", m => { ksefFaktura.Fa.PrzyczynaKorekty = m.Groups["tekst"].Value.Trim(); return ""; }, RegexOptions.IgnoreCase);
		uwagi = Regex.Replace(uwagi, @"(Mechanizm podzielonej płatności|Split payment)", m => { /* P_18A ustawione wcześniej */ return ""; }, RegexOptions.IgnoreCase);
		uwagi = Regex.Replace(uwagi, @"(?<nazwa>.+): (?<tekst>.+)", m => { ksefFaktura.Fa.DodatkowyOpis.Add(new TKluczWartosc() { Klucz = m.Groups["nazwa"].Value, Wartosc = m.Groups["tekst"].Value.Trim() }); return ""; });

		uwagi = uwagi.Trim(' ', '\r', '\n', '\t');
		if (!String.IsNullOrEmpty(uwagi)) ksefFaktura.Fa.DodatkowyOpis.Add(new TKluczWartosc() { Klucz = "Uwagi", Wartosc = uwagi });
		if (!String.IsNullOrEmpty(zamowienie.NrZamowienia) || zamowienie.DataZamowieniaValueSpecified)
		{
			ksefFaktura.Fa.WarunkiTransakcji = new FakturaFaWarunkiTransakcji();
			ksefFaktura.Fa.WarunkiTransakcji.Zamowienia.Add(zamowienie);
		}
		if (!String.IsNullOrEmpty(rejestry.BDO) || !String.IsNullOrEmpty(rejestry.KRS) || !String.IsNullOrEmpty(rejestry.REGON))
		{
			ksefFaktura.Stopka = new FakturaStopka();
			ksefFaktura.Stopka.Rejestry.Add(rejestry);
		}

		return ksefFaktura;
	}

	private static DBFaktura Zbuduj(KSEFFaktura ksefFaktura)
	{
		var dbFaktura = new DBFaktura();
		dbFaktura.Pozycje = [];
		dbFaktura.Wplaty = [];
		dbFaktura.Numer = ksefFaktura.Fa.P_2;
		dbFaktura.Rodzaj = ksefFaktura.Fa.RodzajFaktury switch
		{
			TRodzajFaktury.VAT or TRodzajFaktury.ROZ or TRodzajFaktury.UPR or TRodzajFaktury.ZAL => RodzajFaktury.Zakup,
			TRodzajFaktury.KOR or TRodzajFaktury.KOR_ROZ or TRodzajFaktury.KOR_ZAL => RodzajFaktury.KorektaZakupu,
			_ => throw new ApplicationException($"Nieobsługiwany rodzaj faktury: {ksefFaktura.Fa.RodzajFaktury}.")
		};
		dbFaktura.DataWystawienia = ksefFaktura.Fa.P_1;
		if (ksefFaktura.Fa.P_6.HasValue) dbFaktura.DataSprzedazy = ksefFaktura.Fa.P_6.Value;
		else dbFaktura.DataSprzedazy = dbFaktura.DataWystawienia;
		dbFaktura.Waluta = new Waluta { Skrot = ksefFaktura.Fa.KodWaluty.ToString(), Nazwa = ksefFaktura.Fa.KodWaluty.ToString() };
		dbFaktura.CzyTP = ksefFaktura.Fa.TP > 0;
		dbFaktura.Sprzedawca = new Kontrahent();
		if (ksefFaktura.Podmiot1 != null)
		{
			if (ksefFaktura.Podmiot1.DaneIdentyfikacyjne != null)
			{
				dbFaktura.Sprzedawca.Nazwa = dbFaktura.Sprzedawca.PelnaNazwa = dbFaktura.NazwaSprzedawcy = ksefFaktura.Podmiot1.DaneIdentyfikacyjne.Nazwa;
				dbFaktura.Sprzedawca.NIP = dbFaktura.NIPSprzedawcy = ksefFaktura.Podmiot1.DaneIdentyfikacyjne.NIP;
			}

			if (ksefFaktura.Podmiot1.Adres != null) dbFaktura.Sprzedawca.Kraj = new Kraj { KodISO2 = ksefFaktura.Podmiot1.Adres.KodKraju.ToString() };
			if (ksefFaktura.Podmiot1.Adres != null) dbFaktura.Sprzedawca.AdresRejestrowy = dbFaktura.DaneSprzedawcy = ksefFaktura.Podmiot1.Adres.AdresL1 + "\r\n" + ksefFaktura.Podmiot1.Adres.AdresL2;
			if (ksefFaktura.Podmiot1.AdresKoresp != null) dbFaktura.Sprzedawca.AdresKorespondencyjny = ksefFaktura.Podmiot1.AdresKoresp.AdresL1 + "\r\n" + ksefFaktura.Podmiot1.AdresKoresp.AdresL2;
			if (ksefFaktura.Podmiot1.DaneKontaktowe != null && ksefFaktura.Podmiot1.DaneKontaktowe.Count > 0)
			{
				dbFaktura.Sprzedawca.Telefon = ksefFaktura.Podmiot1.DaneKontaktowe[0].Telefon;
				dbFaktura.Sprzedawca.EMail = ksefFaktura.Podmiot1.DaneKontaktowe[0].Email;
			}
		}
		dbFaktura.Nabywca = new Kontrahent();
		if (ksefFaktura.Podmiot2 != null)
		{
			if (ksefFaktura.Podmiot2.DaneIdentyfikacyjne != null)
			{
				dbFaktura.Nabywca.Nazwa = dbFaktura.Nabywca.PelnaNazwa = dbFaktura.NazwaNabywcy = ksefFaktura.Podmiot2.DaneIdentyfikacyjne.Nazwa;
				if (!String.IsNullOrEmpty(ksefFaktura.Podmiot2.DaneIdentyfikacyjne.NIP)) dbFaktura.Nabywca.NIP = dbFaktura.NIPNabywcy = ksefFaktura.Podmiot2.DaneIdentyfikacyjne.NIP;
				else if (!String.IsNullOrEmpty(ksefFaktura.Podmiot2.DaneIdentyfikacyjne.NrVatUE)) dbFaktura.Nabywca.NIP = dbFaktura.NIPNabywcy = ksefFaktura.Podmiot2.DaneIdentyfikacyjne.KodUE + ksefFaktura.Podmiot2.DaneIdentyfikacyjne.NrVatUE;
				else if (!String.IsNullOrEmpty(ksefFaktura.Podmiot2.DaneIdentyfikacyjne.NrID)) dbFaktura.Nabywca.NIP = dbFaktura.NIPNabywcy = ksefFaktura.Podmiot2.DaneIdentyfikacyjne.KodKraju + ksefFaktura.Podmiot2.DaneIdentyfikacyjne.NrID;
			}

			if (ksefFaktura.Podmiot2.Adres != null) dbFaktura.Nabywca.Kraj = new Kraj { KodISO2 = ksefFaktura.Podmiot2.Adres.KodKraju.ToString() };
			if (ksefFaktura.Podmiot2.Adres != null) dbFaktura.Nabywca.AdresRejestrowy = dbFaktura.DaneNabywcy = ksefFaktura.Podmiot2.Adres.AdresL1 + "\r\n" + ksefFaktura.Podmiot2.Adres.AdresL2;
			if (ksefFaktura.Podmiot2.AdresKoresp != null) dbFaktura.Nabywca.AdresKorespondencyjny = ksefFaktura.Podmiot2.AdresKoresp.AdresL1 + "\r\n" + ksefFaktura.Podmiot2.AdresKoresp.AdresL2;
			if (ksefFaktura.Podmiot2.DaneKontaktowe != null && ksefFaktura.Podmiot2.DaneKontaktowe.Count > 0)
			{
				dbFaktura.Nabywca.Telefon = ksefFaktura.Podmiot2.DaneKontaktowe[0].Telefon;
				dbFaktura.Nabywca.EMail = ksefFaktura.Podmiot2.DaneKontaktowe[0].Email;
			}
		}
		if (ksefFaktura.Fa.Platnosc != null)
		{
			if (ksefFaktura.Fa.Platnosc.TerminPlatnosci != null && ksefFaktura.Fa.Platnosc.TerminPlatnosci.Count > 0 && ksefFaktura.Fa.Platnosc.TerminPlatnosci[0].Termin.HasValue) dbFaktura.TerminPlatnosci = ksefFaktura.Fa.Platnosc.TerminPlatnosci[0].Termin!.Value;
			if (ksefFaktura.Fa.Platnosc.RachunekBankowy != null && ksefFaktura.Fa.Platnosc.RachunekBankowy.Count > 0)
			{
				dbFaktura.RachunekBankowy = ksefFaktura.Fa.Platnosc.RachunekBankowy[0].NrRB;
				dbFaktura.NazwaBanku = ksefFaktura.Fa.Platnosc.RachunekBankowy[0].NazwaBanku;
			}

			dbFaktura.OpisSposobuPlatnosci = ksefFaktura.Fa.Platnosc.FormaPlatnosci switch
			{
				TFormaPlatnosci.Item1 => "Gotówka",
				TFormaPlatnosci.Item2 => "Karta",
				TFormaPlatnosci.Item3 => "Bon",
				TFormaPlatnosci.Item4 => "Czek",
				TFormaPlatnosci.Item5 => "Kredyt",
				TFormaPlatnosci.Item6 => "Przelew",
				TFormaPlatnosci.Item7 => "Mobilna",
				_ => ksefFaktura.Fa.Platnosc.OpisPlatnosci ?? "",
			};

			if (ksefFaktura.Fa.Platnosc.Zaplacono == TWybor1.Item1 && String.IsNullOrEmpty(dbFaktura.OpisSposobuPlatnosci)) dbFaktura.OpisSposobuPlatnosci = "Zapłacono";

			if (ksefFaktura.Fa.Platnosc.DataZaplaty.HasValue) dbFaktura.Wplaty.Add(new Wplata { Data = ksefFaktura.Fa.Platnosc.DataZaplaty.Value, Kwota = ksefFaktura.Fa.P_15 });
		}

		if ((ksefFaktura.Fa.RodzajFaktury == TRodzajFaktury.ZAL || ksefFaktura.Fa.RodzajFaktury == TRodzajFaktury.KOR_ZAL)
			&& !ksefFaktura.Fa.FaWierszSpecified
			&& ksefFaktura.Fa.Zamowienie != null
			&& ksefFaktura.Fa.Zamowienie.ZamowienieWiersz.Count > 0)
		{
			foreach (var wierszZamowienia in ksefFaktura.Fa.Zamowienie.ZamowienieWiersz)
			{
				var wierszFaktury = new FakturaFaFaWiersz();
				wierszFaktury.NrWierszaFa = wierszZamowienia.NrWierszaZam;
				wierszFaktury.UU_ID = wierszZamowienia.UU_IDZ;
				wierszFaktury.P_7 = wierszZamowienia.P_7Z;
				wierszFaktury.Indeks = wierszZamowienia.IndeksZ;
				wierszFaktury.GTIN = wierszZamowienia.GTINZ;
				wierszFaktury.PKWiU = wierszZamowienia.PKWiUZ;
				wierszFaktury.CN = wierszZamowienia.CNZ;
				wierszFaktury.PKOB = wierszZamowienia.PKOBZ;
				wierszFaktury.P_8A = wierszZamowienia.P_8AZ;
				wierszFaktury.P_8B = wierszZamowienia.P_8BZ;
				wierszFaktury.P_9A = wierszZamowienia.P_9AZ;
				wierszFaktury.P_11 = wierszZamowienia.P_11NettoZ;
				wierszFaktury.P_11Vat = wierszZamowienia.P_11VatZ;
				wierszFaktury.P_12 = wierszZamowienia.P_12Z;
				wierszFaktury.P_12_XII = wierszZamowienia.P_12Z_XII;
				wierszFaktury.P_12_Zal_15 = wierszZamowienia.P_12Z_Zal_15;
				wierszFaktury.GTU = wierszZamowienia.GTUZ;
				wierszFaktury.Procedura = (TOznaczenieProcedury?)(int?)wierszZamowienia.ProceduraZ;
				wierszFaktury.KwotaAkcyzy = wierszZamowienia.KwotaAkcyzyZ;
				wierszFaktury.StanPrzed = wierszZamowienia.StanPrzedZ;
				ksefFaktura.Fa.FaWiersz.Add(wierszFaktury);
			}
		}

		foreach (var pozycja in ksefFaktura.Fa.FaWiersz)
		{
			var dbPozycja = new PozycjaFaktury();
			if (pozycja.NrWierszaFa > 100 && (int)pozycja.NrWierszaFa > ksefFaktura.Fa.FaWiersz.Count) dbPozycja.LP = dbFaktura.Pozycje.Count + 1;
			else dbPozycja.LP = (int)pozycja.NrWierszaFa;
			dbPozycja.Opis = pozycja.P_7 ?? "";
			if (!String.IsNullOrEmpty(pozycja.PKWiU)) dbPozycja.Opis += "  PKWiU: " + pozycja.PKWiU;
			dbPozycja.Ilosc = pozycja.P_8B ?? 1;
			dbPozycja.RabatCena = pozycja.P_10Value;
			if (ksefFaktura.Fa.RodzajFaktury == TRodzajFaktury.UPR && dbPozycja.LP == 1)
			{
				dbPozycja.CzyWedlugCenBrutto = true;
				dbPozycja.CenaBrutto = ksefFaktura.Fa.P_15;
				dbPozycja.WartoscBrutto = ksefFaktura.Fa.P_15;
			}
			else if (pozycja.P_9B.HasValue && pozycja.P_11A.HasValue)
			{
				dbPozycja.CzyWedlugCenBrutto = true;
				dbPozycja.CenaBrutto = pozycja.P_9B.Value;
				dbPozycja.WartoscBrutto = pozycja.P_11A.Value;
			}
			else if (pozycja.P_9B.HasValue)
			{
				dbPozycja.CzyWedlugCenBrutto = true;
				dbPozycja.CenaBrutto = pozycja.P_9B.Value;
				dbPozycja.WartoscBrutto = dbPozycja.CenaBrutto * dbPozycja.Ilosc;
			}
			else if (pozycja.P_11A.HasValue && dbPozycja.Ilosc != 0)
			{
				dbPozycja.CzyWedlugCenBrutto = true;
				dbPozycja.WartoscBrutto = pozycja.P_11A.Value;
				dbPozycja.CenaBrutto = dbPozycja.WartoscBrutto / dbPozycja.Ilosc;
			}
			else
			{
				dbPozycja.CenaNetto = pozycja.P_9A.GetValueOrDefault();
				dbPozycja.WartoscNetto = pozycja.P_11.GetValueOrDefault();
			}
			dbPozycja.Towar = new Towar();
			dbPozycja.Towar.Nazwa = pozycja.P_7 ?? "";
			dbPozycja.Towar.JednostkaMiary = dbPozycja.JednostkaMiary = new JednostkaMiary { Nazwa = pozycja.P_8A, Skrot = pozycja.P_8A };
			var kodStawki = MapujStawkeNaKodKSeF(pozycja.P_12.GetValueOrDefault(TStawkaPodatku.Item23));
			dbPozycja.StawkaVat = new StawkaVat { KodKSeF = kodStawki };
			dbPozycja.StawkaVat.Normalizuj();
			if (dbPozycja.CzyWedlugCenBrutto)
			{
				dbPozycja.CenaNetto = (dbPozycja.CenaBrutto * 100m / (100 + dbPozycja.StawkaVat.Wartosc)).Zaokragl();
				dbPozycja.CenaVat = (dbPozycja.CenaBrutto - dbPozycja.CenaNetto).Zaokragl();
				dbPozycja.WartoscNetto = (dbPozycja.WartoscBrutto * 100m / (100 + dbPozycja.StawkaVat.Wartosc)).Zaokragl();
				dbPozycja.WartoscVat = (dbPozycja.WartoscBrutto - dbPozycja.WartoscNetto).Zaokragl();
				var wartoscBrutto = (dbPozycja.Ilosc * (dbPozycja.CenaBrutto - dbPozycja.RabatCena)).Zaokragl();
				if (wartoscBrutto != dbPozycja.WartoscBrutto) dbPozycja.CzyWartosciReczne = true;
			}
			else
			{
				dbPozycja.CenaVat = ((dbPozycja.CenaNetto - dbPozycja.RabatCena) * dbPozycja.StawkaVat.Wartosc / 100).Zaokragl();
				dbPozycja.CenaBrutto = ((dbPozycja.CenaNetto - dbPozycja.RabatCena) + dbPozycja.CenaVat).Zaokragl();
				dbPozycja.WartoscVat = (dbPozycja.WartoscNetto * dbPozycja.StawkaVat.Wartosc / 100).Zaokragl();
				dbPozycja.WartoscBrutto = (dbPozycja.WartoscNetto + dbPozycja.WartoscVat).Zaokragl();
				var wartoscNetto = (dbPozycja.Ilosc * (dbPozycja.CenaNetto - dbPozycja.RabatCena)).Zaokragl();
				if (wartoscNetto != dbPozycja.WartoscNetto) dbPozycja.CzyWartosciReczne = true;
			}
			if (pozycja.StanPrzed == TWybor1.Item1)
			{
				dbPozycja.CzyPrzedKorekta = true;
				dbPozycja.Ilosc = -dbPozycja.Ilosc;
				dbPozycja.WartoscNetto = -dbPozycja.WartoscNetto;
				dbPozycja.WartoscVat = -dbPozycja.WartoscVat;
				dbPozycja.WartoscBrutto = -dbPozycja.WartoscBrutto;
			}
			dbFaktura.Pozycje.Add(dbPozycja);
			dbFaktura.RazemNetto += dbPozycja.WartoscNetto;
			dbFaktura.RazemVat += dbPozycja.WartoscVat;
			dbFaktura.RazemBrutto += dbPozycja.WartoscBrutto;
			if (pozycja.KursWalutyValueSpecified) dbFaktura.KursWaluty = pozycja.KursWalutyValue;
		}

		if (dbFaktura.RazemBrutto != ksefFaktura.Fa.P_15)
		{
			dbFaktura.RazemBrutto = ksefFaktura.Fa.P_15;
			dbFaktura.CzyWartosciReczne = true;
		}

		var razemNetto = ksefFaktura.Fa.P_13_1Value
			+ ksefFaktura.Fa.P_13_2Value
			+ ksefFaktura.Fa.P_13_3Value
			+ ksefFaktura.Fa.P_13_4Value
			+ ksefFaktura.Fa.P_13_5Value
			+ ksefFaktura.Fa.P_13_6_1Value
			+ ksefFaktura.Fa.P_13_6_2Value
			+ ksefFaktura.Fa.P_13_6_3Value
			+ ksefFaktura.Fa.P_13_7Value
			+ ksefFaktura.Fa.P_13_8Value
			+ ksefFaktura.Fa.P_13_9Value
			+ ksefFaktura.Fa.P_13_10Value
			+ ksefFaktura.Fa.P_13_11Value;

		if (dbFaktura.RazemNetto != razemNetto)
		{
			dbFaktura.RazemNetto = razemNetto;
			dbFaktura.CzyWartosciReczne = true;
		}

		var razemVat = ksefFaktura.Fa.P_14_1Value
			+ ksefFaktura.Fa.P_14_2Value
			+ ksefFaktura.Fa.P_14_3Value
			+ ksefFaktura.Fa.P_14_4Value
			+ ksefFaktura.Fa.P_14_5Value;

		if (dbFaktura.RazemVat != razemVat)
		{
			dbFaktura.RazemVat = razemVat;
			dbFaktura.CzyWartosciReczne = true;
		}

		var uwagi = new StringBuilder();

		if (ksefFaktura.Fa.RodzajFaktury == TRodzajFaktury.UPR) uwagi.AppendLine("Faktura uproszczona");
		else if (ksefFaktura.Fa.RodzajFaktury == TRodzajFaktury.ROZ || ksefFaktura.Fa.RodzajFaktury == TRodzajFaktury.KOR_ROZ) uwagi.AppendLine("Faktura rozliczająca");
		else if (ksefFaktura.Fa.RodzajFaktury == TRodzajFaktury.ZAL || ksefFaktura.Fa.RodzajFaktury == TRodzajFaktury.KOR_ZAL) uwagi.AppendLine("Faktura zaliczkowa");

		foreach (var opis in ksefFaktura.Fa.DodatkowyOpis)
		{
			uwagi.AppendLine($"{opis.Klucz}: {opis.Wartosc}");
		}

		foreach (var ksefFakturaKorygowana in ksefFaktura.Fa.DaneFaKorygowanej)
		{
			dbFaktura.FakturaKorygowana = new DBFaktura();
			if (!String.IsNullOrEmpty(ksefFakturaKorygowana.NrKSeFFaKorygowanej)) dbFaktura.FakturaKorygowana.NumerKSeF = ksefFakturaKorygowana.NrKSeFFaKorygowanej;
			if (!String.IsNullOrEmpty(ksefFakturaKorygowana.NrFaKorygowanej)) dbFaktura.FakturaKorygowana.Numer = ksefFakturaKorygowana.NrFaKorygowanej;
			dbFaktura.DataWystawienia = ksefFakturaKorygowana.DataWystFaKorygowanej;
		}

		if (!String.IsNullOrEmpty(ksefFaktura.Fa.PrzyczynaKorekty)) uwagi.AppendLine($"Przyczyna korekty: {ksefFaktura.Fa.PrzyczynaKorekty}");

		foreach (var fakturaZaliczkowa in ksefFaktura.Fa.FakturaZaliczkowa)
		{
			if (fakturaZaliczkowa.NrKSeFZN == TWybor1.Item1) uwagi.AppendLine($"Numer faktury zaliczkowej: {fakturaZaliczkowa.NrFaZaliczkowej}");
			else uwagi.AppendLine($"Numer faktury zaliczkowej: {fakturaZaliczkowa.NrKSeFFaZaliczkowej}");
		}

		dbFaktura.DodatkowePodmioty = new List<DodatkowyPodmiot>();
		foreach (var ksefPodmiot3 in ksefFaktura.Podmiot3)
		{
			var dbPodmiot3 = new DodatkowyPodmiot();
			if (ksefPodmiot3.Rola == TRolaPodmiotu3.Item1) dbPodmiot3.Rodzaj = RodzajDodatkowegoPodmiotu.Faktor;
			if (ksefPodmiot3.Rola == TRolaPodmiotu3.Item2) dbPodmiot3.Rodzaj = RodzajDodatkowegoPodmiotu.Odbiorca;
			if (ksefPodmiot3.Rola == TRolaPodmiotu3.Item3) dbPodmiot3.Rodzaj = RodzajDodatkowegoPodmiotu.PodmiotPierwotny;
			if (ksefPodmiot3.Rola == TRolaPodmiotu3.Item4) dbPodmiot3.Rodzaj = RodzajDodatkowegoPodmiotu.DodatkowyNabywca;
			if (ksefPodmiot3.Rola == TRolaPodmiotu3.Item5) dbPodmiot3.Rodzaj = RodzajDodatkowegoPodmiotu.WystawcaFaktury;
			if (ksefPodmiot3.Rola == TRolaPodmiotu3.Item6) dbPodmiot3.Rodzaj = RodzajDodatkowegoPodmiotu.DokonującyPłatności;
			if (ksefPodmiot3.Rola == TRolaPodmiotu3.Item7) dbPodmiot3.Rodzaj = RodzajDodatkowegoPodmiotu.JSTWystawca;
			if (ksefPodmiot3.Rola == TRolaPodmiotu3.Item8) dbPodmiot3.Rodzaj = RodzajDodatkowegoPodmiotu.JSTOdbiorca;
			if (ksefPodmiot3.Rola == TRolaPodmiotu3.Item9) dbPodmiot3.Rodzaj = RodzajDodatkowegoPodmiotu.CzłonekGrupyVATWystawca;
			if (ksefPodmiot3.Rola == TRolaPodmiotu3.Item10) dbPodmiot3.Rodzaj = RodzajDodatkowegoPodmiotu.CzłonekGrupyVATOdbiorca;
			if (ksefPodmiot3.Rola == TRolaPodmiotu3.Item11) dbPodmiot3.Rodzaj = RodzajDodatkowegoPodmiotu.Pracownik;
			dbPodmiot3.Nazwa = ksefPodmiot3.DaneIdentyfikacyjne.Nazwa;
			dbPodmiot3.NIP = ksefPodmiot3.DaneIdentyfikacyjne.NIP;
			dbPodmiot3.VatUE = ksefPodmiot3.DaneIdentyfikacyjne.NrVatUE;
			dbPodmiot3.IDwew = ksefPodmiot3.DaneIdentyfikacyjne.IDWew;
			if (ksefPodmiot3.Adres != null) dbPodmiot3.Adres = ksefPodmiot3.Adres.AdresL1 + "\n" + ksefPodmiot3.Adres.AdresL2;
			dbPodmiot3.Udzial = ksefPodmiot3.Udzial;
			dbFaktura.DodatkowePodmioty.Add(dbPodmiot3);
		}

		if (ksefFaktura.Fa.Adnotacje != null)
		{
			if (ksefFaktura.Fa.Adnotacje.Zwolnienie != null)
			{
				if (ksefFaktura.Fa.Adnotacje.Zwolnienie.P_19 == TWybor1.Item1) uwagi.AppendLine("Dostawa towarów lub świadczenie usług zwolnionych od podatku na podstawie art. 43 ust. 1, art. 113 ust. 1 i 9 albo przepisów wydanych na podstawie art. 82 ust. 3 lub na podstawie innych przepisów");
				if (!String.IsNullOrEmpty(ksefFaktura.Fa.Adnotacje.Zwolnienie.P_19A)) uwagi.AppendLine($"Podstawa zwolnienia od podatku: {ksefFaktura.Fa.Adnotacje.Zwolnienie.P_19A}");
				if (!String.IsNullOrEmpty(ksefFaktura.Fa.Adnotacje.Zwolnienie.P_19B)) uwagi.AppendLine($"Podstawa zwolnienia od podatku: {ksefFaktura.Fa.Adnotacje.Zwolnienie.P_19B}");
				if (!String.IsNullOrEmpty(ksefFaktura.Fa.Adnotacje.Zwolnienie.P_19C)) uwagi.AppendLine($"Podstawa zwolnienia od podatku: {ksefFaktura.Fa.Adnotacje.Zwolnienie.P_19C}");
			}

			if (ksefFaktura.Fa.Adnotacje.PMarzy != null)
			{
				if (ksefFaktura.Fa.Adnotacje.PMarzy.P_PMarzy_3_1ValueSpecified) { dbFaktura.ProceduraMarzy = ProceduraMarży.TowaryUżywane; uwagi.AppendLine("Procedura marży: towary używane"); }
				if (ksefFaktura.Fa.Adnotacje.PMarzy.P_PMarzy_3_2ValueSpecified) { dbFaktura.ProceduraMarzy = ProceduraMarży.DziełaSztuki; uwagi.AppendLine("Procedura marży: dzieła sztuki"); }
				if (ksefFaktura.Fa.Adnotacje.PMarzy.P_PMarzy_2ValueSpecified) { dbFaktura.ProceduraMarzy = ProceduraMarży.BiuraPodróży; uwagi.AppendLine("Procedura marży: biura podróży"); }
				if (ksefFaktura.Fa.Adnotacje.PMarzy.P_PMarzy_3_3ValueSpecified) { dbFaktura.ProceduraMarzy = ProceduraMarży.PrzedmiotyKolekcjonerskie; uwagi.AppendLine("Procedura marży: przedmioty kolekcjonerskie i antyki"); }
			}

			if (ksefFaktura.Fa.Adnotacje.P_18A == TWybor1_2.Item1)
			{
				uwagi.AppendLine("Mechanizm podzielonej płatności");
				if (!String.IsNullOrEmpty(dbFaktura.OpisSposobuPlatnosci)) dbFaktura.OpisSposobuPlatnosci += ", mechanizm podzielonej płatności";
			}
			if (ksefFaktura.Fa.Adnotacje.P_16 == TWybor1_2.Item1) uwagi.AppendLine("Metoda kasowa");
			if (ksefFaktura.Fa.Adnotacje.P_17 == TWybor1_2.Item1) uwagi.AppendLine("Samofakturowanie");
			if (ksefFaktura.Fa.Adnotacje.P_18 == TWybor1_2.Item1) uwagi.AppendLine("Odwrotne obciążenie");
			if (ksefFaktura.Fa.Adnotacje.P_23 == TWybor1_2.Item1) uwagi.AppendLine("Procedura trójstronna uproszczona");

			// Pominięte: NoweSrodkiTransportu
		}

		if (ksefFaktura.Fa.Rozliczenie != null)
		{
			foreach (var obciazenie in ksefFaktura.Fa.Rozliczenie.Obciazenia)
			{
				if (obciazenie.Kwota == 0) continue;
				dbFaktura.Wplaty.Add(new Wplata { Data = dbFaktura.DataWystawienia, Kwota = -obciazenie.Kwota, Uwagi = obciazenie.Powod, CzyRozliczenie = true });
			}
			foreach (var odliczenie in ksefFaktura.Fa.Rozliczenie.Odliczenia)
			{
				if (odliczenie.Kwota == 0) continue;
				dbFaktura.Wplaty.Add(new Wplata { Data = dbFaktura.DataWystawienia, Kwota = odliczenie.Kwota, Uwagi = odliczenie.Powod, CzyRozliczenie = true });
			}

			if (ksefFaktura.Fa.Rozliczenie.DoZaplatyValueSpecified && ksefFaktura.Fa.Rozliczenie.DoZaplatyValue != dbFaktura.RazemBrutto) uwagi.AppendLine($"Do zapłaty: {ksefFaktura.Fa.Rozliczenie.DoZaplatyValue:0.00} {ksefFaktura.Fa.KodWaluty}");
			if (ksefFaktura.Fa.Rozliczenie.DoRozliczeniaValueSpecified) uwagi.AppendLine($"Do rozliczenia: {ksefFaktura.Fa.Rozliczenie.DoRozliczeniaValue:0.00} {ksefFaktura.Fa.KodWaluty}");
		}

		if (ksefFaktura.Fa.WarunkiTransakcji != null)
		{
			foreach (var zamowienie in ksefFaktura.Fa.WarunkiTransakcji.Zamowienia)
			{
				if (zamowienie.DataZamowieniaValueSpecified) uwagi.AppendLine($"Data zamówienia: {zamowienie.DataZamowieniaValue:yyyy-MM-dd}");
				uwagi.AppendLine($"Numer zamówienia: {zamowienie.NrZamowienia}");
			}

			foreach (var partia in ksefFaktura.Fa.WarunkiTransakcji.NrPartiiTowaru)
			{
				uwagi.AppendLine($"Numer partii: {partia}");
			}

			if (!String.IsNullOrEmpty(ksefFaktura.Fa.WarunkiTransakcji.WarunkiDostawy)) uwagi.AppendLine($"Warunki dostawy: {ksefFaktura.Fa.WarunkiTransakcji.WarunkiDostawy}");

			foreach (var transport in ksefFaktura.Fa.WarunkiTransakcji.Transport)
			{
				uwagi.AppendLine(transport.RodzajTransportu switch
				{
					TRodzajTransportu.Item1 => "Rodzaj transportu: Transport morski",
					TRodzajTransportu.Item2 => "Rodzaj transportu: Transport kolejowy",
					TRodzajTransportu.Item3 => "Rodzaj transportu: Transport drogowy",
					TRodzajTransportu.Item4 => "Rodzaj transportu: Transport lotniczy",
					TRodzajTransportu.Item5 => "Rodzaj transportu: Przesyłka pocztowa",
					TRodzajTransportu.Item7 => "Rodzaj transportu: Stałe instalacje przesyłowe",
					TRodzajTransportu.Item8 => "Rodzaj transportu: Żegluga śródlądowa",
					_ => "Rodzaj transportu: " + transport.RodzajTransportu
				});
				if (!String.IsNullOrEmpty(transport.OpisInnegoTransportu)) uwagi.AppendLine($"Rodzaj transportu: {transport.OpisInnegoTransportu}");

				if (transport.Przewoznik != null)
				{
					if (transport.Przewoznik.DaneIdentyfikacyjne != null) uwagi.AppendLine($"Przewoźnik: {transport.Przewoznik.DaneIdentyfikacyjne.Nazwa}");
					// Pominięte: DaneIdentyfikacyjne.NIP, AdresPrzewoznika
				}

				uwagi.AppendLine(transport.OpisLadunku switch
				{
					TLadunek.Item1 => "Opis ładunku: Bańka",
					TLadunek.Item2 => "Opis ładunku: Beczka",
					TLadunek.Item3 => "Opis ładunku: Butla",
					TLadunek.Item4 => "Opis ładunku: Karton",
					TLadunek.Item5 => "Opis ładunku: Kanister",
					TLadunek.Item6 => "Opis ładunku: Klatka",
					TLadunek.Item7 => "Opis ładunku: Kontener",
					TLadunek.Item8 => "Opis ładunku: Kosz/koszyk",
					TLadunek.Item9 => "Opis ładunku: Łubianka",
					TLadunek.Item10 => "Opis ładunku: Opakowanie zbiorcze",
					TLadunek.Item11 => "Opis ładunku: Paczka",
					TLadunek.Item12 => "Opis ładunku: Pakiet",
					TLadunek.Item13 => "Opis ładunku: Paleta",
					TLadunek.Item14 => "Opis ładunku: Pojemnik",
					TLadunek.Item15 => "Opis ładunku: Pojemnik do ładunków masowych stałych",
					TLadunek.Item16 => "Opis ładunku: Pojemnik do ładunków masowych w postaci płynnej",
					TLadunek.Item17 => "Opis ładunku: Pudełko",
					TLadunek.Item18 => "Opis ładunku: Puszka",
					TLadunek.Item19 => "Opis ładunku: Skrzynia",
					TLadunek.Item20 => "Opis ładunku: Worek",
					_ => "Opis ładunku: " + transport.OpisLadunku
				});

				if (!String.IsNullOrEmpty(transport.OpisInnegoLadunku)) uwagi.AppendLine($"Opis ładunku: {transport.OpisInnegoLadunku}");
				if (!String.IsNullOrEmpty(transport.JednostkaOpakowania)) uwagi.AppendLine($"Jednostka opakowania: {transport.JednostkaOpakowania}");
				if (transport.DataGodzRozpTransportuValueSpecified) uwagi.AppendLine($"Czas rozpoczęcia transportu: {transport.DataGodzRozpTransportuValue:yyyy-MM-dd HH:mm}");

				// Pominięte: WysylkaZ, WysylkaDo
			}
		}

		if (ksefFaktura.Stopka != null)
		{
			foreach (var informacja in ksefFaktura.Stopka.Informacje)
			{
				uwagi.AppendLine(informacja.StopkaFaktury);
			}

			foreach (var rejestr in ksefFaktura.Stopka.Rejestry)
			{
				if (!String.IsNullOrEmpty(rejestr.PelnaNazwa)) uwagi.AppendLine($"Pełna nazwa: {rejestr.PelnaNazwa}");
				if (!String.IsNullOrEmpty(rejestr.KRS)) uwagi.AppendLine($"KRS: {rejestr.KRS}");
				if (!String.IsNullOrEmpty(rejestr.REGON)) uwagi.AppendLine($"REGON: {rejestr.REGON}");
				if (!String.IsNullOrEmpty(rejestr.BDO)) uwagi.AppendLine($"BDO: {rejestr.BDO}");
			}
		}

		dbFaktura.UwagiPubliczne = uwagi.ToString();

		return dbFaktura;
	}

	private static Kontrahent ZnajdzLubUtworzKontrahenta(Baza baza, Kontrahent kontrahent)
	{
		if (kontrahent.Id > 0) return kontrahent;
		var nip = kontrahent.NIP;
		if (String.IsNullOrEmpty(nip)) return kontrahent;
		if (nip.StartsWith("PL")) nip = nip.Substring(2);
		var nipPL = $"PL{nip}";
		var kontrahentDb = baza.Kontrahenci.FirstOrDefault(kontrahent => kontrahent.NIP.Replace("-", "") == nip || kontrahent.NIP.Replace("-", "") == nipPL);
		if (kontrahentDb != null) return kontrahentDb;
		kontrahent.CzyImportKSeF = true;
		baza.Zapisz(kontrahent);
		return kontrahent;
	}

	private static void PowiazKrajKontrahenta(Baza baza, Kontrahent kontrahent, string? kodISO2, string? numerIdentyfikacyjny, bool domyslniePL = false)
	{
		kodISO2 = (kodISO2 ?? "").Trim().ToUpperInvariant();
		if (String.IsNullOrWhiteSpace(kodISO2))
		{
			var numer = OczyscNumerIdentyfikacyjny(numerIdentyfikacyjny);
			if (Regex.IsMatch(numer, @"^\w\w")) kodISO2 = numer[0..2];
			else if (domyslniePL) kodISO2 = "PL";
		}
		if (String.IsNullOrWhiteSpace(kodISO2)) return;

		var kraj = baza.Kraje.FirstOrDefault(e => e.KodISO2 == kodISO2);
		if (kraj == null) return;
		if (kontrahent.KrajId == kraj.Id) return;
		if (kontrahent.Id <= 0)
		{
			kontrahent.Kraj = kraj;
			kontrahent.KrajRef = kraj;
			return;
		}

		kontrahent.KrajRef = kraj;
		baza.Zapisz(kontrahent);
	}

	private static string NormalizujOpisPlatnosci(string? opis)
	{
		if (String.IsNullOrWhiteSpace(opis)) return "";
		var wynik = opis.Trim().ToLowerInvariant();
		wynik = wynik.Replace("mechanizm podzielonej płatności", "mpp");
		wynik = wynik.Replace("mechanizm podzielonej platnosci", "mpp");
		wynik = wynik.Replace("split payment", "mpp");
		wynik = wynik.Replace("płatność", "platnosc");
		wynik = wynik.Replace("płatnosc", "platnosc");
		wynik = wynik.Replace("gotówka", "gotowka");
		wynik = wynik.Replace("przelew bankowy", "przelew");
		wynik = wynik.Replace("wire transfer", "przelew");
		wynik = wynik.Replace("bank transfer", "przelew");
		wynik = wynik.Replace("card", "karta");
		wynik = wynik.Replace("cash", "gotowka");
		wynik = wynik.Replace("check", "czek");
		wynik = wynik.Replace("mobile", "mobilna");
		wynik = wynik.Replace("blik", "mobilna");
		wynik = wynik.Replace(",", " ");
		wynik = wynik.Replace(";", " ");
		while (wynik.Contains("  ")) wynik = wynik.Replace("  ", " ");
		return wynik;
	}

	private static string? RozpoznajTypPlatnosci(string opis)
	{
		if (String.IsNullOrWhiteSpace(opis)) return null;
		if (opis.Contains("gotow")) return "gotowka";
		if (opis.Contains("kart")) return "karta";
		if (opis.Contains("bon")) return "bon";
		if (opis.Contains("czek")) return "czek";
		if (opis.Contains("kredyt")) return "kredyt";
		if (opis.Contains("mobil")) return "mobilna";
		if (opis.Contains("przelew")) return "przelew";
		return null;
	}

	private static int PoliczDopasowanieSposobuPlatnosci(SposobPlatnosci sposobPlatnosci, string opisPlatnosci)
	{
		if (String.IsNullOrWhiteSpace(opisPlatnosci)) return 0;
		var nazwa = NormalizujOpisPlatnosci(sposobPlatnosci.Nazwa);
		if (String.IsNullOrWhiteSpace(nazwa)) return 0;
		if (nazwa == opisPlatnosci) return 100;

		var wynik = 0;
		if (nazwa.Contains(opisPlatnosci, StringComparison.CurrentCultureIgnoreCase)
			|| opisPlatnosci.Contains(nazwa, StringComparison.CurrentCultureIgnoreCase)) wynik += 80;

		var typDokumentu = RozpoznajTypPlatnosci(opisPlatnosci);
		var typSlownika = RozpoznajTypPlatnosci(nazwa);
		if (typDokumentu != null && typDokumentu == typSlownika) wynik += 40;

		if (opisPlatnosci.Contains("mpp") && nazwa.Contains("mpp")) wynik += 20;
		if (opisPlatnosci.Contains("zaplacono") && nazwa.Contains("gotow")) wynik += 10;

		return wynik;
	}

	private static SposobPlatnosci? DopasujSposobPlatnosci(Baza baza, DBFaktura faktura)
	{
		var sposobyPlatnosci = baza.SposobyPlatnosci.ToList();
		var opisPlatnosci = NormalizujOpisPlatnosci(faktura.OpisSposobuPlatnosci);
		if (String.IsNullOrWhiteSpace(opisPlatnosci)) return sposobyPlatnosci.FirstOrDefault(sposob => sposob.CzyDomyslny);

		var liczbaDni = Math.Abs((faktura.TerminPlatnosci - faktura.DataWystawienia).TotalDays);
		return sposobyPlatnosci
			.Select(sposob => new
			{
				Sposob = sposob,
				Wynik = PoliczDopasowanieSposobuPlatnosci(sposob, opisPlatnosci),
				RoznicaDni = Math.Abs(sposob.LiczbaDni - liczbaDni),
			})
			.Where(pozycja => pozycja.Wynik > 0)
			.OrderByDescending(pozycja => pozycja.Wynik)
			.ThenBy(pozycja => pozycja.RoznicaDni)
			.ThenBy(pozycja => pozycja.Sposob.CzyDomyslny ? 0 : 1)
			.Select(pozycja => pozycja.Sposob)
			.FirstOrDefault();
	}

	private static void PoprawPowiazaniaPoImporcie(Baza baza, DBFaktura faktura)
	{
		// Zawsze ustawione przez Zbuduj:
		ArgumentNullException.ThrowIfNull(faktura.Sprzedawca);
		ArgumentNullException.ThrowIfNull(faktura.Nabywca);
		ArgumentNullException.ThrowIfNull(faktura.Waluta);
		ArgumentNullException.ThrowIfNull(faktura.Sprzedawca);

		var sprzedawca = ZnajdzLubUtworzKontrahenta(baza, faktura.Sprzedawca);
		PowiazKrajKontrahenta(baza, sprzedawca, faktura.Sprzedawca.Kraj?.KodISO2, faktura.NIPSprzedawcy, domyslniePL: true);
		faktura.SprzedawcaRef = sprzedawca;
		faktura.Sprzedawca = null;

		var nabywca = ZnajdzLubUtworzKontrahenta(baza, faktura.Nabywca);
		PowiazKrajKontrahenta(baza, nabywca, faktura.Nabywca.Kraj?.KodISO2, faktura.NIPNabywcy);
		faktura.NabywcaRef = nabywca;
		faktura.Nabywca = null;

		if (sprzedawca.CzyPodmiot) faktura.Rodzaj = faktura.ProceduraMarzy == ProceduraMarży.NieDotyczy
			? faktura.Rodzaj == RodzajFaktury.KorektaZakupu ? RodzajFaktury.KorektaSprzedaży : RodzajFaktury.Sprzedaż
			: faktura.Rodzaj == RodzajFaktury.KorektaZakupu ? RodzajFaktury.KorektaVatMarży : RodzajFaktury.VatMarża;

		if (String.IsNullOrEmpty(faktura.NIPNabywcy)) faktura.NIPNabywcy = nabywca.NIP;
		if (String.IsNullOrEmpty(faktura.NazwaNabywcy)) faktura.NazwaNabywcy = nabywca.PelnaNazwa;
		if (String.IsNullOrEmpty(faktura.DaneNabywcy)) faktura.DaneNabywcy = nabywca.AdresRejestrowy;

		if (faktura.FakturaKorygowana != null)
		{
			var fakturaKorygowana = String.IsNullOrEmpty(faktura.FakturaKorygowana.NumerKSeF) ? null : baza.Faktury.FirstOrDefault(f => f.NumerKSeF == faktura.FakturaKorygowana.NumerKSeF && f.Rodzaj != RodzajFaktury.Usunięta);
			fakturaKorygowana ??= String.IsNullOrEmpty(faktura.FakturaKorygowana.Numer) ? null : baza.Faktury.FirstOrDefault(f => f.Numer == faktura.FakturaKorygowana.Numer && f.Sprzedawca == faktura.Sprzedawca && f.Rodzaj != RodzajFaktury.Usunięta);
			if (fakturaKorygowana == null) faktura.UwagiPubliczne = $"Korekta do {faktura.FakturaKorygowana.Numer} z dnia {faktura.FakturaKorygowana.DataWystawienia:yyyy-MM-dd}\r\n{faktura.UwagiPubliczne}";
			else faktura.FakturaKorygowanaRef = fakturaKorygowana;
			faktura.FakturaKorygowana = null;
		}

		faktura.SposobPlatnosciRef = DopasujSposobPlatnosci(baza, faktura);

		var kodWaluty = Waluta.NormalizujKodISO(faktura.Waluta.Skrot);
		var waluta = baza.Waluty
			.AsEnumerable()
			.FirstOrDefault(waluta => Waluta.NormalizujKodISO(waluta.Skrot) == kodWaluty);
		if (waluta == null) baza.Zapisz(waluta = faktura.Waluta);
		faktura.WalutaRef = waluta;
		if (!waluta.CzyDomyslna && faktura.KursWaluty != 0 && faktura.KursWaluty != 1)
		{
			var kurs = NBPService.ZnajdzKursDlaWartosci(baza, waluta.Skrot, faktura.DataWystawienia, faktura.KursWaluty);
			faktura.DataKursu = kurs?.Data;
		}
		faktura.Waluta = null;

		foreach (var pozycja in faktura.Pozycje)
		{
			// Zawsze ustawione przez Zbuduj:
			ArgumentNullException.ThrowIfNull(pozycja.StawkaVat);
			ArgumentNullException.ThrowIfNull(pozycja.JednostkaMiary);
			var kodKSeFStawki = pozycja.StawkaVat.KodKSeFZnormalizowany;
			var stawkaVat = baza.StawkiVat
				.AsEnumerable()
				.FirstOrDefault(stawkaVat => stawkaVat.KodKSeFZnormalizowany == kodKSeFStawki);
			if (stawkaVat == null) stawkaVat = baza.StawkiVat.FirstOrDefault(stawkaVat => stawkaVat.Skrot == pozycja.StawkaVat.Skrot);
			if (stawkaVat == null) stawkaVat = baza.StawkiVat.FirstOrDefault(stawkaVat => stawkaVat.Wartosc == pozycja.StawkaVat.Wartosc);
			if (stawkaVat == null) baza.Zapisz(stawkaVat = pozycja.StawkaVat);
			pozycja.StawkaVatRef = stawkaVat;
			pozycja.StawkaVat = null;

			var jednostka = String.IsNullOrEmpty(pozycja.JednostkaMiary.Nazwa)
				? baza.JednostkiMiar.FirstOrDefault(jednostka => jednostka.CzyDomyslna)
				: baza.JednostkiMiar.FirstOrDefault(jednostka => jednostka.Nazwa.ToLower() == pozycja.JednostkaMiary.Nazwa.ToLower()
					|| jednostka.Skrot.ToLower() == pozycja.JednostkaMiary.Skrot.ToLower()
					|| jednostka.Skrot.ToLower() == pozycja.JednostkaMiary.Skrot.ToLower().TrimEnd('.'));
			if (jednostka == null) baza.Zapisz(jednostka = pozycja.JednostkaMiary);
			pozycja.JednostkaMiaryRef = jednostka;
			pozycja.JednostkaMiary = null;

			var towar = baza.Towary.FirstOrDefault(towar => towar.Nazwa.ToLower() == pozycja.Opis.ToLower());
			pozycja.TowarRef = towar;
			pozycja.Towar = null;
			if (towar != null) pozycja.JednostkaMiaryRef = towar.JednostkaMiaryRef;
		}
	}

	public static void PoprawPowiazaniaPoZapisie(Baza baza, DBFaktura faktura)
	{
		if (faktura.FakturaKorygowanaRef.IsNotNull)
		{
			var fakturaKorygowana = baza.Znajdz(faktura.FakturaKorygowanaRef);
			fakturaKorygowana.FakturaKorygujacaRef = faktura;
			baza.Zapisz(fakturaKorygowana);
		}
	}

	private static TKodKraju PobierzKodKraju(Kontrahent? kontrahent, string? numerIdentyfikacyjny, TKodKraju domyslny)
	{
		var kodISO2 = (kontrahent?.Kraj?.KodISO2 ?? "").Trim().ToUpperInvariant();
		if (!String.IsNullOrWhiteSpace(kodISO2) && Enum.TryParse<TKodKraju>(kodISO2, out var kodKrajuZModelu))
		{
			return kodKrajuZModelu;
		}

		var numer = OczyscNumerIdentyfikacyjny(numerIdentyfikacyjny);
		if (Regex.IsMatch(numer, @"^\w\w") && Enum.TryParse<TKodKraju>(numer[0..2], out var kodKrajuZNumeru))
		{
			return kodKrajuZNumeru;
		}

		return domyslny;
	}

	private static string NormalizujNumerRachunkuDoPorownan(string? numerRachunku)
	{
		return Regex.Replace((numerRachunku ?? "").Trim().ToUpperInvariant(), @"[^A-Z0-9]+", "");
	}

	private static string NormalizujNumerRachunkuDlaKSeF(string numerRachunku)
	{
		return Regex.Replace((numerRachunku ?? "").Trim().ToUpperInvariant(), @"[\s-]+", "");
	}

	private static RachunekBankowy? ZnajdzRachunekDoEksportu(DBFaktura dbFaktura)
	{
		var rachunki = dbFaktura.Sprzedawca?.RachunkiBankowe?
			.Where(rachunek => !String.IsNullOrWhiteSpace(rachunek.NumerRachunku) || !String.IsNullOrWhiteSpace(rachunek.NumerEksportowy))
			.OrderByDescending(rachunek => rachunek.CzyDomyslny)
			.ThenBy(rachunek => rachunek.Id)
			.ToList();
		if (rachunki == null || rachunki.Count == 0) return null;

		var numerZFaktury = NormalizujNumerRachunkuDoPorownan(dbFaktura.RachunekBankowy);
		if (!String.IsNullOrWhiteSpace(numerZFaktury))
		{
			var dopasowany = rachunki.FirstOrDefault(rachunek =>
				NormalizujNumerRachunkuDoPorownan(rachunek.NumerDoEksportu) == numerZFaktury
				|| NormalizujNumerRachunkuDoPorownan(rachunek.NumerRachunku) == numerZFaktury);
			if (dopasowany != null) return dopasowany;
		}

		if (dbFaktura.WalutaId.HasValue)
		{
			var rachunekDlaWaluty = rachunki.FirstOrDefault(rachunek => rachunek.WalutaId == dbFaktura.WalutaId && rachunek.CzyDomyslny)
				?? rachunki.FirstOrDefault(rachunek => rachunek.WalutaId == dbFaktura.WalutaId)
				?? rachunki.FirstOrDefault(rachunek => rachunek.WalutaId == null && rachunek.CzyDomyslny)
				?? rachunki.FirstOrDefault(rachunek => rachunek.WalutaId == null);
			if (rachunekDlaWaluty != null) return rachunekDlaWaluty;
		}

		return rachunki.FirstOrDefault(rachunek => rachunek.CzyDomyslny) ?? rachunki.First();
	}

	private static TRachunekBankowy? ZbudujRachunekBankowyKSeF(DBFaktura dbFaktura)
	{
		var rachunek = ZnajdzRachunekDoEksportu(dbFaktura);
		var numer = rachunek?.NumerDoEksportu;
		var nazwaBanku = rachunek?.NazwaBanku;
		var swift = rachunek?.Swift;

		if (String.IsNullOrWhiteSpace(numer))
		{
			numer = dbFaktura.RachunekBankowy;
			if (String.IsNullOrWhiteSpace(nazwaBanku)) nazwaBanku = dbFaktura.NazwaBanku;
		}

		if (String.IsNullOrWhiteSpace(numer)) return null;

		var ksefRachunek = new TRachunekBankowy
		{
			NrRB = NormalizujNumerRachunkuDlaKSeF(numer)
		};

		if (!String.IsNullOrWhiteSpace(nazwaBanku)) ksefRachunek.NazwaBanku = nazwaBanku;
		if (!String.IsNullOrWhiteSpace(swift)) ksefRachunek.SWIFT = swift.Trim().ToUpperInvariant();

		return ksefRachunek;
	}

	private static List<string> ZweryfikujBiznesowo(DBFaktura faktura)
	{
		var bledy = new List<string>();

		if (!faktura.CzySprzedaz) bledy.Add("Do KSeF można wysyłać tylko dokumenty sprzedaży.");
		if (String.IsNullOrWhiteSpace(faktura.Numer)) bledy.Add("Brak numeru faktury.");
		if (faktura.DataWystawienia == default) bledy.Add("Brak daty wystawienia.");
		if (faktura.DataSprzedazy == default) bledy.Add("Brak daty sprzedaży.");
		if (faktura.DataSprzedazy < new DateTime(2006, 1, 1)) bledy.Add("Data sprzedaży jest nieprawidłowa.");
		if (faktura.DataWystawienia < new DateTime(2006, 1, 1)) bledy.Add("Data wystawienia jest nieprawidłowa.");

		var nipSprzedawcy = OczyscNumerIdentyfikacyjny(faktura.NIPSprzedawcy);
		if (String.IsNullOrWhiteSpace(nipSprzedawcy))
		{
			bledy.Add("Brak NIP sprzedawcy.");
		}
		else if (!Regex.IsMatch(nipSprzedawcy, @"^\d{10}$"))
		{
			bledy.Add("NIP sprzedawcy musi zawierać dokładnie 10 cyfr, bez prefiksu kraju.");
		}

		if (String.IsNullOrWhiteSpace(faktura.NazwaSprzedawcy)) bledy.Add("Brak nazwy sprzedawcy.");
		if (String.IsNullOrWhiteSpace(faktura.DaneSprzedawcy)) bledy.Add("Brak adresu sprzedawcy.");
		if (String.IsNullOrWhiteSpace(faktura.NazwaNabywcy)) bledy.Add("Brak nazwy nabywcy.");
		if (String.IsNullOrWhiteSpace(faktura.DaneNabywcy)) bledy.Add("Brak adresu nabywcy.");

		var nipNabywcy = OczyscNumerIdentyfikacyjny(faktura.NIPNabywcy);
		if (!String.IsNullOrWhiteSpace(nipNabywcy)
			&& !Regex.IsMatch(nipNabywcy, @"^\d{10}$")
			&& !Regex.IsMatch(nipNabywcy, @"^PL\d{10}$")
			&& !Regex.IsMatch(nipNabywcy, @"^[A-Z]{2}[A-Z0-9]{2,32}$")
			&& !Regex.IsMatch(nipNabywcy, @"^[A-Z0-9]{2,32}$"))
		{
			bledy.Add("Identyfikator nabywcy ma nieobsługiwany format. Użyj NIP, VAT UE albo innego numeru bez opisów i zbędnych znaków.");
		}

		if (faktura.Pozycje == null || faktura.Pozycje.Count == 0)
		{
			bledy.Add("Faktura nie ma żadnych pozycji.");
		}
		else
		{
			foreach (var pozycja in faktura.Pozycje.OrderBy(p => p.LP))
			{
				var prefix = $"Pozycja {pozycja.LP}";
				if (String.IsNullOrWhiteSpace(pozycja.Opis)) bledy.Add($"{prefix}: brak opisu towaru lub usługi.");
				if (pozycja.Ilosc == 0) bledy.Add($"{prefix}: ilość nie może być równa 0.");
				if (pozycja.StawkaVat == null && pozycja.StawkaVatRef.IsNull) bledy.Add($"{prefix}: brak stawki VAT.");
				if (pozycja.JednostkaMiary == null && pozycja.JednostkaMiaryRef.IsNull) bledy.Add($"{prefix}: brak jednostki miary.");
			}
		}

		if (faktura.Waluta == null && faktura.WalutaRef.IsNull) bledy.Add("Brak waluty faktury.");
		else
		{
			var skrotWaluty = Waluta.NormalizujKodISO(faktura.Waluta?.Skrot);
			if (!String.IsNullOrWhiteSpace(skrotWaluty) && !Waluta.CzyPolskiZloty(skrotWaluty))
			{
				if (faktura.KursWaluty <= 0) bledy.Add("Dla waluty obcej kurs waluty musi być większy od zera.");
				if (!faktura.DataKursu.HasValue) bledy.Add("Dla waluty obcej brakuje daty kursu.");
			}
		}

		if (faktura.PozostaloDoZaplaty > 0 && faktura.TerminPlatnosci == default) bledy.Add("Brak terminu płatności.");
		if (!String.IsNullOrWhiteSpace(faktura.RachunekBankowy) && String.IsNullOrWhiteSpace(faktura.OpisSposobuPlatnosci))
		{
			bledy.Add("Uzupełnij sposób płatności dla faktury z rachunkiem bankowym.");
		}
		var rachunekDoEksportu = ZnajdzRachunekDoEksportu(faktura);
		if (rachunekDoEksportu != null
			&& !String.IsNullOrWhiteSpace(rachunekDoEksportu.KrajKodISO2)
			&& !String.Equals(rachunekDoEksportu.KrajKodISO2, "PL", StringComparison.OrdinalIgnoreCase)
			&& String.IsNullOrWhiteSpace(rachunekDoEksportu.Swift))
		{
			bledy.Add($"Rachunek \"{rachunekDoEksportu.NazwaFmt}\" ma kraj inny niż PL, więc przed wysyłką do KSeF uzupełnij SWIFT.");
		}

		return bledy;
	}

	private static void ZweryfikujKSeF(KSEFFaktura faktura, string xml, string numerFaktury, IEnumerable<string>? dodatkoweBledy = null)
	{
		var bledy = dodatkoweBledy?.Where(b => !String.IsNullOrWhiteSpace(b)).ToList() ?? new List<string>();
		WalidujXml(xml, bledy);
		WalidujRachunkiBankowe(faktura, bledy);
		if (!bledy.Any()) return;

		var unikalneBledy = bledy
			.Where(b => !String.IsNullOrWhiteSpace(b))
			.Distinct()
			.ToList();
		if (!unikalneBledy.Any()) return;

		throw new WalidacjaKSeFException(
			$"Nie można wysłać faktury {numerFaktury} do KSeF, bo lokalna walidacja wykryła błędy danych:\n"
			+ String.Join("\n", unikalneBledy.Select(b => $"• {b}"))
			+ "\n\nPopraw dane faktury i spróbuj ponownie.");
	}

	private static void WalidujXml(string xml, List<string> bledy)
	{
		if (String.IsNullOrWhiteSpace(xml))
		{
			bledy.Add("Nie udało się wygenerować XML faktury.");
			return;
		}

		try
		{
			using var stringReader = new StringReader(xml);
			using var reader = XmlReader.Create(stringReader, new XmlReaderSettings
			{
				DtdProcessing = DtdProcessing.Prohibit
			});
			while (reader.Read()) { }
		}
		catch (Exception exc)
		{
			bledy.Add($"Wygenerowany XML faktury jest niepoprawny: {exc.Message}");
		}
	}

	private static void WalidujRachunkiBankowe(KSEFFaktura faktura, List<string> bledy)
	{
		var rachunki = faktura.Fa?.Platnosc?.RachunekBankowy;
		if (rachunki == null || rachunki.Count == 0) return;

		for (var i = 0; i < rachunki.Count; i++)
		{
			var rachunek = rachunki[i];
			var numer = rachunek.NrRB ?? "";
			var sciezka = $"KSEFFaktura.Fa.Platnosc.RachunekBankowy[{i}].NrRB";
			if (String.IsNullOrWhiteSpace(numer))
			{
				bledy.Add($"{sciezka}: numer rachunku jest pusty.");
				continue;
			}

			var numerLower = numer.ToLowerInvariant();
			if (numerLower.Contains("sort") || numerLower.Contains("account") || numerLower.Contains("number"))
			{
				bledy.Add($"{sciezka}: numer rachunku zawiera opis typu \"sort code/account number\" zamiast samego numeru lub IBAN.");
			}

			if (!Regex.IsMatch(numer, @"^[0-9A-Z]{10,34}$"))
			{
				bledy.Add($"{sciezka}: numer rachunku do KSeF może zawierać tylko litery A-Z i cyfry 0-9 oraz mieć długość od 10 do 34 znaków.");
			}
		}
	}

	private static string FormatujPole(string nazwaPola)
	{
		return nazwaPola switch
		{
			nameof(TRachunekBankowy.NrRB) => "numer rachunku",
			nameof(TRachunekBankowy.SWIFT) => "SWIFT",
			_ => nazwaPola,
		};
	}

	private static string OczyscNumerIdentyfikacyjny(string? numer)
	{
		return Regex.Replace((numer ?? "").Trim().ToUpperInvariant(), @"[\s-]+", "");
	}

	private static string PobierzKodKSeFDlaPozycji(DBFaktura faktura, StawkaVat stawkaVat)
	{
		var kod = stawkaVat.KodKSeFZnormalizowany;
		if (faktura.CzyWDT && stawkaVat.CzyZeroKraj) return "0 WDT";
		return kod;
	}

	private static TStawkaPodatku MapujKodKSeFNaStawke(string kodKSeF)
	{
		return StawkaVat.NormalizujKodKSeF(kodKSeF) switch
		{
			"23" => TStawkaPodatku.Item23,
			"22" => TStawkaPodatku.Item22,
			"8" => TStawkaPodatku.Item8,
			"7" => TStawkaPodatku.Item7,
			"5" => TStawkaPodatku.Item5,
			"4" => TStawkaPodatku.Item4,
			"3" => TStawkaPodatku.Item3,
			"0 KR" => TStawkaPodatku.Item0_KR,
			"0 WDT" => TStawkaPodatku.Item0_WDT,
			"0 EX" => TStawkaPodatku.Item0_EX,
			"ZW" => TStawkaPodatku.zw,
			"OO" => TStawkaPodatku.oo,
			"NP I" => TStawkaPodatku.np_I,
			"NP II" => TStawkaPodatku.np_II,
			_ => TStawkaPodatku.Item23,
		};
	}

	private static string MapujStawkeNaKodKSeF(TStawkaPodatku stawkaPodatku)
	{
		return stawkaPodatku switch
		{
			TStawkaPodatku.Item23 => "23",
			TStawkaPodatku.Item22 => "22",
			TStawkaPodatku.Item8 => "8",
			TStawkaPodatku.Item7 => "7",
			TStawkaPodatku.Item5 => "5",
			TStawkaPodatku.Item4 => "4",
			TStawkaPodatku.Item3 => "3",
			TStawkaPodatku.Item0_KR => "0 KR",
			TStawkaPodatku.Item0_WDT => "0 WDT",
			TStawkaPodatku.Item0_EX => "0 EX",
			TStawkaPodatku.zw => "ZW",
			TStawkaPodatku.oo => "OO",
			TStawkaPodatku.np_I => "NP I",
			TStawkaPodatku.np_II => "NP II",
			_ => "23",
		};
	}

	private static void DodajPodsumowanieStawki(FakturaFa faktura, string kodKSeF, decimal wartoscNetto, decimal wartoscVat)
	{
		switch (StawkaVat.NormalizujKodKSeF(kodKSeF))
		{
			case "23":
			case "22":
				faktura.P_13_1 ??= 0;
				faktura.P_14_1 ??= 0;
				faktura.P_13_1 += wartoscNetto;
				faktura.P_14_1 += wartoscVat;
				break;
			case "8":
			case "7":
				faktura.P_13_2 ??= 0;
				faktura.P_14_2 ??= 0;
				faktura.P_13_2 += wartoscNetto;
				faktura.P_14_2 += wartoscVat;
				break;
			case "5":
			case "3":
				faktura.P_13_3 ??= 0;
				faktura.P_14_3 ??= 0;
				faktura.P_13_3 += wartoscNetto;
				faktura.P_14_3 += wartoscVat;
				break;
			case "4":
				faktura.P_13_4 ??= 0;
				faktura.P_14_4 ??= 0;
				faktura.P_13_4 += wartoscNetto;
				faktura.P_14_4 += wartoscVat;
				break;
			case "0 KR":
				faktura.P_13_6_1 ??= 0;
				faktura.P_13_6_1 += wartoscNetto;
				break;
			case "0 WDT":
				faktura.P_13_6_2 ??= 0;
				faktura.P_13_6_2 += wartoscNetto;
				break;
			case "0 EX":
				faktura.P_13_6_3 ??= 0;
				faktura.P_13_6_3 += wartoscNetto;
				break;
			case "ZW":
				faktura.P_13_7 ??= 0;
				faktura.P_13_7 += wartoscNetto;
				break;
			case "NP I":
				faktura.P_13_8 ??= 0;
				faktura.P_13_8 += wartoscNetto;
				break;
			case "NP II":
				faktura.P_13_9 ??= 0;
				faktura.P_13_9 += wartoscNetto;
				break;
			case "OO":
				faktura.P_13_10 ??= 0;
				faktura.P_13_10 += wartoscNetto;
				break;
		}
	}

	private static string SerializujDoXml(KSEFFaktura faktura)
	{
		OczyscTekstyDlaXml(faktura);
		var xo = new XmlAttributeOverrides();
		var xs = new XmlSerializer(typeof(KSEFFaktura), xo);
		var xml = new StringBuilder();
		using var xw = XmlWriter.Create(xml, new XmlWriterSettings() { OmitXmlDeclaration = true, Indent = true });
		var nss = new XmlSerializerNamespaces();
		xs.Serialize(xw, faktura, nss);
		return xml.ToString();
	}

	private static void OczyscTekstyDlaXml(object? obiekt)
	{
		if (obiekt == null) return;

		var typ = obiekt.GetType();
		if (CzyTypProsty(typ)) return;

		if (obiekt is IEnumerable kolekcja && obiekt is not string)
		{
			foreach (var element in kolekcja) OczyscTekstyDlaXml(element);
			return;
		}

		foreach (var wlasciwosc in typ.GetProperties(BindingFlags.Public | BindingFlags.Instance))
		{
			if (!wlasciwosc.CanRead || wlasciwosc.GetIndexParameters().Length > 0) continue;

			if (wlasciwosc.PropertyType == typeof(string))
			{
				if (!wlasciwosc.CanWrite) continue;
				var wartosc = (string?)wlasciwosc.GetValue(obiekt);
				var oczyszczona = OczyscTekstDlaXml(wartosc);
				if (!String.Equals(wartosc, oczyszczona, StringComparison.Ordinal)) wlasciwosc.SetValue(obiekt, oczyszczona);
				continue;
			}

			if (CzyTypProsty(wlasciwosc.PropertyType)) continue;
			OczyscTekstyDlaXml(wlasciwosc.GetValue(obiekt));
		}
	}

	private static string? OczyscTekstDlaXml(string? tekst) => SanitizacjaTekstu.UsunNiedozwoloneZnakiXml(tekst);

	private static bool CzyTypProsty(Type typ)
	{
		typ = Nullable.GetUnderlyingType(typ) ?? typ;
		return typ.IsPrimitive
			|| typ.IsEnum
			|| typ == typeof(string)
			|| typ == typeof(decimal)
			|| typ == typeof(DateTime)
			|| typ == typeof(DateTimeOffset)
			|| typ == typeof(TimeSpan)
			|| typ == typeof(Guid);
	}
}
