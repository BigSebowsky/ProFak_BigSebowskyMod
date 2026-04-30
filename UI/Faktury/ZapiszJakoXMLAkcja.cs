using ProFak.DB;

namespace ProFak.UI;

class ZapiszJakoXMLAkcja : AkcjaNaSpisie<Faktura>
{
	public override string Nazwa => "🖫 Zapisz jako XML [CTRL-S]";
	public override bool CzyDostepnaDlaRekordow(IEnumerable<Faktura> zaznaczoneRekordy) => zaznaczoneRekordy.Count() >= 1;
	public override bool CzyKlawiszSkrotu(Keys klawisz, Keys modyfikatory) => modyfikatory == Keys.Control && klawisz == Keys.S;
	public override bool PrzeladujPoZakonczeniu => false;

	public override void Uruchom(Kontekst kontekst, ref IEnumerable<Faktura> zaznaczoneRekordy)
	{
		var podmiot = kontekst.Baza.Kontrahenci.First(kontrahent => kontrahent.CzyPodmiot);
		if (zaznaczoneRekordy.Count() == 1) ZapiszJeden(podmiot, zaznaczoneRekordy.Single());
		else ZapiszWiele(podmiot, zaznaczoneRekordy);
	}

	protected string? WybierzPlik(string numerKSeF)
	{
		using var dialog = new SaveFileDialog();
		dialog.Title = "Zapisywanie pliku";
		dialog.RestoreDirectory = true;
		dialog.FileName = numerKSeF + ".xml";
		if (dialog.ShowDialog() != DialogResult.OK) return null;
		return dialog.FileName;
	}

	protected string? WybierzKatalog()
	{
		using var dialog = new FolderBrowserDialog();
		dialog.Description = "Wybierz katalog, do którego mają zostać zapisane pliki.";
		dialog.AutoUpgradeEnabled = false;
		if (dialog.ShowDialog() != DialogResult.OK) return null;
		return dialog.SelectedPath;
	}

	protected void ZapiszXml(string plik, Faktura naglowek, string xml)
	{
		File.WriteAllText(plik, xml);
		File.SetLastWriteTime(plik, naglowek.DataKSeF ?? naglowek.DataWystawienia);
	}

	private void ZapiszJeden(Kontrahent podmiot, Faktura naglowek)
	{
		var plik = WybierzPlik(naglowek.NumerKSeF);
		if (plik == null) return;
		if (String.IsNullOrEmpty(naglowek.XMLKSeF))
		{
			OknoPostepu.Uruchom(async cancellationToken =>
			{
				using var api = new IO.KSEF2.API(podmiot.SrodowiskoKSeF);
				await api.UwierzytelnijAsync(podmiot.NIP, podmiot.TokenKSeF, cancellationToken);
				naglowek.XMLKSeF = await api.PobierzFaktureAsync(naglowek.NumerKSeF, cancellationToken);
			});
		}

		ZapiszXml(plik, naglowek, naglowek.XMLKSeF);
	}

	private void ZapiszWiele(Kontrahent podmiot, IEnumerable<Faktura> naglowki)
	{
		var katalog = WybierzKatalog();
		if (katalog == null) return;

		OknoPostepu.Uruchom(async cancellationToken =>
		{
			using var api = new IO.KSEF2.API(podmiot.SrodowiskoKSeF);
			await api.UwierzytelnijAsync(podmiot.NIP, podmiot.TokenKSeF, cancellationToken);
			foreach (var naglowek in naglowki)
			{
				if (cancellationToken.IsCancellationRequested) break;
				if (String.IsNullOrEmpty(naglowek.XMLKSeF)) naglowek.XMLKSeF = await api.PobierzFaktureAsync(naglowek.NumerKSeF, cancellationToken);
				var plik = Path.Combine(katalog, naglowek.NumerKSeF) + ".xml";
				ZapiszXml(plik, naglowek, naglowek.XMLKSeF);
			}
		});
	}
}

class ZapiszJakoXMLLokalneAkcja : ZapiszJakoXMLAkcja
{
	public override string Nazwa => "🖫 Zapisz XML KSeF [CTRL-S]";
	public override bool CzyDostepnaDlaRekordow(IEnumerable<Faktura> zaznaczoneRekordy) => zaznaczoneRekordy.Count(e => e.CzySprzedaz || !String.IsNullOrEmpty(e.XMLKSeF)) >= 1;

	public override void Uruchom(Kontekst kontekst, ref IEnumerable<Faktura> zaznaczoneRekordy)
	{
		if (zaznaczoneRekordy.Count() == 1) ZapiszJeden(kontekst, zaznaczoneRekordy.Single());
		else ZapiszWiele(kontekst, zaznaczoneRekordy);
	}

	private void ZapiszJeden(Kontekst kontekst, Faktura faktura)
	{
		var plik = WybierzPlik(faktura.NumerKSeFJakoNazwaPliku);
		if (plik == null) return;
		var xml = ZapewnijLokalnyXml(kontekst, faktura);
		ZapiszXml(plik, faktura, xml);
	}

	private void ZapiszWiele(Kontekst kontekst, IEnumerable<Faktura> faktury)
	{
		var katalog = WybierzKatalog();
		if (katalog == null) return;

		OknoPostepu.Uruchom(async cancellationToken =>
		{
			foreach (var faktura in faktury)
			{
				if (cancellationToken.IsCancellationRequested) break;
				var xml = ZapewnijLokalnyXml(kontekst, faktura);
				var plik = Path.Combine(katalog, faktura.NumerKSeFJakoNazwaPliku) + ".xml";
				ZapiszXml(plik, faktura, xml);
			}
		});
	}

	private static string ZapewnijLokalnyXml(Kontekst kontekst, Faktura faktura)
	{
		if (!faktura.CzySprzedaz)
		{
			if (String.IsNullOrWhiteSpace(faktura.XMLKSeF))
				throw new ApplicationException($"Brak XML KSeF dla faktury {faktura.NumerKSeFJakoNazwaPliku}.");
			return faktura.XMLKSeF;
		}

		if (String.IsNullOrWhiteSpace(faktura.Numer))
			throw new ApplicationException("Przed zapisaniem XML należy zapisać fakturę w celu nadania jej numeru.");

		kontekst.Baza.Zapisz(faktura);
		if (String.IsNullOrWhiteSpace(faktura.NumerKSeF) || String.IsNullOrWhiteSpace(faktura.XMLKSeF))
		{
			faktura.XMLKSeF = IO.FA_3.Generator.ZbudujXML(kontekst.Baza, faktura);
			kontekst.Baza.Zapisz(faktura);
		}

		return faktura.XMLKSeF;
	}
}
