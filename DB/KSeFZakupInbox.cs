namespace ProFak.DB;

public class KSeFZakupInbox : Rekord<KSeFZakupInbox>
{
	public string NumerKSeF { get; set; } = "";
	public string Numer { get; set; } = "";
	public DateTime DataWystawienia { get; set; }
	public DateTime DataSprzedazy { get; set; }
	public DateTime? DataKSeF { get; set; }
	public DateTime DataPobrania { get; set; } = DateTime.Now;
	public DateTime? DataWeryfikacji { get; set; }
	public DateTime? DataDodaniaJakoZakup { get; set; }
	public DateTime? DataRozliczenia { get; set; }
	public string NazwaSprzedawcy { get; set; } = "";
	public string NIPSprzedawcy { get; set; } = "";
	public string NazwaNabywcy { get; set; } = "";
	public string NIPNabywcy { get; set; } = "";
	public decimal RazemNetto { get; set; }
	public decimal RazemVat { get; set; }
	public decimal RazemBrutto { get; set; }
	public string Waluta { get; set; } = "";
	public string TypDokumentu { get; set; } = "";
	public string XMLKSeF { get; set; } = "";
	public string URLKSeF { get; set; } = "";
	public bool CzyNowa { get; set; } = true;
	public KSeFZakupInboxStatus Status { get; set; } = KSeFZakupInboxStatus.Pobrana;
	public int? FakturaId { get; set; }

	public Ref<Faktura> FakturaRef { get => FakturaId; set => FakturaId = value; }
	public Faktura? Faktura { get; set; }

	public string StatusFmt => Format(Status);
	public string NumerZakupu => Faktura?.Numer ?? "";
	public bool CzyDodanaJakoZakup => FakturaId.HasValue;
	public bool CzyRozliczona => Status == KSeFZakupInboxStatus.Rozliczona;

	public override bool CzyPasuje(string fraza)
		=> base.CzyPasuje(fraza)
		|| CzyPasuje(NumerKSeF, fraza)
		|| CzyPasuje(Numer, fraza)
		|| CzyPasuje(DataWystawienia, fraza)
		|| CzyPasuje(DataSprzedazy, fraza)
		|| CzyPasuje(DataKSeF, fraza)
		|| CzyPasuje(DataPobrania, fraza)
		|| CzyPasuje(DataWeryfikacji, fraza)
		|| CzyPasuje(DataDodaniaJakoZakup, fraza)
		|| CzyPasuje(DataRozliczenia, fraza)
		|| CzyPasuje(NazwaSprzedawcy, fraza)
		|| CzyPasuje(NIPSprzedawcy, fraza)
		|| CzyPasuje(NazwaNabywcy, fraza)
		|| CzyPasuje(NIPNabywcy, fraza)
		|| CzyPasuje(RazemNetto, fraza)
		|| CzyPasuje(RazemVat, fraza)
		|| CzyPasuje(RazemBrutto, fraza)
		|| CzyPasuje(Waluta, fraza)
		|| CzyPasuje(TypDokumentu, fraza)
		|| CzyPasuje(StatusFmt, fraza)
		|| CzyPasuje(NumerZakupu, fraza)
		|| CzyPasuje(CzyNowa ? "Nowa" : "", fraza);
}

public enum KSeFZakupInboxStatus
{
	Pobrana,
	Zweryfikowana,
	DodanaJakoZakup,
	Rozliczona,
	Pominieta
}
