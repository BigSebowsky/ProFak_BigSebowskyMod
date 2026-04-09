namespace ProFak.DB;

public class KSeFZakupInboxStan : Rekord<KSeFZakupInboxStan>
{
	public bool CzyAutoSynchronizacja { get; set; } = true;
	public int InterwalSynchronizacjiMinuty { get; set; } = 15;
	public DateTime DataPoczatkowaSynchronizacji { get; set; } = new DateTime(2026, 2, 1);
	public DateTime? DataOstatniejSynchronizacji { get; set; }
	public DateTime? DataNastepnejSynchronizacji { get; set; }
	public int LiczbaNowychDokumentow { get; set; }

	public override bool CzyPasuje(string fraza)
		=> base.CzyPasuje(fraza)
		|| CzyPasuje(CzyAutoSynchronizacja ? "Automatyczna" : "", fraza)
		|| CzyPasuje(InterwalSynchronizacjiMinuty, fraza)
		|| CzyPasuje(DataPoczatkowaSynchronizacji, fraza)
		|| CzyPasuje(DataOstatniejSynchronizacji, fraza)
		|| CzyPasuje(DataNastepnejSynchronizacji, fraza)
		|| CzyPasuje(LiczbaNowychDokumentow, fraza);
}
