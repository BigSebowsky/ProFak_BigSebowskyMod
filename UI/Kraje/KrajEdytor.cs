using ProFak.DB;

namespace ProFak.UI;

class KrajEdytor : EdytorDwieKolumny<Kraj>
{
	public KrajEdytor()
	{
		DodajTextBox(kraj => kraj.KodISO2, "Kod ISO2", wymagane: true);
		DodajTextBox(kraj => kraj.Nazwa, "Nazwa", wymagane: true);
		DodajCheckBox(kraj => kraj.CzyUE, "Kraj UE");
		UstawRozmiar();
	}
}
