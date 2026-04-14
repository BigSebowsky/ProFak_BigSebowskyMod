namespace ProFak.DB;

public class Waluta : Rekord<Waluta>
{
	public string Skrot { get; set; } = "";
	public string Nazwa { get; set; } = "";
	public bool CzyDomyslna { get; set; }
	public string KodISO => NormalizujKodISO(Skrot);
	public string KodISOFmt => KodISO;
	public bool CzyPLN => CzyPolskiZloty(KodISO);

	public string CzyDomyslnaFmt => CzyDomyslna ? "Tak" : "Nie";

	public override bool CzyPasuje(string fraza)
		=> base.CzyPasuje(fraza)
		|| CzyPasuje(KodISO, fraza)
		|| CzyPasuje(Skrot, fraza)
		|| CzyPasuje(Nazwa, fraza)
		|| CzyPasuje(CzyDomyslna ? "Domyślna" : "", fraza);

	public void Normalizuj()
	{
		Skrot = NormalizujKodISO(Skrot);
	}

	public static string NormalizujKodISO(string? skrot)
	{
		var wynik = (skrot ?? "").Trim().ToUpperInvariant();
		return wynik switch
		{
			"ZŁ" => "PLN",
			"ZL" => "PLN",
			_ => wynik,
		};
	}

	public static bool CzyPolskiZloty(string? skrot)
	{
		return NormalizujKodISO(skrot) == "PLN";
	}
}
