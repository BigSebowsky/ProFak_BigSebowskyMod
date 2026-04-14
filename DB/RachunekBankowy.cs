namespace ProFak.DB;

public class RachunekBankowy : Rekord<RachunekBankowy>
{
	public int KontrahentId { get; set; }
	public string Nazwa { get; set; } = "";
	public string NumerRachunku { get; set; } = "";
	public string NumerEksportowy { get; set; } = "";
	public string NazwaBanku { get; set; } = "";
	public string Swift { get; set; } = "";
	public int? WalutaId { get; set; }
	public int? KrajId { get; set; }
	public bool CzyDomyslny { get; set; }

	public Ref<Kontrahent> KontrahentRef { get => KontrahentId; set => KontrahentId = value; }
	public Ref<Waluta> WalutaRef { get => WalutaId; set => WalutaId = value; }
	public Ref<Kraj> KrajRef { get => KrajId; set => KrajId = value; }

	public Kontrahent? Kontrahent { get; set; }
	public Waluta? Waluta { get; set; }
	public Kraj? Kraj { get; set; }

	public string NazwaFmt
	{
		get
		{
			if (!String.IsNullOrWhiteSpace(Nazwa)) return Nazwa;
			if (!String.IsNullOrWhiteSpace(NazwaBanku)) return $"{NazwaBanku}: {NumerRachunku}";
			return NumerRachunku;
		}
	}

	public string WalutaSkrot => Waluta?.Skrot ?? "";
	public string KrajKodISO2 => Kraj?.KodISO2 ?? "";
	public string CzyDomyslnyFmt => CzyDomyslny ? "Tak" : "";
	public string NumerDoEksportu => String.IsNullOrWhiteSpace(NumerEksportowy) ? NumerRachunku : NumerEksportowy;

	public override bool CzyPasuje(string fraza)
		=> base.CzyPasuje(fraza)
		|| CzyPasuje(Nazwa, fraza)
		|| CzyPasuje(NumerRachunku, fraza)
		|| CzyPasuje(NumerEksportowy, fraza)
		|| CzyPasuje(NazwaBanku, fraza)
		|| CzyPasuje(Swift, fraza)
		|| CzyPasuje(WalutaSkrot, fraza)
		|| CzyPasuje(KrajKodISO2, fraza)
		|| CzyPasuje(CzyDomyslnyFmt, fraza)
		|| CzyPasuje(CzyDomyslny ? "Domyślny" : "", fraza);
}
