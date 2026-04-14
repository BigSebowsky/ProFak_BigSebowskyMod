using ProFak.DB;

namespace ProFak.UI;

class WalutaEdytor : EdytorDwieKolumny<Waluta>
{
	public WalutaEdytor()
	{
		var textBoxSkrot = DodajTextBox(waluta => waluta.Skrot, "Skrót", wymagane: true);
		DodajTextBox(waluta => waluta.Nazwa, "Nazwa", wymagane: true);
		DodajCheckBox(waluta => waluta.CzyDomyslna, "Domyślna");
		Walidacja(textBoxSkrot, skrot =>
		{
			var kodISO = Waluta.NormalizujKodISO(skrot);
			return kodISO.Length == 3 && kodISO.All(Char.IsLetter)
				? null
				: "Kod waluty powinien być 3-literowym kodem ISO 4217, np. PLN, EUR, GBP.";
		}, miekki: false);
		textBoxSkrot.Validating += (sender, e) =>
		{
			textBoxSkrot.Text = Waluta.NormalizujKodISO(textBoxSkrot.Text);
		};
		UstawRozmiar();
	}
}
