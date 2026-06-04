namespace NuclearOptionSDK.Bridge;

public static class AppVersion
{
    public const string ReleaseBase = "0.7.0";
    public const string VersionChannel = "PR-R";
    public const int CycleBuildNumber = 2;
    public const string ChangeLetters = "VP";
    public const int SubNumber = 1;

    public static string BuildToken => $"{VersionChannel}{CycleBuildNumber}{ChangeLetters}{SubNumber}";
    public static string Display => $"{ReleaseBase} Build {BuildToken}";
}
