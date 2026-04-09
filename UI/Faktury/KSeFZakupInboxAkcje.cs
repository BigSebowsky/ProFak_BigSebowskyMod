using ProFak.DB;
using ProFak.IO.KSEF2;
using System.Diagnostics;

namespace ProFak.UI;

class SynchronizujKSeFZakupInboxAkcja : AkcjaNaSpisie<KSeFZakupInbox>
{
	public override string Nazwa => "⟳ Synchronizuj z KSeF [F5]";
	public override bool CzyDostepnaDlaRekordow(IEnumerable<KSeFZakupInbox> zaznaczoneRekordy) => true;
	public override bool CzyKlawiszSkrotu(Keys klawisz, Keys modyfikatory) => modyfikatory == Keys.None && klawisz == Keys.F5;

	public override void Uruchom(Kontekst kontekst, ref IEnumerable<KSeFZakupInbox> zaznaczoneRekordy)
	{
	}
}

class ZweryfikujKSeFZakupInboxAkcja : AkcjaNaSpisie<KSeFZakupInbox>
{
	private readonly KSeFZakupInboxSpis spis;

	public override string Nazwa => "✓ Oznacz jako zweryfikowane [CTRL-Q]";
	public override bool CzyDostepnaDlaRekordow(IEnumerable<KSeFZakupInbox> zaznaczoneRekordy) => zaznaczoneRekordy.Any();
	public override bool CzyKlawiszSkrotu(Keys klawisz, Keys modyfikatory) => modyfikatory == Keys.Control && klawisz == Keys.Q;
	public override bool PrzeladujPoZakonczeniu => false;

	public ZweryfikujKSeFZakupInboxAkcja(KSeFZakupInboxSpis spis)
	{
		this.spis = spis;
	}

	public override void Uruchom(Kontekst kontekst, ref IEnumerable<KSeFZakupInbox> zaznaczoneRekordy)
	{
		var rekordy = zaznaczoneRekordy.ToList();
		var teraz = DateTime.Now;
		foreach (var rekord in rekordy)
		{
			if (rekord.Status == KSeFZakupInboxStatus.DodanaJakoZakup || rekord.Status == KSeFZakupInboxStatus.Rozliczona) continue;
			rekord.Status = KSeFZakupInboxStatus.Zweryfikowana;
			rekord.CzyNowa = false;
			rekord.DataWeryfikacji ??= teraz;
		}
		kontekst.Baza.Zapisz(rekordy);
		spis.OdswiezLokalnie();
	}
}

class PominKSeFZakupInboxAkcja : AkcjaNaSpisie<KSeFZakupInbox>
{
	private readonly KSeFZakupInboxSpis spis;

	public override string Nazwa => "⊘ Oznacz jako pominięte [CTRL-D]";
	public override bool CzyDostepnaDlaRekordow(IEnumerable<KSeFZakupInbox> zaznaczoneRekordy) => zaznaczoneRekordy.Any(e => !e.CzyDodanaJakoZakup);
	public override bool CzyKlawiszSkrotu(Keys klawisz, Keys modyfikatory) => modyfikatory == Keys.Control && klawisz == Keys.D;
	public override bool PrzeladujPoZakonczeniu => false;

	public PominKSeFZakupInboxAkcja(KSeFZakupInboxSpis spis)
	{
		this.spis = spis;
	}

	public override void Uruchom(Kontekst kontekst, ref IEnumerable<KSeFZakupInbox> zaznaczoneRekordy)
	{
		var rekordy = zaznaczoneRekordy.Where(e => !e.CzyDodanaJakoZakup).ToList();
		foreach (var rekord in rekordy)
		{
			rekord.Status = KSeFZakupInboxStatus.Pominieta;
			rekord.CzyNowa = false;
		}
		if (rekordy.Count > 0) kontekst.Baza.Zapisz(rekordy);
		spis.OdswiezLokalnie();
	}
}

class UstawieniaKSeFZakupInboxAkcja : AkcjaNaSpisie<KSeFZakupInbox>
{
	private readonly KSeFZakupInboxSpis spis;

	public override string Nazwa => "⚙ Ustawienia synchronizacji";
	public override bool CzyDostepnaDlaRekordow(IEnumerable<KSeFZakupInbox> zaznaczoneRekordy) => true;
	public override bool PrzeladujPoZakonczeniu => false;

	public UstawieniaKSeFZakupInboxAkcja(KSeFZakupInboxSpis spis)
	{
		this.spis = spis;
	}

	public override void Uruchom(Kontekst kontekst, ref IEnumerable<KSeFZakupInbox> zaznaczoneRekordy)
	{
		using var nowyKontekst = new Kontekst(kontekst);
		using var transakcja = nowyKontekst.Transakcja();
		var rekord = KSeFZakupInboxService.PobierzLubUtworzStan(nowyKontekst.Baza);
		nowyKontekst.Dodaj(rekord);
		using var edytor = new KSeFZakupInboxUstawieniaEdytor();
		using var okno = new Dialog("Ustawienia synchronizacji KSeF", edytor, nowyKontekst);
		edytor.Przygotuj(nowyKontekst, rekord);
		if (okno.ShowDialog() != DialogResult.OK) return;
		edytor.KoniecEdycji();
		nowyKontekst.Baza.Zapisz(rekord);
		transakcja.Zatwierdz();
		spis.OdswiezLokalnie();
	}
}

class DodajJakoZakupZInboxAkcja : AkcjaNaSpisie<KSeFZakupInbox>
{
	private readonly KSeFZakupInboxSpis spis;

	public override string Nazwa => "➕ Dodaj jako zakup [INS]";
	public override bool CzyDostepnaDlaRekordow(IEnumerable<KSeFZakupInbox> zaznaczoneRekordy) => zaznaczoneRekordy.Any();
	public override bool CzyKlawiszSkrotu(Keys klawisz, Keys modyfikatory) => modyfikatory == Keys.None && klawisz == Keys.Insert;
	public override bool PrzeladujPoZakonczeniu => false;

	public DodajJakoZakupZInboxAkcja(KSeFZakupInboxSpis spis)
	{
		this.spis = spis;
	}

	public override void Uruchom(Kontekst kontekst, ref IEnumerable<KSeFZakupInbox> zaznaczoneRekordy)
	{
		var pominOkno = false;
		if (zaznaczoneRekordy.Count() > 1)
		{
			var odp = MessageBox.Show("Wybrano więcej niż jeden dokument. Czy dodać zakupy w ciemno, bez wyświetlania formularza edycji dla każdej z nich?", "ProFak", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
			if (odp == DialogResult.Cancel) return;
			if (odp == DialogResult.Yes) pominOkno = true;
		}

		var rekordy = new List<KSeFZakupInbox>();
		var podmiot = kontekst.Baza.Kontrahenci.First(kontrahent => kontrahent.CzyPodmiot);
		foreach (var naglowek in zaznaczoneRekordy.ToList())
		{
			using var nowyKontekst = new Kontekst(kontekst);
			using var transakcja = nowyKontekst.Transakcja();
			var rekord = nowyKontekst.Baza.Znajdz(naglowek.Ref);
			var istniejaca = nowyKontekst.Baza.Faktury.FirstOrDefault(e => e.NumerKSeF == rekord.NumerKSeF && e.Rodzaj != RodzajFaktury.Usunięta);
			if (istniejaca != null)
			{
				var odp = MessageBox.Show($"Faktura {istniejaca.Numer} ({istniejaca.NumerKSeF}) już istnieje w bazie. Czy mimo to chcesz ją dodać ponownie?", "ProFak", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
				if (odp == DialogResult.No) continue;
				if (odp == DialogResult.Cancel) break;
			}

			if (String.IsNullOrEmpty(rekord.XMLKSeF))
			{
				OknoPostepu.Uruchom(async cancellationToken =>
				{
					await KSeFZakupInboxService.ZapewnijXmlAsync(nowyKontekst.Baza, rekord, cancellationToken);
				});
			}

			var faktura = IO.FA_3.Generator.ZbudujDB(nowyKontekst.Baza, rekord.XMLKSeF);
			faktura.NumerKSeF = rekord.NumerKSeF;
			faktura.DataKSeF = rekord.DataKSeF;
			using var api = new IO.KSEF2.API(podmiot.SrodowiskoKSeF);
			faktura.URLKSeF = api.ZbudujUrl(rekord.XMLKSeF, faktura.NIPSprzedawcy, faktura.DataWystawienia);
			nowyKontekst.Baza.Zapisz(faktura);
			IO.FA_3.Generator.PoprawPowiazaniaPoZapisie(nowyKontekst.Baza, faktura);

			if (!pominOkno)
			{
				nowyKontekst.Dodaj(faktura);
				using var edytor = new FakturaZakupuEdytor();
				using var okno = new Dialog("Nowa pozycja", edytor, nowyKontekst);
				edytor.Przygotuj(nowyKontekst, faktura);
				if (okno.ShowDialog() != DialogResult.OK)
				{
					if (naglowek != zaznaczoneRekordy.Last() && MessageBox.Show("Kontynuować dodawanie kolejnych dokumentów?", "ProFak", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button1) != DialogResult.Yes)
						break;
					continue;
				}
				edytor.KoniecEdycji();
				nowyKontekst.Baza.Zapisz(faktura);
			}

			rekord.FakturaRef = faktura;
			rekord.Status = faktura.CzyZaplacona ? KSeFZakupInboxStatus.Rozliczona : KSeFZakupInboxStatus.DodanaJakoZakup;
			rekord.CzyNowa = false;
			rekord.DataWeryfikacji ??= DateTime.Now;
			rekord.DataDodaniaJakoZakup = DateTime.Now;
			rekord.DataRozliczenia = faktura.CzyZaplacona ? faktura.DataWplywu : null;
			nowyKontekst.Baza.Zapisz(rekord);

			transakcja.Zatwierdz();
			rekordy.Add(rekord);
		}
		zaznaczoneRekordy = rekordy;
		spis.OdswiezLokalnie();
	}
}

class OtworzZakupZInboxAkcja : AkcjaNaSpisie<KSeFZakupInbox>
{
	private readonly KSeFZakupInboxSpis spis;

	public override string Nazwa => "✎ Otwórz zakup [F2]";
	public override bool CzyDostepnaDlaRekordow(IEnumerable<KSeFZakupInbox> zaznaczoneRekordy) => zaznaczoneRekordy.Count() == 1 && zaznaczoneRekordy.Single().FakturaRef.IsNotNull;
	public override bool CzyKlawiszSkrotu(Keys klawisz, Keys modyfikatory) => modyfikatory == Keys.None && (klawisz == Keys.Enter || klawisz == Keys.F2);
	public override bool PrzeladujPoZakonczeniu => false;

	public OtworzZakupZInboxAkcja(KSeFZakupInboxSpis spis)
	{
		this.spis = spis;
	}

	public override void Uruchom(Kontekst kontekst, ref IEnumerable<KSeFZakupInbox> zaznaczoneRekordy)
	{
		using var nowyKontekst = new Kontekst(kontekst);
		using var transakcja = nowyKontekst.Transakcja();
		var rekord = nowyKontekst.Baza.Znajdz(zaznaczoneRekordy.Single().Ref);
		if (rekord.FakturaRef.IsNull) return;
		nowyKontekst.Baza.Zablokuj(rekord.FakturaRef);
		var faktura = nowyKontekst.Baza.Znajdz(rekord.FakturaRef);
		nowyKontekst.Dodaj(faktura);
		using var edytor = new FakturaZakupuEdytor();
		using var okno = new Dialog("Edycja danych", edytor, nowyKontekst);
		edytor.Przygotuj(nowyKontekst, faktura);
		if (okno.ShowDialog() != DialogResult.OK) return;
		edytor.KoniecEdycji();
		nowyKontekst.Baza.Zapisz(faktura);
		KSeFZakupInboxService.OdswiezPowiazaniaIStatusy(nowyKontekst.Baza);
		transakcja.Zatwierdz();
		spis.OdswiezLokalnie();
	}
}

class ZapiszJakoXMLKSeFZakupInboxAkcja : AkcjaNaSpisie<KSeFZakupInbox>
{
	public override string Nazwa => "🖫 Zapisz jako XML [CTRL-S]";
	public override bool CzyDostepnaDlaRekordow(IEnumerable<KSeFZakupInbox> zaznaczoneRekordy) => zaznaczoneRekordy.Any();
	public override bool CzyKlawiszSkrotu(Keys klawisz, Keys modyfikatory) => modyfikatory == Keys.Control && klawisz == Keys.S;
	public override bool PrzeladujPoZakonczeniu => false;

	public override void Uruchom(Kontekst kontekst, ref IEnumerable<KSeFZakupInbox> zaznaczoneRekordy)
	{
		var rekordy = zaznaczoneRekordy.ToList();
		if (rekordy.Count == 1) ZapiszJeden(kontekst, rekordy[0]);
		else ZapiszWiele(kontekst, rekordy);
	}

	private static void ZapiszJeden(Kontekst kontekst, KSeFZakupInbox rekord)
	{
		using var dialog = new SaveFileDialog();
		dialog.Title = "Zapisywanie pliku";
		dialog.RestoreDirectory = true;
		dialog.FileName = rekord.NumerKSeF + ".xml";
		if (dialog.ShowDialog() != DialogResult.OK) return;

		if (String.IsNullOrEmpty(rekord.XMLKSeF))
		{
			OknoPostepu.Uruchom(async cancellationToken =>
			{
				await KSeFZakupInboxService.ZapewnijXmlAsync(kontekst.Baza, rekord, cancellationToken);
			});
		}

		File.WriteAllText(dialog.FileName, rekord.XMLKSeF);
		File.SetLastWriteTime(dialog.FileName, rekord.DataKSeF ?? rekord.DataWystawienia);
	}

	private static void ZapiszWiele(Kontekst kontekst, IEnumerable<KSeFZakupInbox> rekordy)
	{
		using var dialog = new FolderBrowserDialog();
		dialog.Description = "Wybierz katalog, do którego mają zostać zapisane pliki.";
		dialog.AutoUpgradeEnabled = false;
		if (dialog.ShowDialog() != DialogResult.OK) return;
		var katalog = dialog.SelectedPath;

		OknoPostepu.Uruchom(async cancellationToken =>
		{
			foreach (var rekord in rekordy)
			{
				cancellationToken.ThrowIfCancellationRequested();
				if (String.IsNullOrEmpty(rekord.XMLKSeF)) await KSeFZakupInboxService.ZapewnijXmlAsync(kontekst.Baza, rekord, cancellationToken);
				var plik = Path.Combine(katalog, rekord.NumerKSeF) + ".xml";
				File.WriteAllText(plik, rekord.XMLKSeF);
				File.SetLastWriteTime(plik, rekord.DataKSeF ?? rekord.DataWystawienia);
			}
		});
	}
}

class ZapiszJakoPDFKSeFZakupInboxAkcja : AkcjaNaSpisie<KSeFZakupInbox>
{
	public override string Nazwa => "🖫 Zapisz PDF KSeF [CTRL-P]";
	public override bool CzyDostepnaDlaRekordow(IEnumerable<KSeFZakupInbox> zaznaczoneRekordy) => zaznaczoneRekordy.Any();
	public override bool CzyKlawiszSkrotu(Keys klawisz, Keys modyfikatory) => modyfikatory == Keys.Control && klawisz == Keys.P;
	public override bool PrzeladujPoZakonczeniu => false;

	public override void Uruchom(Kontekst kontekst, ref IEnumerable<KSeFZakupInbox> zaznaczoneRekordy)
	{
		var rekordy = zaznaczoneRekordy.ToList();
		if (rekordy.Count == 1)
		{
			var rekord = rekordy[0];
			using var dialog = new SaveFileDialog();
			dialog.Filter = "Dokument PDF (*.pdf)|*.pdf";
			dialog.Title = "Zapisywanie wizualizacji faktury KSeF";
			dialog.RestoreDirectory = true;
			dialog.FileName = rekord.NumerKSeF + ".pdf";
			if (dialog.ShowDialog() != DialogResult.OK) return;

			byte[] pdf = [];
			OknoPostepu.Uruchom(async cancellationToken =>
			{
				if (String.IsNullOrEmpty(rekord.XMLKSeF)) await KSeFZakupInboxService.ZapewnijXmlAsync(kontekst.Baza, rekord, cancellationToken);
				pdf = IO.KSEFPDF.Generator.ZbudujPDF(rekord.XMLKSeF, rekord.NumerKSeF, cancellationToken);
			});
			File.WriteAllBytes(dialog.FileName, pdf);
			Process.Start(new ProcessStartInfo { UseShellExecute = true, FileName = dialog.FileName });
			return;
		}

		using var folder = new FolderBrowserDialog();
		folder.AutoUpgradeEnabled = false;
		folder.Description = "Wybierz folder, do którego mają zostać zapisane pliki PDF.";
		if (folder.ShowDialog() != DialogResult.OK) return;
		var katalog = folder.SelectedPath;
		var liczbaPlikow = 0;

		OknoPostepu.Uruchom(async cancellationToken =>
		{
			foreach (var rekord in rekordy)
			{
				cancellationToken.ThrowIfCancellationRequested();
				if (String.IsNullOrEmpty(rekord.XMLKSeF)) await KSeFZakupInboxService.ZapewnijXmlAsync(kontekst.Baza, rekord, cancellationToken);
				var pdf = IO.KSEFPDF.Generator.ZbudujPDF(rekord.XMLKSeF, rekord.NumerKSeF, cancellationToken);
				File.WriteAllBytes(Path.Combine(katalog, rekord.NumerKSeF + ".pdf"), pdf);
				liczbaPlikow++;
			}
		});
		MessageBox.Show($"Liczba wygenerowanych plików PDF: {liczbaPlikow}.", "ProFak", MessageBoxButtons.OK, MessageBoxIcon.Information);
	}
}
