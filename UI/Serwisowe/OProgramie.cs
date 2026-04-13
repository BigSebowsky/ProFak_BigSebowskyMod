using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ProFak.UI;

partial class OProgramie : UserControl, IKontrolkaZKontekstem
{
	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
	public Kontekst Kontekst { get; set; } = default!;

	public OProgramie()
	{
		InitializeComponent();
		labelWersja.Text = ProFakInfo.InformationalVersion;
		labelSciezka.Text = Environment.ProcessPath;
		labelData.Text = File.GetLastWriteTime(Environment.ProcessPath!).ToString("d MMMM yyyy, H:mm:ss");
	}

	private void linkLabelStrona_LinkClicked(object? sender, LinkLabelLinkClickedEventArgs e)
	{
		Process.Start(new ProcessStartInfo { UseShellExecute = true, FileName = ProFakInfo.RepositoryUrl });
	}

	private void btnSprawdzAktualizacje_Click(object? sender, EventArgs e)
	{
		try
		{
			string? response = null;
			OknoPostepu.Uruchom(async cancellationToken =>
			{
				using var wb = new HttpClient();
				wb.DefaultRequestHeaders.UserAgent.ParseAdd(ProFakInfo.UserAgent);
				response = await wb.GetStringAsync(ProFakInfo.ReleasesLatestApiUrl, cancellationToken);
				cancellationToken.ThrowIfCancellationRequested();
			});

			var json = JsonDocument.Parse(response!);
			var wersjaGitHub = WersjaWydania.Parse(json.RootElement.GetProperty("tag_name").ToString());
			var wersjaAplikacji = WersjaWydania.Parse(ProFakInfo.InformationalVersion);
			if (wersjaGitHub.CompareTo(wersjaAplikacji) > 0)
			{
				if (MessageBox.Show("Dostępna jest nowa wersja " + wersjaGitHub + ".\r\nCzy chcesz przejść do strony pobierania?", ProFakInfo.ProductName, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
				{
					Process.Start(new ProcessStartInfo { UseShellExecute = true, FileName = json.RootElement.GetProperty("html_url").ToString() });
				}
			}
			else
			{
				MessageBox.Show("Nie znaleziono nowej wersji programu", ProFakInfo.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Information);
			}
		}
		catch (Exception ex)
		{
			OknoBledu.Pokaz(ex);
		}
	}

	private readonly record struct WersjaWydania(Version Bazowa, int WersjaForka, string Tekst) : IComparable<WersjaWydania>
	{
		public static WersjaWydania Parse(string? tekst)
		{
			var wartosc = (tekst ?? "").Trim();
			if (wartosc.StartsWith("v", StringComparison.OrdinalIgnoreCase)) wartosc = wartosc[1..];
			var match = Regex.Match(wartosc, @"^(?<base>\d+(?:\.\d+){1,3})(?:-bigseb\.(?<fork>\d+))?$", RegexOptions.IgnoreCase);
			if (!match.Success)
			{
				Version.TryParse(wartosc, out var wersjaFallback);
				return new WersjaWydania(wersjaFallback ?? new Version(0, 0), 0, String.IsNullOrWhiteSpace(tekst) ? "0.0.0" : tekst!);
			}

			Version.TryParse(match.Groups["base"].Value, out var bazowa);
			var wersjaForka = match.Groups["fork"].Success ? Int32.Parse(match.Groups["fork"].Value) : 0;
			return new WersjaWydania(bazowa ?? new Version(0, 0), wersjaForka, String.IsNullOrWhiteSpace(tekst) ? wartosc : tekst!);
		}

		public int CompareTo(WersjaWydania other)
		{
			var wynik = Bazowa.CompareTo(other.Bazowa);
			return wynik != 0 ? wynik : WersjaForka.CompareTo(other.WersjaForka);
		}

		public override string ToString() => Tekst;
	}
}
