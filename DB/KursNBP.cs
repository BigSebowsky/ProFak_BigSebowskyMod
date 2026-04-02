namespace ProFak.DB;

public class KursNBP : Rekord<KursNBP>
{
	public int WalutaId { get; set; }
	public DateTime Data { get; set; }
	public decimal KursSredni { get; set; }
	public string NumerTabeli { get; set; } = "";

	public Ref<Waluta> WalutaRef { get => WalutaId; set => WalutaId = value; }

	public Waluta? Waluta { get; set; }

	public override bool CzyPasuje(string fraza)
		=> base.CzyPasuje(fraza)
		|| CzyPasuje(Waluta?.Skrot, fraza)
		|| CzyPasuje(Data.ToString(UI.Format.Data), fraza)
		|| CzyPasuje(KursSredni, fraza)
		|| CzyPasuje(NumerTabeli, fraza);
}
