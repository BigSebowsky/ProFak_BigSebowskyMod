namespace ProFak.DB;

public class RachunekBankowy : Rekord<RachunekBankowy>
{
	public int KontrahentId { get; set; }
	public string Nazwa { get; set; } = "";
	public string NumerRachunku { get; set; } = "";
	public string NazwaBanku { get; set; } = "";
	public string Swift { get; set; } = "";
	public int? WalutaId { get; set; }
	public bool CzyDomyslny { get; set; }

	public Ref<Kontrahent> KontrahentRef { get => KontrahentId; set => KontrahentId = value; }
	public Ref<Waluta> WalutaRef { get => WalutaId; set => WalutaId = value; }

	public Kontrahent? Kontrahent { get; set; }
	public Waluta? Waluta { get; set; }

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

	public override bool CzyPasuje(string fraza)
		=> base.CzyPasuje(fraza)
		|| CzyPasuje(Nazwa, fraza)
		|| CzyPasuje(NumerRachunku, fraza)
		|| CzyPasuje(NazwaBanku, fraza)
		|| CzyPasuje(Swift, fraza)
		|| CzyPasuje(WalutaSkrot, fraza)
		|| CzyPasuje(CzyDomyslny ? "Domyślny" : "", fraza);
}
