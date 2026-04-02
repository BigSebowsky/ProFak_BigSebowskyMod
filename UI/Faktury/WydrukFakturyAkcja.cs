using ProFak.DB;

namespace ProFak.UI;

class WydrukFakturyAkcja : AkcjaNaSpisie<Faktura>
{
	private sealed class WyborSzablonuWydruku : UserControl
	{
		private readonly ComboBox comboBoxSzablon;

		public string Szablon => comboBoxSzablon.SelectedValue?.ToString() ?? "Faktura";

		public WyborSzablonuWydruku(string domyslnySzablon)
		{
			var labelSzablon = new Label
			{
				Anchor = AnchorStyles.Left,
				AutoSize = true,
				Text = "Szablon wydruku:"
			};
			comboBoxSzablon = new ComboBox
			{
				Anchor = AnchorStyles.Left | AnchorStyles.Right,
				DropDownStyle = ComboBoxStyle.DropDownList,
				DisplayMember = "Text",
				ValueMember = "Value",
				Margin = new Padding(3, 0, 3, 0),
				Width = 220
			};
			comboBoxSzablon.Items.Add(new { Text = "Klasyczny (PL)", Value = "Faktura" });
			comboBoxSzablon.Items.Add(new { Text = "Nowoczesny (PL)", Value = "FakturaPL" });
			comboBoxSzablon.Items.Add(new { Text = "Nowoczesny (PL/EN)", Value = "FakturaEN" });
			comboBoxSzablon.SelectedIndex = Math.Max(0, comboBoxSzablon.FindStringExact(
				domyslnySzablon == "FakturaEN" ? "Nowoczesny (PL/EN)" :
				domyslnySzablon == "FakturaPL" ? "Nowoczesny (PL)" :
				"Klasyczny (PL)"));

			var tableLayoutPanel = new TableLayoutPanel
			{
				AutoSize = true,
				AutoSizeMode = AutoSizeMode.GrowAndShrink,
				ColumnCount = 2,
				Dock = DockStyle.Fill,
				RowCount = 1
			};
			tableLayoutPanel.ColumnStyles.Add(new ColumnStyle());
			tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
			tableLayoutPanel.Controls.Add(labelSzablon, 0, 0);
			tableLayoutPanel.Controls.Add(comboBoxSzablon, 1, 0);

			AutoSize = true;
			AutoSizeMode = AutoSizeMode.GrowAndShrink;
			Controls.Add(tableLayoutPanel);
			comboBoxSzablon.Select();
		}
	}

	public override string Nazwa => "🖶 Drukuj [CTRL-P]";
	public override bool CzyDostepnaDlaRekordow(IEnumerable<Faktura> zaznaczoneRekordy) => zaznaczoneRekordy.Any() && !zaznaczoneRekordy.Any(e => !e.Numerator.HasValue);
	public override bool CzyKlawiszSkrotu(Keys klawisz, Keys modyfikatory) => klawisz == Keys.P && modyfikatory == Keys.Control;
	protected virtual bool CzyDuplikat => false;

	public override void Uruchom(Kontekst kontekst, ref IEnumerable<Faktura> zaznaczoneRekordy)
	{
		var szablon = WybierzSzablon(kontekst);
		if (szablon == null) return;
		var wydruk = new Wydruki.Faktura(kontekst.Baza, zaznaczoneRekordy.Select(faktura => faktura.Ref), CzyDuplikat, szablon);
		using var okno = new OknoWydruku(wydruk);
		okno.ShowDialog();
	}

	private static string? WybierzSzablon(Kontekst kontekst)
	{
		using var wybor = new WyborSzablonuWydruku(Wyglad.SzablonFaktury);
		using var okno = new Dialog("Wybór szablonu wydruku", wybor, kontekst);
		okno.TekstPrzyciskuZapisz = "Drukuj [F10]";
		okno.TekstPrzyciskuAnuluj = "Anuluj [ESC]";
		return okno.ShowDialog() == DialogResult.OK ? wybor.Szablon : null;
	}
}
