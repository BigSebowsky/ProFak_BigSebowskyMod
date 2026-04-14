using Microsoft.EntityFrameworkCore;
using ProFak.DB;
using System.ComponentModel;

namespace ProFak.UI;

class KursyWalutEkran : UserControl, IKontrolkaZKontekstem
{
	private readonly TableLayoutPanel uklad;
	private readonly FlowLayoutPanel panelAkcji;
	private readonly Label labelOdDnia;
	private readonly DateTimePicker dateTimePickerOdDnia;
	private readonly ButtonDPI buttonPobierzNBP;
	private readonly ButtonDPI buttonPrzeladuj;
	private readonly SplitContainer splitContainer;
	private readonly DataGridView dataGridViewDaty;
	private readonly DataGridView dataGridViewKursy;
	private readonly Label labelStatus;
	private bool zaladowano;
	private CancellationTokenSource? autoOdswiezCts;

	private readonly BindingList<DzienKursowWiersz> dni = [];
	private readonly BindingList<KursWalutyWiersz> kursy = [];

	private DateTime? WybranaData => dataGridViewDaty.CurrentRow?.DataBoundItem is DzienKursowWiersz wiersz ? wiersz.Data : null;

	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
	public Kontekst Kontekst
	{
		get => field;
		set
		{
			field = value;
			if (IsHandleCreated) Przeladuj();
		}
	} = default!;

	public KursyWalutEkran()
	{
		uklad = new TableLayoutPanel();
		panelAkcji = new FlowLayoutPanel();
		labelOdDnia = new Label();
		dateTimePickerOdDnia = new DateTimePicker();
		buttonPobierzNBP = new ButtonDPI();
		buttonPrzeladuj = new ButtonDPI();
		splitContainer = new SplitContainer();
		dataGridViewDaty = new DataGridView();
		dataGridViewKursy = new DataGridView();
		labelStatus = new Label();

		Dock = DockStyle.Fill;

		uklad.Dock = DockStyle.Fill;
		uklad.ColumnCount = 1;
		uklad.RowCount = 3;
		uklad.RowStyles.Add(new RowStyle());
		uklad.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
		uklad.RowStyles.Add(new RowStyle());
		Controls.Add(uklad);

		panelAkcji.AutoSize = true;
		panelAkcji.Dock = DockStyle.Fill;
		panelAkcji.Padding = new Padding(6);
		panelAkcji.WrapContents = false;

		labelOdDnia.AutoSize = true;
		labelOdDnia.Margin = new Padding(0, 8, 6, 0);
		labelOdDnia.Text = "Od dnia";

		dateTimePickerOdDnia.CustomFormat = Wyglad.FormatDaty;
		dateTimePickerOdDnia.Format = DateTimePickerFormat.Custom;
		dateTimePickerOdDnia.Value = DateTime.Today.AddDays(-365);
		dateTimePickerOdDnia.Width = 110;

		buttonPobierzNBP.AutoSize = true;
		buttonPobierzNBP.Text = "Pobierz z NBP";
		buttonPobierzNBP.Click += buttonPobierzNBP_Click;

		buttonPrzeladuj.AutoSize = true;
		buttonPrzeladuj.Text = "Przeladuj [F5]";
		buttonPrzeladuj.Click += buttonPrzeladuj_Click;

		panelAkcji.Controls.Add(labelOdDnia);
		panelAkcji.Controls.Add(dateTimePickerOdDnia);
		panelAkcji.Controls.Add(buttonPobierzNBP);
		panelAkcji.Controls.Add(buttonPrzeladuj);
		uklad.Controls.Add(panelAkcji, 0, 0);

		splitContainer.Dock = DockStyle.Fill;
		splitContainer.FixedPanel = FixedPanel.Panel1;
		uklad.Controls.Add(splitContainer, 0, 1);

		SkonfigurujGridDat();
		SkonfigurujGridKursow();

		splitContainer.Panel1.Controls.Add(dataGridViewDaty);
		splitContainer.Panel2.Controls.Add(dataGridViewKursy);

		labelStatus.AutoSize = true;
		labelStatus.Dock = DockStyle.Fill;
		labelStatus.Padding = new Padding(6, 4, 6, 6);
		uklad.Controls.Add(labelStatus, 0, 2);
	}

	protected override void OnCreateControl()
	{
		base.OnCreateControl();
		UstawPodzial();
		if (zaladowano || Kontekst == null) return;
		zaladowano = true;
		Przeladuj();
		_ = AutoOdswiezKursyAsync();
	}

	private async Task AutoOdswiezKursyAsync()
	{
		using var cts = autoOdswiezCts = new CancellationTokenSource();
		try
		{
			await NBPService.UzupelnijBrakujaceKursyAsync(Kontekst.Baza, cts.Token);
			if (IsHandleCreated) BeginInvoke(Przeladuj);
		}
		catch { }
		finally
		{
			if (autoOdswiezCts == cts) autoOdswiezCts = null;
		}
	}

	protected override void OnResize(EventArgs e)
	{
		base.OnResize(e);
		UstawPodzial();
	}

	private void SkonfigurujGridDat()
	{
		dataGridViewDaty.Dock = DockStyle.Fill;
		dataGridViewDaty.AllowUserToAddRows = false;
		dataGridViewDaty.AllowUserToDeleteRows = false;
		dataGridViewDaty.AllowUserToOrderColumns = false;
		dataGridViewDaty.AllowUserToResizeRows = false;
		dataGridViewDaty.AutoGenerateColumns = false;
		dataGridViewDaty.MultiSelect = false;
		dataGridViewDaty.ReadOnly = true;
		dataGridViewDaty.RowHeadersVisible = false;
		dataGridViewDaty.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
		dataGridViewDaty.Columns.Add(new DataGridViewTextBoxColumn
		{
			Name = nameof(DzienKursowWiersz.DataFmt),
			DataPropertyName = nameof(DzienKursowWiersz.DataFmt),
			HeaderText = "Dzien",
			AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
		});
		dataGridViewDaty.Columns.Add(new DataGridViewTextBoxColumn
		{
			Name = nameof(DzienKursowWiersz.LiczbaWalut),
			DataPropertyName = nameof(DzienKursowWiersz.LiczbaWalut),
			HeaderText = "Walut",
			Width = 60,
			DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight }
		});
		dataGridViewDaty.DataSource = dni;
		dataGridViewDaty.SelectionChanged += dataGridViewDaty_SelectionChanged;
	}

	private void SkonfigurujGridKursow()
	{
		dataGridViewKursy.Dock = DockStyle.Fill;
		dataGridViewKursy.AllowUserToAddRows = false;
		dataGridViewKursy.AllowUserToDeleteRows = false;
		dataGridViewKursy.AllowUserToOrderColumns = false;
		dataGridViewKursy.AllowUserToResizeRows = false;
		dataGridViewKursy.AutoGenerateColumns = false;
		dataGridViewKursy.MultiSelect = false;
		dataGridViewKursy.ReadOnly = true;
		dataGridViewKursy.RowHeadersVisible = false;
		dataGridViewKursy.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
		dataGridViewKursy.Columns.Add(new DataGridViewTextBoxColumn
		{
			Name = nameof(KursWalutyWiersz.Waluta),
			DataPropertyName = nameof(KursWalutyWiersz.Waluta),
			HeaderText = "Waluta",
			Width = 80
		});
		dataGridViewKursy.Columns.Add(new DataGridViewTextBoxColumn
		{
			Name = nameof(KursWalutyWiersz.KursSredni),
			DataPropertyName = nameof(KursWalutyWiersz.KursSredni),
			HeaderText = "Kurs sredni",
			Width = 110,
			DefaultCellStyle = new DataGridViewCellStyle
			{
				Alignment = DataGridViewContentAlignment.MiddleRight,
				Format = "#,##0.0000"
			}
		});
		dataGridViewKursy.Columns.Add(new DataGridViewTextBoxColumn
		{
			Name = nameof(KursWalutyWiersz.NumerTabeli),
			DataPropertyName = nameof(KursWalutyWiersz.NumerTabeli),
			HeaderText = "Tabela",
			AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
		});
		dataGridViewKursy.DataSource = kursy;
	}

	private void buttonPrzeladuj_Click(object? sender, EventArgs e)
	{
		Przeladuj();
	}

	private void buttonPobierzNBP_Click(object? sender, EventArgs e)
	{
		autoOdswiezCts?.Cancel();
		try
		{
			OknoPostepu.Uruchom(cancellationToken => NBPService.UzupelnijKursyAsync(Kontekst.Baza, dateTimePickerOdDnia.Value.Date, cancellationToken));
			Przeladuj();
		}
		catch (Exception exc)
		{
			OknoBledu.Pokaz(exc);
		}
	}

	private void dataGridViewDaty_SelectionChanged(object? sender, EventArgs e)
	{
		PrzeladujKursyDlaWybranegoDnia();
	}

	private void Przeladuj()
	{
		var wybranaData = WybranaData;
		var rekordy = Kontekst.Baza.KursyNBP
			.Include(kurs => kurs.Waluta)
			.AsEnumerable()
			.OrderByDescending(kurs => kurs.Data)
			.ThenBy(kurs => kurs.Waluta?.Skrot)
			.ToList();

		dni.RaiseListChangedEvents = false;
		dni.Clear();
		foreach (var dzien in rekordy
			.GroupBy(kurs => kurs.Data.Date)
			.Select(grupa => new DzienKursowWiersz
			{
				Data = grupa.Key,
				LiczbaWalut = grupa.Count()
			})
			.OrderByDescending(wiersz => wiersz.Data))
		{
			dni.Add(dzien);
		}
		dni.RaiseListChangedEvents = true;
		dni.ResetBindings();

		if (dni.Count == 0)
		{
			kursy.Clear();
			labelStatus.Text = "Brak kursow walut. Uzyj \"Pobierz z NBP\".";
			return;
		}

		var indeks = 0;
		if (wybranaData.HasValue)
		{
			var znaleziony = dni.ToList().FindIndex(wiersz => wiersz.Data == wybranaData.Value.Date);
			if (znaleziony >= 0) indeks = znaleziony;
		}

		if (dataGridViewDaty.Rows.Count > indeks)
		{
			dataGridViewDaty.ClearSelection();
			dataGridViewDaty.Rows[indeks].Selected = true;
			dataGridViewDaty.CurrentCell = dataGridViewDaty.Rows[indeks].Cells[0];
		}

		PrzeladujKursyDlaWybranegoDnia();
	}

	private void UstawPodzial()
	{
		if (!splitContainer.IsHandleCreated) return;
		splitContainer.Panel1MinSize = 180;
		splitContainer.Panel2MinSize = 300;
		var maksymalnyPodzial = splitContainer.Width - splitContainer.Panel2MinSize;
		if (maksymalnyPodzial < splitContainer.Panel1MinSize) return;
		splitContainer.SplitterDistance = Math.Min(230, maksymalnyPodzial);
	}

	private void PrzeladujKursyDlaWybranegoDnia()
	{
		kursy.RaiseListChangedEvents = false;
		kursy.Clear();

		if (WybranaData is not DateTime data)
		{
			kursy.RaiseListChangedEvents = true;
			kursy.ResetBindings();
			labelStatus.Text = "Brak wybranego dnia.";
			return;
		}

		var rekordy = Kontekst.Baza.KursyNBP
			.Include(kurs => kurs.Waluta)
			.Where(kurs => kurs.Data == data.Date)
			.AsEnumerable()
			.OrderBy(kurs => kurs.Waluta?.Skrot)
			.Select(kurs => new KursWalutyWiersz
			{
				Waluta = kurs.Waluta?.Skrot ?? "",
				KursSredni = kurs.KursSredni,
				NumerTabeli = kurs.NumerTabeli
			})
			.ToList();

		foreach (var rekord in rekordy) kursy.Add(rekord);

		kursy.RaiseListChangedEvents = true;
		kursy.ResetBindings();
		labelStatus.Text = $"Dzien: {data.ToString(Wyglad.FormatDaty)}, liczba walut: {rekordy.Count}";
	}

	private sealed class DzienKursowWiersz
	{
		public DateTime Data { get; set; }
		public int LiczbaWalut { get; set; }
		public string DataFmt => Data.ToString(Wyglad.FormatDaty);
	}

	private sealed class KursWalutyWiersz
	{
		public string Waluta { get; set; } = "";
		public decimal KursSredni { get; set; }
		public string NumerTabeli { get; set; } = "";
	}
}
