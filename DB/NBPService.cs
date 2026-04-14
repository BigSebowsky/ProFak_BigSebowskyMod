using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProFak.DB;

static class NBPService
{
	private const int MaxDniWstecz = 30;
	private const int MaksDniNaZapytanie = 93;
	private static readonly HttpClient httpClient = new()
	{
		BaseAddress = new Uri("https://api.nbp.pl/api/exchangerates/rates/")
	};

	public static decimal? ZnajdzKurs(Baza baza, string walutaSkrot, DateTime data)
	{
		return ZnajdzKursZData(baza, walutaSkrot, data)?.KursSredni;
	}

	public static KursNBP? ZnajdzKursZData(Baza baza, string walutaSkrot, DateTime data)
	{
		if (String.IsNullOrWhiteSpace(walutaSkrot)) return null;
		var kodISO = Waluta.NormalizujKodISO(walutaSkrot);
		var waluta = baza.Waluty.FirstOrDefault(w => w.KodISO == kodISO);
		if (waluta == null || waluta.CzyDomyslna) return null;

		var szukana = data.Date.AddDays(-1);
		for (int i = 0; i < MaxDniWstecz; i++, szukana = szukana.AddDays(-1))
		{
			var kurs = baza.KursyNBP.FirstOrDefault(k => k.WalutaId == waluta.Id && k.Data == szukana);
			if (kurs != null) return kurs;
		}
		return null;
	}

	public static KursNBP? ZnajdzKursDlaWartosci(Baza baza, string walutaSkrot, DateTime data, decimal kursWaluty)
	{
		if (String.IsNullOrWhiteSpace(walutaSkrot)) return null;
		var kodISO = Waluta.NormalizujKodISO(walutaSkrot);
		var waluta = baza.Waluty.FirstOrDefault(w => w.KodISO == kodISO);
		if (waluta == null || waluta.CzyDomyslna) return null;

		var oczekiwanyKurs = kursWaluty.Zaokragl(4);
		var szukana = data.Date.AddDays(-1);
		for (int i = 0; i < MaxDniWstecz; i++, szukana = szukana.AddDays(-1))
		{
			var kurs = baza.KursyNBP.FirstOrDefault(k => k.WalutaId == waluta.Id && k.Data == szukana);
			if (kurs != null && kurs.KursSredni.Zaokragl(4) == oczekiwanyKurs) return kurs;
		}
		return null;
	}

	public static Task UzupelnijKursyAsync(Baza baza, DateTime odDnia, CancellationToken cancellationToken = default)
	{
		var od = odDnia.Date;
		var doDnia = DateTime.Today.AddDays(-1);
		if (od > doDnia) return Task.CompletedTask;
		return UzupelnijKursyAsync(baza, od, doDnia, cancellationToken);
	}

	public static async Task UzupelnijBrakujaceKursyAsync(Baza baza, CancellationToken cancellationToken = default)
	{
		await UzupelnijKursyAsync(baza, DateTime.Today.AddDays(-365), DateTime.Today.AddDays(-1), cancellationToken);
	}

	public static Task BackfillWalutyAsync(Baza baza, Waluta waluta, CancellationToken cancellationToken = default)
	{
		if (waluta.CzyDomyslna || String.IsNullOrWhiteSpace(waluta.KodISO)) return Task.CompletedTask;
		return PobierzZakresWalutyAsync(baza, waluta, DateTime.Today.AddDays(-365), DateTime.Today.AddDays(-1), cancellationToken);
	}

	private static async Task UzupelnijKursyAsync(Baza baza, DateTime od, DateTime doDnia, CancellationToken cancellationToken)
	{
		var waluty = baza.Waluty
			.Where(waluta => !waluta.CzyDomyslna && !String.IsNullOrWhiteSpace(waluta.Skrot))
			.AsEnumerable()
			.Where(waluta => !String.IsNullOrWhiteSpace(waluta.KodISO))
			.OrderBy(waluta => waluta.KodISO)
			.ToList();

		foreach (var waluta in waluty)
		{
			cancellationToken.ThrowIfCancellationRequested();
			await PobierzZakresWalutyAsync(baza, waluta, od, doDnia, cancellationToken);
		}
	}

	private static async Task PobierzZakresWalutyAsync(Baza baza, Waluta waluta, DateTime od, DateTime doDnia, CancellationToken cancellationToken)
	{
		if (od > doDnia) return;

		var istniejaceDaty = baza.KursyNBP
			.Where(kurs => kurs.WalutaId == waluta.Id && kurs.Data >= od.Date && kurs.Data <= doDnia.Date)
			.Select(kurs => kurs.Data)
			.ToHashSet();
		var noweKursy = new List<KursNBP>();

		for (var start = od.Date; start <= doDnia.Date; start = start.AddDays(MaksDniNaZapytanie))
		{
			cancellationToken.ThrowIfCancellationRequested();
			var koniec = start.AddDays(MaksDniNaZapytanie - 1);
			if (koniec > doDnia.Date) koniec = doDnia.Date;

			foreach (var kurs in await PobierzKursyAsync(waluta, start, koniec, cancellationToken))
			{
				if (istniejaceDaty.Contains(kurs.Data)) continue;
				noweKursy.Add(kurs);
				istniejaceDaty.Add(kurs.Data);
			}
		}

		if (noweKursy.Count > 0)
		{
			try
			{
				baza.Zapisz(noweKursy);
			}
			catch (DbUpdateException ex) when (CzyDuplikatKursu(ex))
			{
				foreach (var kurs in noweKursy)
				{
					try { baza.Zapisz([kurs]); }
					catch (DbUpdateException e2) when (CzyDuplikatKursu(e2)) { }
				}
			}
		}
	}

	private static bool CzyDuplikatKursu(DbUpdateException ex)
		=> ex.InnerException is SqliteException se
		&& se.SqliteErrorCode == 19
		&& se.Message.Contains("KursNBP.WalutaId", StringComparison.OrdinalIgnoreCase);

	private static async Task<List<KursNBP>> PobierzKursyAsync(Waluta waluta, DateTime od, DateTime doDnia, CancellationToken cancellationToken)
	{
		foreach (var tabela in new[] { "A", "B" })
		{
			var url = $"{tabela}/{waluta.KodISO.ToLowerInvariant()}/{od:yyyy-MM-dd}/{doDnia:yyyy-MM-dd}/?format=json";
			using var response = await httpClient.GetAsync(url, cancellationToken);
			if (response.StatusCode == HttpStatusCode.NotFound)
			{
				var fallback = await PobierzKursyDzienPoDniuAsync(tabela, waluta, od, doDnia, cancellationToken);
				if (fallback.Count > 0) return fallback;
				continue;
			}
			response.EnsureSuccessStatusCode();

			await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
			var payload = await JsonSerializer.DeserializeAsync<NBPKursResponse>(stream, cancellationToken: cancellationToken);
			if (payload?.Rates == null) return [];

			return payload.Rates
				.Where(rate => !String.IsNullOrWhiteSpace(rate.No))
				.Select(rate => new KursNBP
				{
					WalutaRef = waluta.Ref,
					Data = rate.EffectiveDate.Date,
					KursSredni = rate.Mid.Zaokragl(4),
					NumerTabeli = rate.No ?? ""
				})
				.OrderBy(kurs => kurs.Data)
				.ToList();
		}

		return [];
	}

	private static async Task<List<KursNBP>> PobierzKursyDzienPoDniuAsync(string tabela, Waluta waluta, DateTime od, DateTime doDnia, CancellationToken cancellationToken)
	{
		var wynik = new List<KursNBP>();

		for (var data = od.Date; data <= doDnia.Date; data = data.AddDays(1))
		{
			cancellationToken.ThrowIfCancellationRequested();

			var url = $"{tabela}/{waluta.KodISO.ToLowerInvariant()}/{data:yyyy-MM-dd}/?format=json";
			using var response = await httpClient.GetAsync(url, cancellationToken);
			if (response.StatusCode == HttpStatusCode.NotFound) continue;
			response.EnsureSuccessStatusCode();

			await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
			var payload = await JsonSerializer.DeserializeAsync<NBPKursResponse>(stream, cancellationToken: cancellationToken);
			if (payload?.Rates == null) continue;

			wynik.AddRange(payload.Rates
				.Where(rate => !String.IsNullOrWhiteSpace(rate.No))
				.Select(rate => new KursNBP
				{
					WalutaRef = waluta.Ref,
					Data = rate.EffectiveDate.Date,
					KursSredni = rate.Mid.Zaokragl(4),
					NumerTabeli = rate.No ?? ""
				}));
		}

		return wynik
			.OrderBy(kurs => kurs.Data)
			.ToList();
	}

	private sealed class NBPKursResponse
	{
		[JsonPropertyName("rates")]
		public List<NBPKursRate>? Rates { get; set; }
	}

	private sealed class NBPKursRate
	{
		[JsonPropertyName("no")]
		public string? No { get; set; }

		[JsonPropertyName("effectiveDate")]
		public DateTime EffectiveDate { get; set; }

		[JsonPropertyName("mid")]
		public decimal Mid { get; set; }
	}
}
