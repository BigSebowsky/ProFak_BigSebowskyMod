using ProFak.IO.FA_3;
using System.Diagnostics;

namespace ProFak.UI;

public partial class OknoBledu : Form
{
	public OknoBledu()
	{
		InitializeComponent();
		ShowInTaskbar = false;
	}

	public OknoBledu(Exception exc)
		: this()
	{
		textBoxWyjatek.Text = exc.ToString();
	}

	public OknoBledu(string tekst, bool pokazLink = false)
		: this()
	{
		textBoxWyjatek.Text = tekst;
		linkLabelURL.Visible = pokazLink;
	}

	private void linkLabelURL_LinkClicked(object? sender, LinkLabelLinkClickedEventArgs e)
	{
		Process.Start(new ProcessStartInfo { UseShellExecute = true, FileName = ProFakInfo.IssuesUrl });
	}

	private void buttonKopiuj_Click(object? sender, EventArgs e)
	{
		if (String.IsNullOrWhiteSpace(textBoxWyjatek.Text)) return;
		Clipboard.SetText(textBoxWyjatek.Text);
	}

	public static void Pokaz(Exception exc)
	{
		if (exc is WalidacjaKSeFException)
		{
			using var okno = new OknoBledu(exc.Message);
			okno.Text = ProFakInfo.ProductName + " - Walidacja KSeF";
			okno.ShowDialog();
		}
		else if (exc.GetType() == typeof(ApplicationException))
		{
			MessageBox.Show(exc.Message, ProFakInfo.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
		}
		else if (exc is OperationCanceledException)
		{
			MessageBox.Show("Operacja została przerwana.", ProFakInfo.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Information);
		}
#if SQLSERVER
		else if (exc is Microsoft.Data.SqlClient.SqlException se && se.Number == 1222)
		{
			MessageBox.Show("Rekord jest modyfikowany na innym stanowisku.", ProFakInfo.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
		}
#else
		else if (exc is Microsoft.Data.Sqlite.SqliteException se && se.SqliteErrorCode == 5)
		{
			MessageBox.Show("Baza danych jest używana na innym stanowisku.", ProFakInfo.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
		}
#endif
		else
		{
			using var okno = new OknoBledu(exc);
			okno.ShowDialog();
		}
	}
}
