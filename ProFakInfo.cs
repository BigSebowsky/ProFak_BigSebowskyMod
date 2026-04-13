using System.Reflection;

namespace ProFak;

internal static class ProFakInfo
{
	public const string ProductName = "ProFak BigSebowskyMod";
	public const string RepositoryUrl = "https://github.com/BigSebowsky/ProFak_BigSebowskyMod";
	public const string ReleasesLatestApiUrl = "https://api.github.com/repos/BigSebowsky/ProFak_BigSebowskyMod/releases/latest";
	public const string IssuesUrl = "https://github.com/BigSebowsky/ProFak_BigSebowskyMod/issues";
	public const string OriginalRepositoryUrl = "https://github.com/lkosson/profak";
	public static string UserAgent => $"{ProductName} ({RepositoryUrl})";
	public static string InformationalVersion =>
		typeof(ProFakInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
		?? typeof(ProFakInfo).Assembly.GetName().Version?.ToString()
		?? "0.0.0";
}
