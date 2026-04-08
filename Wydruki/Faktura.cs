using Microsoft.EntityFrameworkCore;
using Microsoft.Reporting.WinForms;
using ProFak.DB;
using System.Drawing.Imaging;
using ZXing;
using ZXing.Windows.Compatibility;

namespace ProFak.Wydruki;

public class Faktura : Wydruk
{
	private readonly List<FakturaDTO> dane;
	private readonly string szablon;

	public Faktura(Baza baza, IEnumerable<Ref<DB.Faktura>> fakturyRefs, bool duplikat = false, string szablon = "Faktura")
	{
		this.szablon = szablon;
		var dwujezyczny = String.Equals(this.szablon, "FakturaEN", StringComparison.Ordinal);
		var tylkoEn = String.Equals(this.szablon, "FakturaOnlyEN", StringComparison.Ordinal);
		string Tekst(string pl, string plEn, string en) => tylkoEn ? en : dwujezyczny ? plEn : pl;
		dane = new List<FakturaDTO>();
		foreach (var fakturaRef in fakturyRefs)
		{
			var faktura = baza.Znajdz(fakturaRef);
			var pozycje = baza.PozycjeFaktur
				.Where(pozycja => pozycja.FakturaId == faktura.Id)
				.Include(pozycja => pozycja.StawkaVat)
				.Include(pozycja => pozycja.JednostkaMiary)
				.Include(pozycja => pozycja.Towar).ThenInclude(towar => towar!.JednostkaMiary)
				.OrderBy(pozycja => pozycja.LP)
				.ThenBy(pozycja => pozycja.CzyPrzedKorekta)
				.ToList();
			var odbiorca = baza.DodatkowePodmioty
				.FirstOrDefault(dodatkowyPodmiot => dodatkowyPodmiot.FakturaId == faktura.Id && dodatkowyPodmiot.Rodzaj == RodzajDodatkowegoPodmiotu.Odbiorca);
			var wplaty = baza.Wplaty.Where(wplata => wplata.FakturaId == faktura.Id).ToList();
			var zaplacono = wplaty.Sum(wplata => wplata.Kwota);
			var dozaplaty = faktura.RazemBrutto - zaplacono;
			var waluta = baza.ZnajdzLubNull(faktura.WalutaRef);
			var walutaVAT = baza.Waluty.FirstOrDefault(waluta => waluta.CzyDomyslna);
			var walutaSkrot = waluta?.Skrot ?? "zł";
			var walutaVATSkrot = walutaVAT?.Skrot ?? walutaSkrot;
			var jestvat = pozycje.Any(e => e.StawkaVat != null && !String.Equals(e.StawkaVat.Skrot, "ZW", StringComparison.CurrentCultureIgnoreCase)) && faktura.ProceduraMarzy == ProceduraMarży.NieDotyczy;
			var jestrabat = pozycje.Any(e => e.RabatProcent > 0 || e.RabatCena > 0 || e.RabatWartosc > 0);

			var fakturaDTO = new FakturaDTO();
			if (faktura.Rodzaj == RodzajFaktury.Sprzedaż) fakturaDTO.Rodzaj = Tekst(jestvat ? "Faktura VAT" : "Faktura", jestvat ? "Faktura VAT (VAT Invoice)" : "Faktura (Invoice)", jestvat ? "VAT Invoice" : "Invoice");
			else if (faktura.Rodzaj == RodzajFaktury.Rachunek) fakturaDTO.Rodzaj = Tekst("Rachunek", "Rachunek (Bill)", "Bill");
			else if (faktura.Rodzaj == RodzajFaktury.Proforma) fakturaDTO.Rodzaj = Tekst("Faktura pro forma", "Faktura pro forma (Pro forma invoice)", "Pro forma invoice");
			else if (faktura.Rodzaj == RodzajFaktury.KorektaSprzedaży) fakturaDTO.Rodzaj = Tekst(jestvat ? "Korekta faktury VAT" : "Korekta faktury", jestvat ? "Korekta faktury VAT (VAT Invoice correction)" : "Korekta faktury (Invoice correction)", jestvat ? "VAT Invoice correction" : "Invoice correction");
			else if (faktura.Rodzaj == RodzajFaktury.KorektaRachunku) fakturaDTO.Rodzaj = Tekst("Korekta rachunku", "Korekta rachunku (Bill correction)", "Bill correction");
			else if (faktura.Rodzaj == RodzajFaktury.DowódWewnętrzny) fakturaDTO.Rodzaj = Tekst("Dowód wewnętrzny", "Dowód wewnętrzny (Internal document)", "Internal document");
			else if (faktura.Rodzaj == RodzajFaktury.VatMarża) fakturaDTO.Rodzaj = Tekst("Faktura VAT marża", "Faktura VAT marża (Margin VAT invoice)", "Margin VAT invoice");
			else if (faktura.Rodzaj == RodzajFaktury.KorektaVatMarży) fakturaDTO.Rodzaj = Tekst("Korekta faktury VAT marża", "Korekta faktury VAT marża (Margin VAT invoice correction)", "Margin VAT invoice correction");
			else fakturaDTO.Rodzaj = faktura.Rodzaj.ToString();

			fakturaDTO.Numer = faktura.Numer;
			fakturaDTO.JestVAT = jestvat;
			fakturaDTO.JestRabat = jestrabat;
			fakturaDTO.Waluta = walutaSkrot;
			fakturaDTO.WalutaVAT = walutaVATSkrot;
			fakturaDTO.KursWaluty = faktura.KursWaluty;
			if (faktura.ProceduraMarzy != ProceduraMarży.NieDotyczy) fakturaDTO.ProceduraMarzy = Rekord.Format(faktura.ProceduraMarzy);

			if (faktura.FakturaPierwotnaRef.IsNotNull)
			{
				var fakturaBazowa = baza.Znajdz(faktura.FakturaPierwotnaRef);
				if (fakturaBazowa.Rodzaj == RodzajFaktury.Proforma) fakturaDTO.Korekta = Tekst("do faktury pro forma <b>", "do faktury pro forma / related to pro forma invoice <b>", "<b>related to pro forma invoice</b> ") + fakturaBazowa.Numer + "</b>";
				else if (fakturaBazowa.Rodzaj == RodzajFaktury.VatMarża) fakturaDTO.Korekta = Tekst("<b>do faktury VAT marża</b> ", "<b>do faktury VAT marża / related to margin VAT invoice</b> ", "<b>related to margin VAT invoice</b> ") + fakturaBazowa.Numer + "<br/>" + Tekst("<b>z dnia</b> ", "<b>z dnia / issued on</b> ", "<b>issued on</b> ") + fakturaBazowa.DataWystawienia.ToString(UI.Wyglad.FormatDaty) + "<br/>";
				else fakturaDTO.Korekta = Tekst(jestvat ? "<b>do faktury VAT</b> " : "<b>do faktury</b> ", jestvat ? "<b>do faktury VAT / related to VAT invoice</b> " : "<b>do faktury / related to invoice</b> ", jestvat ? "<b>related to VAT invoice</b> " : "<b>related to invoice</b> ") + fakturaBazowa.Numer + "<br/>" + Tekst("<b>z dnia</b> ", "<b>z dnia / issued on</b> ", "<b>issued on</b> ") + fakturaBazowa.DataWystawienia.ToString(UI.Wyglad.FormatDaty) + "<br/>";
			}

			if (duplikat)
			{
				if (!String.IsNullOrEmpty(fakturaDTO.Korekta)) fakturaDTO.Korekta += "<br/>";
				fakturaDTO.Korekta += Tekst("<b>Duplikat z dnia</b> ", "<b>Duplikat z dnia / Duplicate issued on</b> ", "<b>Duplicate issued on</b> ") + DateTime.Now.ToString(UI.Wyglad.FormatDaty);
			}

			fakturaDTO.DataWystawienia = faktura.DataWystawienia.ToString(UI.Wyglad.FormatDaty);
			fakturaDTO.DataSprzedazy = faktura.DataSprzedazy.ToString(UI.Wyglad.FormatDaty);

			fakturaDTO.NazwaNabywcy = faktura.NazwaNabywcy;
			fakturaDTO.AdresNabywcy = faktura.DaneNabywcy;
			fakturaDTO.NIPNabywcy = faktura.NIPNabywcy;

			fakturaDTO.NazwaSprzedawcy = faktura.NazwaSprzedawcy;
			fakturaDTO.AdresSprzedawcy = faktura.DaneSprzedawcy;
			fakturaDTO.NIPSprzedawcy = faktura.NIPSprzedawcy;

			if (odbiorca != null)
			{
				fakturaDTO.DaneOdbiorcy = odbiorca.Nazwa;
				if (!String.IsNullOrEmpty(odbiorca.Adres)) fakturaDTO.DaneOdbiorcy += "<br/>" + odbiorca.Adres.Replace("\n", "<br/>");
				if (!String.IsNullOrEmpty(odbiorca.NIP)) fakturaDTO.DaneOdbiorcy += "<br/><b>" + Tekst("NIP:", "NIP / Tax ID:", "Tax ID:") + "</b> " + odbiorca.NIP;
				if (!String.IsNullOrEmpty(odbiorca.VatUE)) fakturaDTO.DaneOdbiorcy += "<br/><b>" + Tekst("Nr VAT UE:", "Nr VAT UE / EU VAT No.:", "EU VAT No.:") + "</b> " + odbiorca.VatUE;
			}

			if (dozaplaty < 0)
			{
				fakturaDTO.Slownie = tylkoEn ? SlownieEN.Slownie(-dozaplaty, walutaSkrot) : dwujezyczny ? $"{SlowniePL.Slownie(-dozaplaty, walutaSkrot)} / {SlownieEN.Slownie(-dozaplaty, walutaSkrot)}" : SlowniePL.Slownie(-dozaplaty, walutaSkrot);
				fakturaDTO.TerminPlatnosci = "";
				fakturaDTO.FormaPlatnosci = "";
				fakturaDTO.DoZwrotu = (-dozaplaty).ToString(UI.Wyglad.FormatKwoty) + " " + walutaSkrot;
				fakturaDTO.DoZaplaty = "";
				fakturaDTO.NumerRachunku = "";
				fakturaDTO.NazwaBanku = "";
			}
			else
			{
				fakturaDTO.TerminPlatnosci = faktura.TerminPlatnosci.ToString(UI.Wyglad.FormatDaty);
				fakturaDTO.FormaPlatnosci = faktura.OpisSposobuPlatnosci;
				fakturaDTO.Slownie = tylkoEn ? SlownieEN.Slownie(dozaplaty, walutaSkrot) : dwujezyczny ? $"{SlowniePL.Slownie(dozaplaty, walutaSkrot)} / {SlownieEN.Slownie(dozaplaty, walutaSkrot)}" : SlowniePL.Slownie(dozaplaty, walutaSkrot);
				fakturaDTO.DoZwrotu = "";
				fakturaDTO.DoZaplaty = dozaplaty.ToString(UI.Wyglad.FormatKwoty) + " " + walutaSkrot;
				fakturaDTO.NumerRachunku = faktura.RachunekBankowy;
				fakturaDTO.NazwaBanku = faktura.NazwaBanku;
			}

			fakturaDTO.Rozliczenia = "";
			foreach (var wplata in wplaty.OrderBy(e => e.Data).ThenBy(e => e.Id))
			{
				if (!String.IsNullOrEmpty(fakturaDTO.Rozliczenia)) fakturaDTO.Rozliczenia += "<br/>";
				if (wplata.CzyRozliczenie) fakturaDTO.Rozliczenia += "<b>" + wplata.Uwagi + ":</b> " + wplata.Kwota.ToString(UI.Wyglad.FormatKwoty) + " " + walutaSkrot;
				else fakturaDTO.Rozliczenia += Tekst("<b>Zapłacono " + wplata.Data.ToString(UI.Wyglad.FormatDaty) + ":</b> ", "<b>Zapłacono " + wplata.Data.ToString(UI.Wyglad.FormatDaty) + " / Paid on " + wplata.Data.ToString(UI.Wyglad.FormatDaty) + ":</b> ", "<b>Paid on " + wplata.Data.ToString(UI.Wyglad.FormatDaty) + ":</b> ") + wplata.Kwota.ToString(UI.Wyglad.FormatKwoty) + " " + walutaSkrot;
			}

			fakturaDTO.NumerKSeF = faktura.NumerKSeF;
			fakturaDTO.Uwagi = faktura.UwagiPubliczne;
			fakturaDTO.KodKSeF = "";

			if (!String.IsNullOrEmpty(faktura.URLKSeF))
			{
				var writer = new BarcodeWriter();
				writer.Options.Margin = 0;
				writer.Options.NoPadding = true;
				writer.Options.Width = 500;
				writer.Options.Height = 500;
				writer.Format = BarcodeFormat.QR_CODE;
				var qrKSeF = writer.WriteAsBitmap(faktura.URLKSeF);
				var ms = new MemoryStream();
				qrKSeF.Save(ms, ImageFormat.Png);
				fakturaDTO.KodKSeF = Convert.ToBase64String(ms.ToArray());
			}

			fakturaDTO.OpisPozycji = "";

			dane.Add(fakturaDTO);

			foreach (var pozycja in pozycje)
			{
				var pozycjaDTO = new FakturaDTO();
				pozycjaDTO.LP = pozycja.LP.ToString();
				pozycjaDTO.Numer = faktura.Numer; // musi tu być - po tym jest grupowanie stron
				pozycjaDTO.JestVAT = jestvat;
				pozycjaDTO.JestRabat = jestrabat;

				if (faktura.Rodzaj == RodzajFaktury.KorektaSprzedaży || faktura.Rodzaj == RodzajFaktury.KorektaVatMarży)
					pozycjaDTO.NaglowekPozycji = Tekst(pozycja.CzyPrzedKorekta ? "Przed korektą" : "Po korekcie", pozycja.CzyPrzedKorekta ? "Przed korektą (Before correction)" : "Po korekcie (After correction)", pozycja.CzyPrzedKorekta ? "Before correction" : "After correction");
				else
					pozycjaDTO.NaglowekPozycji = "";

				var jm = pozycja.JednostkaMiary ?? pozycja.Towar?.JednostkaMiary;

				pozycjaDTO.OpisPozycji = pozycja.Opis;
				pozycjaDTO.CenaNetto = pozycja.Cena;
				pozycjaDTO.Ilosc = Math.Abs(pozycja.Ilosc / 1.000000000000m) + " " + jm?.Skrot;
				pozycjaDTO.JM = jm?.Skrot;
				pozycjaDTO.WartoscNetto = pozycja.WartoscNetto;
				pozycjaDTO.WartoscVat = (pozycja.WartoscVat * faktura.KursWaluty).Zaokragl();
				pozycjaDTO.WartoscBrutto = pozycja.WartoscBrutto;
				pozycjaDTO.StawkaVAT = pozycja.StawkaVat?.Skrot ?? "-";
				pozycjaDTO.Rabat = pozycja.RabatFmt.Replace(", ", "\n") + (pozycja.RabatWartosc > 0 || pozycja.RabatCena > 0 ? " " + walutaSkrot : "");
				pozycjaDTO.RabatRazem = pozycja.RabatRazem;
				pozycjaDTO.Waluta = walutaSkrot;
				pozycjaDTO.WalutaVAT = walutaVATSkrot;

				dane.Add(pozycjaDTO);
			}
		}
	}

	public override void Przygotuj(LocalReport report)
	{
		using var rdlc = WczytajSzablon(szablon);
		var prefiksSubraportu = szablon switch
		{
			"FakturaEN" => "FakturaEN",
			"FakturaOnlyEN" => "FakturaOnlyEN",
			_ => "Faktura"
		};
		report.DisplayName = String.Join(", ", dane.Select(e => e.Numer).Distinct().Order());
		report.LoadReportDefinition(rdlc);
		report.LoadSubreportDefinition("PozycjeVatRabat", WczytajSzablon(prefiksSubraportu + "PozycjeVatRabat"));
		report.LoadSubreportDefinition("PozycjeVat", WczytajSzablon(prefiksSubraportu + "PozycjeVat"));
		report.LoadSubreportDefinition("PozycjeRabat", WczytajSzablon(prefiksSubraportu + "PozycjeRabat"));
		report.LoadSubreportDefinition("Pozycje", WczytajSzablon(prefiksSubraportu + "Pozycje"));
		report.SubreportProcessing += SubreportProcessing;
		report.DataSources.Add(new ReportDataSource("DSFaktury", dane));

		void SubreportProcessing(object? sender, SubreportProcessingEventArgs e)
		{
			e.DataSources.Add(report.DataSources[0]);
		}
	}
}
