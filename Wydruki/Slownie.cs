using System.Text;

namespace ProFak.Wydruki;

class SlowniePL
{
	private readonly static string[] JEDNOSCI = { "zero", "jeden", "dwa", "trzy", "cztery", "pięć", "sześć", "siedem", "osiem", "dziewięć", "dziesięć", "jedenaście", "dwanaście", "trzynaście", "czternaście", "piętnaście", "szesnaście", "siedemnaście", "osiemnaście", "dziewiętnaście" };
	private readonly static string[] DZIESIATKI = { "", "", "dwadzieścia", "trzydzieści", "czterdzieści", "pięćdziesiąt", "sześćdziesiąt", "siedemdziesiąt", "osiemdziesiąt", "dziewięćdziesiąt" };
	private readonly static string[] SETKI = { "", "sto", "dwieście", "trzysta", "czterysta", "pięćset", "sześćset", "siedemset", "osiemset", "dziewięćset" };
	private readonly static string[][] TYSIACE = { new[] { "", "", "" }, new[] { " tysiąc", " tysiące", " tysięcy" }, new[] { " milion", " miliony", " milionów" }, new[] { " miliard", " miliardy", " miliardów" } };

	private static string SlownieDo1000(int wartosc)
	{
		if (wartosc == 0) return "";

		var wynik = new StringBuilder();

		if (wartosc >= 100)
		{
			if (wynik.Length > 0) wynik.Append(" ");
			wynik.Append(SETKI[wartosc / 100]);
			wartosc %= 100;
		}

		if (wartosc >= 20)
		{
			if (wynik.Length > 0) wynik.Append(" ");
			wynik.Append(DZIESIATKI[wartosc / 10]);
			wartosc %= 10;
		}

		if (wartosc > 0)
		{
			if (wynik.Length > 0) wynik.Append(" ");
			wynik.Append(JEDNOSCI[wartosc]);
		}

		return wynik.ToString();
	}

	private static void Slownie(StringBuilder wynik, long wartosc, int rzadWielkosci, long dzielnik)
	{
		if (wartosc >= 1000)
		{
			Slownie(wynik, wartosc / 1000, rzadWielkosci + 1, dzielnik * 1000);
			wartosc = wartosc % 1000;
		}

		if (wartosc == 0) return;

		if (wynik.Length > 0) wynik.Append(" ");

		wynik.Append(SlownieDo1000((int)wartosc));

		int odmiana;

		int ostatniaCyfra = (int)wartosc % 10;
		int dwieCyfry = (int)wartosc % 100;

		if (wartosc >= 100)
		{
			if (dwieCyfry >= 20)
			{
				if (ostatniaCyfra == 2) odmiana = 1;
				else if (ostatniaCyfra == 3) odmiana = 1;
				else if (ostatniaCyfra == 4) odmiana = 1;
				else odmiana = 2;
			}
			else if (dwieCyfry == 2) odmiana = 1;
			else if (dwieCyfry == 3) odmiana = 1;
			else if (dwieCyfry == 4) odmiana = 1;
			else odmiana = 2;
		}
		else if (wartosc >= 20)
		{
			if (ostatniaCyfra == 2) odmiana = 1;
			else if (ostatniaCyfra == 3) odmiana = 1;
			else if (ostatniaCyfra == 4) odmiana = 1;
			else odmiana = 2;
		}
		else if (wartosc >= 5) odmiana = 2;
		else if (wartosc >= 2) odmiana = 1;
		else odmiana = 0;

		wynik.Append(TYSIACE[rzadWielkosci][odmiana]);
	}

	public static string Slownie(long wartosc)
	{
		if (wartosc == 0) return JEDNOSCI[0];

		var wynik = new StringBuilder();
		Slownie(wynik, wartosc, 0, 1000);
		return wynik.ToString();
	}

	public static string Slownie(decimal kwota, string waluta)
	{
		var zlote = (long)Math.Floor(kwota);
		var grosze = (long)((kwota - zlote) * 100);
		var wynik = Slownie(zlote) + " " + waluta;
		if (grosze > 0) wynik += " i " + Slownie(grosze) + " " + (waluta == "zł" ? "gr" : waluta + "/100");
		return wynik;
	}
}

class SlownieEN
{
	private static readonly string[] Jednosci = { "zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine" };
	private static readonly string[] Nastki = { "ten", "eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen", "seventeen", "eighteen", "nineteen" };
	private static readonly string[] Dziesiatki = { "", "", "twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety" };
	private static readonly string[] Rzedy = { "", "thousand", "million", "billion" };

	private static string SlownieDo1000(int wartosc)
	{
		if (wartosc == 0) return "";

		var wynik = new StringBuilder();

		if (wartosc >= 100)
		{
			wynik.Append(Jednosci[wartosc / 100]);
			wynik.Append(" hundred");
			wartosc %= 100;
		}

		if (wartosc >= 20)
		{
			if (wynik.Length > 0) wynik.Append(" ");
			wynik.Append(Dziesiatki[wartosc / 10]);
			if (wartosc % 10 > 0)
			{
				wynik.Append(" ");
				wynik.Append(Jednosci[wartosc % 10]);
			}
		}
		else if (wartosc >= 10)
		{
			if (wynik.Length > 0) wynik.Append(" ");
			wynik.Append(Nastki[wartosc - 10]);
		}
		else if (wartosc > 0)
		{
			if (wynik.Length > 0) wynik.Append(" ");
			wynik.Append(Jednosci[wartosc]);
		}

		return wynik.ToString();
	}

	public static string Slownie(long wartosc)
	{
		if (wartosc == 0) return Jednosci[0];

		var czesci = new List<string>();
		var rzad = 0;

		while (wartosc > 0 && rzad < Rzedy.Length)
		{
			var fragment = (int)(wartosc % 1000);
			if (fragment > 0)
			{
				var opis = SlownieDo1000(fragment);
				if (!String.IsNullOrEmpty(Rzedy[rzad])) opis += " " + Rzedy[rzad];
				czesci.Insert(0, opis);
			}

			wartosc /= 1000;
			rzad++;
		}

		return String.Join(" ", czesci);
	}

	public static string Slownie(decimal kwota, string waluta)
	{
		var cale = (long)Math.Floor(kwota);
		var setne = (int)((kwota - cale) * 100);
		var wynik = Slownie(cale) + " " + waluta;
		if (setne > 0) wynik += " and " + Slownie(setne) + " " + (waluta == "zł" ? "grosz" : waluta + "/100");
		return wynik;
	}
}
