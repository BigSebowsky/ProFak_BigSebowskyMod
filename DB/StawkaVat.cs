namespace ProFak.DB;

public class StawkaVat : Rekord<StawkaVat>
{
	private static readonly HashSet<string> KodyLiczbowe = ["23", "22", "8", "7", "5", "4", "3"];

	public string Skrot { get; set; } = "";
	public string KodKSeF { get; set; } = "";
	public decimal Wartosc { get; set; }
	public bool CzyDomyslna { get; set; }

	public string CzyDomyslnaFmt => CzyDomyslna ? "Tak" : "Nie";
	public string KodKSeFFmt => KodKSeFZnormalizowany;
	public string SkrotFmt => String.IsNullOrWhiteSpace(Skrot) ? DomyslnySkrotDlaKoduKSeF(KodKSeFZnormalizowany) : Skrot;
	public string KodKSeFZnormalizowany => NormalizujKodKSeF(KodKSeF, Skrot, Wartosc);
	public bool CzyZwolniona => KodKSeFZnormalizowany == "ZW";
	public bool CzyOdwrotneObciazenie => KodKSeFZnormalizowany == "OO";
	public bool CzyNiePodlegaI => KodKSeFZnormalizowany == "NP I";
	public bool CzyNiePodlegaII => KodKSeFZnormalizowany == "NP II";
	public bool CzyNiePodlega => CzyNiePodlegaI || CzyNiePodlegaII;
	public bool CzyZeroKraj => KodKSeFZnormalizowany == "0 KR";
	public bool CzyZeroWDT => KodKSeFZnormalizowany == "0 WDT";
	public bool CzyZeroEksport => KodKSeFZnormalizowany == "0 EX";
	public bool CzyZero => CzyZeroKraj || CzyZeroWDT || CzyZeroEksport;

	public override bool CzyPasuje(string fraza)
		=> base.CzyPasuje(fraza)
		|| CzyPasuje(Skrot, fraza)
		|| CzyPasuje(KodKSeF, fraza)
		|| CzyPasuje(Wartosc, fraza)
		|| CzyPasuje(CzyDomyslna ? "Domyślna" : "", fraza);

	public void Normalizuj()
	{
		KodKSeF = KodKSeFZnormalizowany;
		Skrot = DomyslnySkrotDlaKoduKSeF(KodKSeF);
		Wartosc = DomyslnaWartoscDlaKoduKSeF(KodKSeF, Wartosc);
	}

	public static string NormalizujKodKSeF(string? kodKSeF, string? skrot = null, decimal? wartosc = null)
	{
		var tekst = OczyscKod(kodKSeF);
		if (String.IsNullOrWhiteSpace(tekst)) tekst = OczyscKod(skrot);

		if (!String.IsNullOrWhiteSpace(tekst))
		{
			if (KodyLiczbowe.Contains(tekst)) return tekst;
			if (tekst is "23%" or "22%" or "8%" or "7%" or "5%" or "4%" or "3%") return tekst.TrimEnd('%');
			if (tekst is "0" or "0%" or "0 KR" or "0 KRAJ" or "0 KRAJOWA" or "0 KRAJOWY") return "0 KR";
			if (tekst.Contains("WDT")) return "0 WDT";
			if (tekst.Contains("EX") || tekst.Contains("EXPORT") || tekst.Contains("EKSPORT")) return "0 EX";
			if (tekst is "ZW" || tekst.Contains("ZWOL")) return "ZW";
			if (tekst is "OO" || tekst.Contains("ODWROT")) return "OO";
			if (tekst is "NP II" or "NPII" or "NP2" || tekst.Contains("ART. 100") || tekst.Contains("ART 100")) return "NP II";
			if (tekst is "NP" or "NP I" or "NPI" or "NP1" || tekst.Contains("NIE PODLEGA") || tekst.Contains("POZA TERYTORIUM")) return "NP I";
		}

		if (wartosc.HasValue)
		{
			var wartoscZaokraglona = Decimal.ToInt32(Decimal.Round(wartosc.Value, 0, MidpointRounding.AwayFromZero));
			if (KodyLiczbowe.Contains(wartoscZaokraglona.ToString())) return wartoscZaokraglona.ToString();
			if (wartoscZaokraglona == 0) return "0 KR";
		}

		return "23";
	}

	public static string DomyslnySkrotDlaKoduKSeF(string? kodKSeF)
	{
		return NormalizujKodKSeF(kodKSeF) switch
		{
			"23" => "23%",
			"22" => "22%",
			"8" => "8%",
			"7" => "7%",
			"5" => "5%",
			"4" => "4%",
			"3" => "3%",
			"0 KR" => "0% kraj",
			"0 WDT" => "0% WDT",
			"0 EX" => "0% eksport",
			"ZW" => "ZW",
			"OO" => "OO",
			"NP I" => "NP I",
			"NP II" => "NP II",
			_ => "23%",
		};
	}

	public static decimal DomyslnaWartoscDlaKoduKSeF(string? kodKSeF, decimal wartoscDomyslna = 0)
	{
		return NormalizujKodKSeF(kodKSeF) switch
		{
			"23" => 23m,
			"22" => 22m,
			"8" => 8m,
			"7" => 7m,
			"5" => 5m,
			"4" => 4m,
			"3" => 3m,
			"0 KR" => 0m,
			"0 WDT" => 0m,
			"0 EX" => 0m,
			"ZW" => 0m,
			"OO" => 0m,
			"NP I" => 0m,
			"NP II" => 0m,
			_ => wartoscDomyslna,
		};
	}

	private static string OczyscKod(string? tekst)
	{
		if (String.IsNullOrWhiteSpace(tekst)) return "";
		var wynik = tekst.Trim().ToUpperInvariant();
		wynik = wynik.Replace("Ą", "A").Replace("Ć", "C").Replace("Ę", "E").Replace("Ł", "L").Replace("Ń", "N").Replace("Ó", "O").Replace("Ś", "S").Replace("Ż", "Z").Replace("Ź", "Z");
		wynik = wynik.Replace("_", " ");
		while (wynik.Contains("  ")) wynik = wynik.Replace("  ", " ");
		return wynik;
	}
}
