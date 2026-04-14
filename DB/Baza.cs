#define nLOG_SQL
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ProFak.DB;

public class Baza : DbContext
{
	public static string NazwaKataloguProgramu => "ProFak";
	public static string NazwaPlikuBazy => "profak.sqlite3";
	public static string NazwaOdnosnikaDoBazy => "baza.txt";
	public static string NazwaKataloguKopiiZapasowych => "Kopie zapasowe";
	public static string PrywatnyKatalog => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
	public static string PublicznyKatalog => Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
	public static string LokalnyKatalog => Path.GetDirectoryName(Environment.ProcessPath)!;
	public static string PrywatnaSciezka => Path.Combine(PrywatnyKatalog, NazwaKataloguProgramu, NazwaPlikuBazy);
	public static string PublicznaSciezka => Path.Combine(PublicznyKatalog, NazwaKataloguProgramu, NazwaPlikuBazy);
	public static string LokalnaSciezka => Path.Combine(LokalnyKatalog, NazwaPlikuBazy);
	public static string OdnosnikDoBazy => Path.Combine(LokalnyKatalog, NazwaOdnosnikaDoBazy);
	public static string AktywnyKatalog => Path.GetDirectoryName(Sciezka)!;
	public static string KatalogKopiiZapasowych => Path.Combine(AktywnyKatalog, NazwaKataloguKopiiZapasowych);

	public static string? Sciezka { get; set; }

	private static SqliteConnection? bazaTymczasowa;
	private readonly string parametryPolaczenia;

	public IQueryable<DeklaracjaVat> DeklaracjeVat => Set<DeklaracjaVat>();
	public IQueryable<DodatkowyPodmiot> DodatkowePodmioty => Set<DodatkowyPodmiot>();
	public IQueryable<Faktura> Faktury => Set<Faktura>();
	public IQueryable<JednostkaMiary> JednostkiMiar => Set<JednostkaMiary>();
	public IQueryable<Kraj> Kraje => Set<Kraj>();
	public IQueryable<KSeFZakupInbox> KSeFZakupyInbox => Set<KSeFZakupInbox>();
	public IQueryable<KSeFZakupInboxStan> KSeFZakupyInboxStan => Set<KSeFZakupInboxStan>();
	public IQueryable<KolumnaSpisu> KolumnySpisow => Set<KolumnaSpisu>();
	public IQueryable<Konfiguracja> Konfiguracja => Set<Konfiguracja>();
	public IQueryable<Kontrahent> Kontrahenci => Set<Kontrahent>();
	public IQueryable<KursNBP> KursyNBP => Set<KursNBP>();
	public IQueryable<RachunekBankowy> RachunkiBankowe => Set<RachunekBankowy>();
	public IQueryable<Numerator> Numeratory => Set<Numerator>();
	public IQueryable<Plik> Pliki => Set<Plik>();
	public IQueryable<PozycjaFaktury> PozycjeFaktur => Set<PozycjaFaktury>();
	public IQueryable<SkladkaZus> SkladkiZus => Set<SkladkaZus>();
	public IQueryable<SposobPlatnosci> SposobyPlatnosci => Set<SposobPlatnosci>();
	public IQueryable<StanMenu> StanyMenu => Set<StanMenu>();
	public IQueryable<StanNumeratora> StanyNumeratorow => Set<StanNumeratora>();
	public IQueryable<StawkaVat> StawkiVat => Set<StawkaVat>();
	public IQueryable<Towar> Towary => Set<Towar>();
	public IQueryable<UrzadSkarbowy> UrzedySkarbowe => Set<UrzadSkarbowy>();
	public IQueryable<Waluta> Waluty => Set<Waluta>();
	public IQueryable<Wplata> Wplaty => Set<Wplata>();
	public IQueryable<Zawartosc> Zawartosci => Set<Zawartosc>();
	public IQueryable<ZaliczkaPit> ZaliczkiPit => Set<ZaliczkaPit>();

	public Baza(string? sciezka)
	{
		parametryPolaczenia = PrzygotujParametryPolaczenia(sciezka ?? Sciezka);
		// IdentityResolution potrzebne, żeby dało się jednocześnie skasować dwie faktury z dołączoną taką samą walutą;
		// bez tego RemoveRange próbuje dodać dwie takie same waluty do konktekstu i wywala się na duplikacji.
		ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTrackingWithIdentityResolution;
	}

	public Baza()
		: this(null)
	{
	}

	public static void Przygotuj()
	{
		WykonajAutomatycznaKopieBazy();
#if SQLSERVER
		using var baza = new DB.BazaSqlServer();
#else
		using var baza = new DB.Baza();
#endif
		baza.Migruj();
		DaneStartowe.Zaladuj(baza);
	}

	public void Migruj()
	{
#if !SQLSERVER
		NaprawHistorieMigracjiSqlite();
#endif
		Database.Migrate();
		NormalizujWalutyDoISO();
		UzupelnijDateKursuFaktur();
	}

	private void UzupelnijDateKursuFaktur()
	{
		var fakturyDoUzupelnienia = Faktury
			.Include(faktura => faktura.Waluta)
			.Where(faktura => faktura.DataKursu == null && faktura.WalutaId != null && faktura.KursWaluty != 1)
			.ToList();
		if (fakturyDoUzupelnienia.Count == 0) return;

		var zmienione = new List<Faktura>();
		foreach (var faktura in fakturyDoUzupelnienia)
		{
			if (faktura.Waluta == null || faktura.Waluta.CzyDomyslna) continue;
			var kurs = NBPService.ZnajdzKursDlaWartosci(this, faktura.Waluta.KodISO, faktura.DataWystawienia, faktura.KursWaluty);
			if (kurs == null) continue;
			faktura.DataKursu = kurs.Data;
			zmienione.Add(faktura);
		}

		if (zmienione.Count > 0) Zapisz(zmienione);
	}

	private void NormalizujWalutyDoISO()
	{
		var waluty = Waluty.OrderBy(waluta => waluta.Id).ToList();
		if (waluty.Count == 0) return;

		var wedlugKodu = new Dictionary<string, Waluta>(StringComparer.OrdinalIgnoreCase);
		var zmienione = new List<Waluta>();
		var doUsuniecia = new List<Waluta>();

		foreach (var waluta in waluty)
		{
			var kodISO = Waluta.NormalizujKodISO(waluta.Skrot);
			if (String.IsNullOrWhiteSpace(kodISO))
			{
				if (waluta.Skrot != kodISO)
				{
					waluta.Skrot = kodISO;
					zmienione.Add(waluta);
				}
				continue;
			}

			if (!wedlugKodu.TryGetValue(kodISO, out var docelowa))
			{
				wedlugKodu[kodISO] = waluta;
				if (waluta.Skrot != kodISO)
				{
					waluta.Skrot = kodISO;
					zmienione.Add(waluta);
				}
				continue;
			}

			if (docelowa.Id == waluta.Id) continue;

			ScalWaluty(waluta, docelowa, zmienione);
			doUsuniecia.Add(waluta);
		}

		if (zmienione.Count > 0) Zapisz(zmienione.DistinctBy(waluta => waluta.Id).ToList());
		if (doUsuniecia.Count > 0) Usun(doUsuniecia);
	}

	private void ScalWaluty(Waluta zrodlo, Waluta cel, List<Waluta> zmienione)
	{
		Faktury.Where(faktura => faktura.WalutaId == zrodlo.Id)
			.ExecuteUpdate(aktualizacja => aktualizacja.SetProperty(faktura => faktura.WalutaId, cel.Id));
		Kontrahenci.Where(kontrahent => kontrahent.DomyslnaWalutaId == zrodlo.Id)
			.ExecuteUpdate(aktualizacja => aktualizacja.SetProperty(kontrahent => kontrahent.DomyslnaWalutaId, cel.Id));
		RachunkiBankowe.Where(rachunek => rachunek.WalutaId == zrodlo.Id)
			.ExecuteUpdate(aktualizacja => aktualizacja.SetProperty(rachunek => rachunek.WalutaId, cel.Id));

		var zdublowaneKursy = KursyNBP
			.Where(kurs => kurs.WalutaId == zrodlo.Id)
			.Join(
				KursyNBP.Where(kurs => kurs.WalutaId == cel.Id),
				zrodloKurs => zrodloKurs.Data,
				celKurs => celKurs.Data,
				(zrodloKurs, _) => zrodloKurs.Id)
			.ToList();
		if (zdublowaneKursy.Count > 0)
		{
			KursyNBP.Where(kurs => zdublowaneKursy.Contains(kurs.Id)).ExecuteDelete();
		}

		KursyNBP.Where(kurs => kurs.WalutaId == zrodlo.Id)
			.ExecuteUpdate(aktualizacja => aktualizacja.SetProperty(kurs => kurs.WalutaId, cel.Id));

		if (zrodlo.CzyDomyslna && !cel.CzyDomyslna)
		{
			cel.CzyDomyslna = true;
			zmienione.Add(cel);
		}

		if (String.IsNullOrWhiteSpace(cel.Nazwa) && !String.IsNullOrWhiteSpace(zrodlo.Nazwa))
		{
			cel.Nazwa = zrodlo.Nazwa;
			zmienione.Add(cel);
		}
	}

	private static void WykonajAutomatycznaKopieBazy()
	{
		try
		{
			if (String.IsNullOrEmpty(Sciezka) || !File.Exists(Sciezka)) return;
			var sciezkaKopiiDziennej = Sciezka + "~";
			var sciezkaKopiiMiesiecznej = Sciezka + "~~";
			var dzis = DateTime.Now.Date;
			var miesiac = dzis.AddDays(1 - dzis.Day);
			var dataBazy = File.GetLastWriteTime(Sciezka);
			var dataKopiiDziennej = File.Exists(sciezkaKopiiDziennej) ? File.GetLastWriteTime(sciezkaKopiiDziennej) : DateTime.MinValue;
			var dataKopiiMiesiecznej = File.Exists(sciezkaKopiiMiesiecznej) ? File.GetLastWriteTime(sciezkaKopiiMiesiecznej) : DateTime.MinValue;

			if (dataBazy <= dataKopiiDziennej) return;
			if (dzis == dataKopiiDziennej.Date) return;

			if (dataKopiiMiesiecznej < miesiac && dataKopiiDziennej > miesiac)
			{
				File.Delete(sciezkaKopiiMiesiecznej);
				File.Move(sciezkaKopiiDziennej, sciezkaKopiiMiesiecznej);
			}

			WykonajKopie(sciezkaKopiiDziennej);
		}
		catch
		{
		}
	}

#if !SQLSERVER
	private void NaprawHistorieMigracjiSqlite()
	{
		if (String.IsNullOrEmpty(Sciezka) || !File.Exists(Sciezka)) return;
		using var connection = new SqliteConnection(PrzygotujParametryPolaczenia(Sciezka));
		connection.Open();

		if (!CzyIstniejeTabela(connection, "__EFMigrationsHistory")) return;

		// W części baz struktura została już zmieniona ręcznie albo przez niepełną migrację,
		// więc wpis w historii EF może nie istnieć mimo poprawnego schematu.
		if (CzyIstniejeTabela(connection, "Konfiguracja") && CzyIstniejeKolumna(connection, "Konfiguracja", "SzablonFaktury"))
		{
			DodajMigracjeJesliBrakuje(connection, "20260331120000_SzablonFaktury");
		}

		if (CzyIstniejeTabela(connection, "RachunekBankowy"))
		{
			DodajMigracjeJesliBrakuje(connection, "20260402142946_RachunkiBankoweKontrahenta");
		}

		if (CzyIstniejeTabela(connection, "KursNBP"))
		{
			DodajMigracjeJesliBrakuje(connection, "20260402144112_KursyNBP");
		}
	}

	private static void DodajMigracjeJesliBrakuje(SqliteConnection connection, string migrationId)
	{
		if (CzyIstniejeMigracja(connection, migrationId)) return;

		using var command = connection.CreateCommand();
		command.CommandText = "INSERT INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") VALUES (@migrationId, @productVersion)";
		command.Parameters.AddWithValue("@migrationId", migrationId);
		command.Parameters.AddWithValue("@productVersion", typeof(DbContext).Assembly.GetName().Version?.ToString(3) ?? "10.0.2");
		command.ExecuteNonQuery();
	}

	private static bool CzyIstniejeTabela(SqliteConnection connection, string nazwaTabeli)
	{
		using var command = connection.CreateCommand();
		command.CommandText = "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = @nazwa";
		command.Parameters.AddWithValue("@nazwa", nazwaTabeli);
		return command.ExecuteScalar() != null;
	}

	private static bool CzyIstniejeKolumna(SqliteConnection connection, string nazwaTabeli, string nazwaKolumny)
	{
		using var command = connection.CreateCommand();
		command.CommandText = $"PRAGMA table_info(\"{nazwaTabeli}\")";
		using var reader = command.ExecuteReader();
		while (reader.Read())
		{
			if (String.Equals(reader["name"]?.ToString(), nazwaKolumny, StringComparison.OrdinalIgnoreCase)) return true;
		}
		return false;
	}

	private static bool CzyIstniejeMigracja(SqliteConnection connection, string migrationId)
	{
		using var command = connection.CreateCommand();
		command.CommandText = "SELECT 1 FROM \"__EFMigrationsHistory\" WHERE \"MigrationId\" = @migrationId";
		command.Parameters.AddWithValue("@migrationId", migrationId);
		return command.ExecuteScalar() != null;
	}
#endif

	public static void WykonajKopie(string plikDocelowy)
	{
		string? plikNieaktualny = null;
		if (File.Exists(plikDocelowy))
		{
			plikNieaktualny = plikDocelowy + "-del";
			File.Move(plikDocelowy, plikNieaktualny);
		}
		using (var zrodlo = new SqliteConnection(PrzygotujParametryPolaczenia(Sciezka)))
		{
			zrodlo.Open();
			using var cel = new SqliteConnection($"Data Source={plikDocelowy};Pooling=false");
			zrodlo.BackupDatabase(cel);
		}
		if (plikNieaktualny != null) File.Delete(plikNieaktualny);
	}

	public static void UstalSciezkeBazy()
	{
		if (File.Exists(OdnosnikDoBazy)) Sciezka = File.ReadAllLines(OdnosnikDoBazy)[0];
		else if (File.Exists(LokalnaSciezka)) Sciezka = LokalnaSciezka;
		else if (File.Exists(PrywatnaSciezka)) Sciezka = PrywatnaSciezka;
		else if (File.Exists(PublicznaSciezka)) Sciezka = PublicznaSciezka;
	}

	public static void ZapiszOdnosnikDoBazy()
	{
		try
		{
			if (Sciezka == PublicznaSciezka || Sciezka == PrywatnaSciezka || Sciezka == Baza.LokalnaSciezka)
			{
				if (File.Exists(OdnosnikDoBazy)) File.Delete(OdnosnikDoBazy);
			}
			else
			{
				File.WriteAllText(OdnosnikDoBazy, Sciezka);
			}
		}
		catch
		{
		}
	}

	private static string PrzygotujParametryPolaczenia(string? sciezka)
	{
#if SQLSERVER
		return sciezka;
#else
		string polaczenie;
		if (sciezka == null)
		{
			polaczenie = "Data Source=ProFak;Mode=Memory;Cache=Shared";
			if (bazaTymczasowa == null)
			{
				bazaTymczasowa = new SqliteConnection(polaczenie);
				bazaTymczasowa.Open();
			}
		}
		else
		{
			polaczenie = $"Data Source={sciezka}";
		}
		return polaczenie;
#endif
	}

	protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
	{
		base.OnConfiguring(optionsBuilder);
#if SQLSERVER
		optionsBuilder.UseSqlServer(parametryPolaczenia);
#else
		optionsBuilder.UseSqlite(parametryPolaczenia, opts => opts.CommandTimeout(5));
#endif
#if LOG_SQL
		optionsBuilder.EnableSensitiveDataLogging();
		optionsBuilder.LogTo((evt, level) => evt == RelationalEventId.CommandExecuting || evt == RelationalEventId.CommandExecuted, LogCommand);
#endif
	}
#if LOG_SQL
	private void LogCommand(EventData eventData)
	{
		if (eventData.EventId == RelationalEventId.CommandExecuting && eventData is CommandEventData ced)
		{
			Debug.WriteLine($"Uruchamianie polecenia\n{ced.Command.CommandText}");
			if (ced.Command.Parameters.Count > 0) Debug.WriteLine($"Parametry:\n{String.Join("\n", ced.Command.Parameters.Cast<System.Data.Common.DbParameter>().Select(p => p.ParameterName + "='" + p.Value + "'"))}");
		}
		else if (eventData.EventId == RelationalEventId.CommandExecuted && eventData is CommandExecutedEventData ceed)
		{
			Debug.WriteLine($"Polecenie wykonane w {ceed.Duration.TotalMilliseconds} ms");
		}
	}
#endif
	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		DB.Model.ProFakModelBuilder.Configure(modelBuilder);
	}

	public void Dodaj<TRekord>(IEnumerable<TRekord> rekordy)
		where TRekord : Rekord<TRekord>
	{
		var set = Set<TRekord>();
		foreach (var rekord in rekordy)
		{
			set.Add(rekord);
		}
		ZapiszZmiany();
	}

	public void Zapisz<TRekord>(TRekord rekord)
		where TRekord : Rekord<TRekord>
		=> Zapisz(new[] { rekord });

	public void Zapisz<TRekord>(IEnumerable<TRekord> rekordy)
		where TRekord : Rekord<TRekord>
	{
		var set = Set<TRekord>();
		foreach (var rekord in rekordy)
		{
			if (rekord.Id <= 0)
			{
				rekord.Id = 0;
				set.Add(rekord);
			}
			else
			{
				Entry(rekord).State = EntityState.Modified;
			}
		}
		ZapiszZmiany();
	}

	public void Zapisz(object rekord)
	{
		Entry(rekord).State = EntityState.Modified;
		ZapiszZmiany();
	}

	public void Usun<TRekord>()
		where TRekord : Rekord<TRekord>
		=> Set<TRekord>().ExecuteDelete();

	public void Usun<TRekord>(TRekord rekord)
		where TRekord : Rekord<TRekord>
		=> Usun(new[] { rekord });

	public void Usun<TRekord>(IEnumerable<TRekord> rekordy)
		where TRekord : Rekord<TRekord>
	{
		Set<TRekord>().RemoveRange(rekordy);
		ZapiszZmiany();
	}

	public void Usun(object rekord)
	{
		Entry(rekord).State = EntityState.Deleted;
		ZapiszZmiany();
	}

	private void ZapiszZmiany()
	{
		try
		{
			NormalizujDanePrzedZapisem();
			SaveChanges();
		}
		finally
		{
			ChangeTracker.Entries().Where(e => e.Entity != null).ToList().ForEach(e => e.State = EntityState.Detached);
		}
	}

	private void NormalizujDanePrzedZapisem()
	{
		foreach (var entry in ChangeTracker.Entries<Waluta>().Where(entry => entry.State == EntityState.Added || entry.State == EntityState.Modified))
		{
			entry.Entity.Normalizuj();
		}
	}

	public void Zablokuj<TRekord>(Ref<TRekord> rekordRef)
		where TRekord : Rekord<TRekord>
	{
#if SQLSERVER
		Database.ExecuteSqlRaw($"SET LOCK_TIMEOUT 1000; SELECT Id FROM {typeof(TRekord).Name} WITH (ROWLOCK, UPDLOCK) WHERE Id={rekordRef.Id}");
#endif
	}

	public void Zablokuj<TRekord>()
		where TRekord : Rekord<TRekord>
	{
#if SQLSERVER
		Database.ExecuteSqlRaw($"SET LOCK_TIMEOUT 1000; SELECT Id FROM {typeof(TRekord).Name} WITH (TABLOCK, UPDLOCK)");
#endif
	}

	public bool CzyZablokowana()
	{
#if !SQLSERVER
		if (Database.CurrentTransaction != null) return false;
		if (Sciezka == null) return false;
		try
		{
			using var connection = new SqliteConnection(PrzygotujParametryPolaczenia(Sciezka));
			connection.Open();
			using (var command = connection.CreateCommand())
			{
				command.CommandText = "BEGIN IMMEDIATE TRANSACTION";
				command.CommandTimeout = -1;
				command.ExecuteNonQuery();
			}
			using (var command = connection.CreateCommand())
			{
				command.CommandText = "ROLLBACK";
				command.ExecuteNonQuery();
			}
		}
		catch (SqliteException se) when (se.SqliteErrorCode == 5)
		{
			return true;
		}
#endif
		return false;
	}

	public TRekord Znajdz<TRekord>(Ref<TRekord> rekordRef)
		where TRekord : Rekord<TRekord>
		=> Set<TRekord>().FirstOrDefault(r => r.Id == rekordRef.Id) ?? throw new ApplicationException($"Nie znaleziono rekordu {rekordRef}.");


	public TRekord? ZnajdzLubNull<TRekord>(Ref<TRekord> rekordRef)
		where TRekord : Rekord<TRekord>
		=> rekordRef.IsNull ? null : Znajdz(rekordRef);

	public IEnumerable<Dictionary<string, object>> Zapytanie(FormattableString zapytanie)
	{
		using var polaczenie = Database.GetDbConnection();
		polaczenie.Open();
		using var polecenie = polaczenie.CreateCommand();
		var nazwyParametrow = new List<string>();
		for (int i = 0; i < zapytanie.ArgumentCount; i++)
		{
			var nazwaParametru = "@P" + i;
			nazwyParametrow.Add(nazwaParametru);
			var wartoscParametru = zapytanie.GetArgument(i);
			if (wartoscParametru == null) wartoscParametru = DBNull.Value;
			var parametr = polecenie.CreateParameter();
			parametr.ParameterName = nazwaParametru;
			parametr.Value = wartoscParametru;
			polecenie.Parameters.Add(parametr);
		}
		var sql = String.Format(zapytanie.Format, nazwyParametrow.ToArray());
		polecenie.CommandText = sql;
		using var reader = polecenie.ExecuteReader();
		var wynik = new List<Dictionary<string, object>>();
		while (reader.Read())
		{
			var wiersz = new Dictionary<string, object>();
			for (int i = 0; i < reader.FieldCount; i++)
			{
				wiersz[reader.GetName(i)] = reader.GetValue(i);
			}
			wynik.Add(wiersz);
		}
		return wynik;
	}

	public static void ZamknijPolaczenia()
	{
		SqliteConnection.ClearAllPools();
	}
}
#if SQLSERVER
class BazaSqlServer : Baza
{
}
#endif
