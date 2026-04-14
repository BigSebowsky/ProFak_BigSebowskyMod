namespace ProFak.DB;

public class Kraj : Rekord<Kraj>
{
	public string KodISO2 { get; set; } = "";
	public string Nazwa { get; set; } = "";
	public bool CzyUE { get; set; }

	public string CzyUEFmt => CzyUE ? "Tak" : "Nie";
	public string NazwaFmt => String.IsNullOrWhiteSpace(KodISO2) ? Nazwa : $"{KodISO2} - {Nazwa}";

	public override bool CzyPasuje(string fraza)
		=> base.CzyPasuje(fraza)
		|| CzyPasuje(KodISO2, fraza)
		|| CzyPasuje(Nazwa, fraza)
		|| CzyPasuje(CzyUE ? "UE" : "", fraza);
}
