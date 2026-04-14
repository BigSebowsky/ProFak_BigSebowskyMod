using Microsoft.EntityFrameworkCore;
using ProFak.DB;
using System.ComponentModel;

namespace ProFak.UI;

class RachunekBankowySpis : Spis<RachunekBankowy>
{
	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
	public Ref<Kontrahent> KontrahentRef { get; set; }

	public RachunekBankowySpis()
	{
		DodajKolumne(nameof(RachunekBankowy.NazwaFmt), "Nazwa", rozciagnij: true);
		DodajKolumne(nameof(RachunekBankowy.NumerRachunku), "Numer rachunku", rozciagnij: true);
		DodajKolumne(nameof(RachunekBankowy.NazwaBanku), "Bank");
		DodajKolumne(nameof(RachunekBankowy.Swift), "SWIFT");
		DodajKolumne(nameof(RachunekBankowy.KrajKodISO2), "Kraj");
		DodajKolumne(nameof(RachunekBankowy.WalutaSkrot), "Waluta");
		DodajKolumne(nameof(RachunekBankowy.CzyDomyslnyFmt), "Domyślny");
		DodajKolumneId();
	}

	protected override void Przeladuj()
	{
		IQueryable<RachunekBankowy> q = Kontekst.Baza.RachunkiBankowe.Include(rachunek => rachunek.Waluta).Include(rachunek => rachunek.Kraj);
		if (KontrahentRef.IsNotNull) q = q.Where(rachunek => rachunek.KontrahentId == KontrahentRef.Id);
		Rekordy = q.OrderByDescending(rachunek => rachunek.CzyDomyslny).ThenBy(rachunek => rachunek.Nazwa).ThenBy(rachunek => rachunek.Id).ToList();
	}
}
