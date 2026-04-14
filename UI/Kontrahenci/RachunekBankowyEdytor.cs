using ProFak.DB;

namespace ProFak.UI;

class RachunekBankowyEdytor : EdytorDwieKolumny<RachunekBankowy>
{
	private readonly ComboBox comboBoxKraj;
	private readonly ButtonDPI buttonKraj;
	private readonly ComboBox comboBoxWaluta;
	private readonly ButtonDPI buttonWaluta;

	public RachunekBankowyEdytor()
	{
		DodajTextBox(rachunekBankowy => rachunekBankowy.Nazwa, "Nazwa");
		DodajTextBox(rachunekBankowy => rachunekBankowy.NumerRachunku, "Numer rachunku", wymagane: true);
		DodajTextBox(rachunekBankowy => rachunekBankowy.NumerEksportowy, "Numer eksportowy / KSeF");
		DodajTextBox(rachunekBankowy => rachunekBankowy.NazwaBanku, "Nazwa banku");
		DodajTextBox(rachunekBankowy => rachunekBankowy.Swift, "SWIFT");

		comboBoxKraj = new ComboBox
		{
			Anchor = AnchorStyles.Left | AnchorStyles.Right,
			DropDownStyle = ComboBoxStyle.DropDownList,
			Width = 200
		};
		buttonKraj = new ButtonDPI
		{
			AutoSize = true,
			Text = "..."
		};
		var panelKraj = new TableLayoutPanel
		{
			AutoSize = true,
			AutoSizeMode = AutoSizeMode.GrowAndShrink,
			ColumnCount = 2,
			Dock = DockStyle.Fill,
			Margin = new Padding(0)
		};
		panelKraj.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
		panelKraj.ColumnStyles.Add(new ColumnStyle());
		panelKraj.Controls.Add(comboBoxKraj, 0, 0);
		panelKraj.Controls.Add(buttonKraj, 1, 0);
		DodajWiersz(panelKraj, "Kraj rachunku");
		kontroler.Powiazanie(comboBoxKraj, rachunekBankowy => rachunekBankowy.KrajRef);

		comboBoxWaluta = new ComboBox
		{
			Anchor = AnchorStyles.Left | AnchorStyles.Right,
			DropDownStyle = ComboBoxStyle.DropDownList,
			Width = 200
		};
		buttonWaluta = new ButtonDPI
		{
			AutoSize = true,
			Text = "..."
		};
		var panelWaluta = new TableLayoutPanel
		{
			AutoSize = true,
			AutoSizeMode = AutoSizeMode.GrowAndShrink,
			ColumnCount = 2,
			Dock = DockStyle.Fill,
			Margin = new Padding(0)
		};
		panelWaluta.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
		panelWaluta.ColumnStyles.Add(new ColumnStyle());
		panelWaluta.Controls.Add(comboBoxWaluta, 0, 0);
		panelWaluta.Controls.Add(buttonWaluta, 1, 0);
		DodajWiersz(panelWaluta, "Waluta");
		kontroler.Powiazanie(comboBoxWaluta, rachunekBankowy => rachunekBankowy.WalutaRef);

		DodajCheckBox(rachunekBankowy => rachunekBankowy.CzyDomyslny, "Rachunek domyślny");
		UstawRozmiar();
	}

	protected override void KontekstGotowy()
	{
		base.KontekstGotowy();

		new Slownik<Waluta>(
			Kontekst, comboBoxWaluta, buttonWaluta,
			Kontekst.Baza.Waluty.OrderBy(waluta => waluta.CzyDomyslna ? 0 : 1).ThenBy(waluta => waluta.Skrot).ToList,
			waluta => waluta.Nazwa,
			waluta => { },
			Spisy.Waluty,
			dopuscPustaWartosc: true)
			.Zainstaluj();

		new Slownik<Kraj>(
			Kontekst, comboBoxKraj, buttonKraj,
			Kontekst.Baza.Kraje.OrderBy(kraj => kraj.KodISO2).ToList,
			kraj => kraj.NazwaFmt,
			kraj => { },
			Spisy.Kraje,
			dopuscPustaWartosc: true)
			.Zainstaluj();
	}

	public override void KoniecEdycji()
	{
		base.KoniecEdycji();
		if (!Rekord.CzyDomyslny) return;

		var inneRachunkiDomyslne = Kontekst.Baza.RachunkiBankowe
			.Where(rachunek => rachunek.KontrahentId == Rekord.KontrahentId && rachunek.Id != Rekord.Id && rachunek.CzyDomyslny)
			.ToList();
		foreach (var rachunek in inneRachunkiDomyslne)
		{
			rachunek.CzyDomyslny = false;
		}
		if (inneRachunkiDomyslne.Any()) Kontekst.Baza.Zapisz(inneRachunkiDomyslne);
	}
}
