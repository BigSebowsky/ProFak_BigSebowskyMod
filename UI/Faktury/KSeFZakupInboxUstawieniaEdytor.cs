using ProFak.DB;

namespace ProFak.UI;

class KSeFZakupInboxUstawieniaEdytor : EdytorDwieKolumny<KSeFZakupInboxStan>
{
	private readonly TextBox textBoxOstatniaSynchronizacja;
	private readonly TextBox textBoxNastepnaSynchronizacja;

	public KSeFZakupInboxUstawieniaEdytor()
	{
		DodajCheckBox(e => e.CzyAutoSynchronizacja, "Automatyczna synchronizacja w tle");
		var numericUpDownInterwal = DodajNumericUpDown(e => e.InterwalSynchronizacjiMinuty, "Interwał synchronizacji [min]");
		numericUpDownInterwal.Minimum = 5;
		numericUpDownInterwal.Maximum = 24 * 60;
		var dateTimePickerPoczatkowa = DodajDatePicker(e => e.DataPoczatkowaSynchronizacji, "Pobieraj dokumenty od dnia");
		dateTimePickerPoczatkowa.MaxDate = DateTime.Today;

		textBoxOstatniaSynchronizacja = new TextBox { ReadOnly = true, TabStop = false, Anchor = AnchorStyles.Left | AnchorStyles.Right };
		textBoxNastepnaSynchronizacja = new TextBox { ReadOnly = true, TabStop = false, Anchor = AnchorStyles.Left | AnchorStyles.Right };
		DodajWiersz(textBoxOstatniaSynchronizacja, "Ostatnia synchronizacja");
		DodajWiersz(textBoxNastepnaSynchronizacja, "Następna synchronizacja");
		UstawRozmiar();
	}

	protected override void RekordGotowy()
	{
		base.RekordGotowy();
		textBoxOstatniaSynchronizacja.Text = Rekord.DataOstatniejSynchronizacji?.ToString(Wyglad.FormatCzasu) ?? "";
		textBoxNastepnaSynchronizacja.Text = Rekord.DataNastepnejSynchronizacji?.ToString(Wyglad.FormatCzasu) ?? "";
	}

	public override void KoniecEdycji()
	{
		base.KoniecEdycji();
		if (Rekord.CzyAutoSynchronizacja)
		{
			Rekord.DataNastepnejSynchronizacji = DateTime.Now.AddMinutes(Math.Max(Rekord.InterwalSynchronizacjiMinuty, 5));
		}
		else
		{
			Rekord.DataNastepnejSynchronizacji = null;
		}
	}
}
