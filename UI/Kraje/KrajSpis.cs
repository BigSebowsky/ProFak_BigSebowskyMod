using ProFak.DB;

namespace ProFak.UI;

class KrajSpis : Spis<Kraj>
{
	public KrajSpis()
	{
		DodajKolumne(nameof(Kraj.KodISO2), "Kod ISO2");
		DodajKolumne(nameof(Kraj.Nazwa), "Nazwa", rozciagnij: true);
		DodajKolumne(nameof(Kraj.CzyUEFmt), "UE");
		DodajKolumneId();
	}

	protected override void Przeladuj()
	{
		Rekordy = Kontekst.Baza.Kraje.AsEnumerable().OrderBy(kraj => kraj.KodISO2);
	}
}
