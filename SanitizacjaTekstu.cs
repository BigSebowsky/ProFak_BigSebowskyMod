using System.Text;
using System.Xml;

namespace ProFak;

static class SanitizacjaTekstu
{
	public static string? UsunNiedozwoloneZnakiXml(string? tekst)
	{
		if (String.IsNullOrEmpty(tekst)) return tekst;
		StringBuilder? builder = null;

		for (var i = 0; i < tekst.Length; i++)
		{
			var znak = tekst[i];
			if (XmlConvert.IsXmlChar(znak))
			{
				builder?.Append(znak);
				continue;
			}

			builder ??= new StringBuilder(tekst.Length);
			if (builder.Length == 0 && i > 0) builder.Append(tekst, 0, i);
		}

		return builder?.ToString() ?? tekst;
	}
}
