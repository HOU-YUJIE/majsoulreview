namespace MajsoulReview.Services;

public static class AppPaths
{
    public static string Root { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MajsoulReview");

    public static string Images { get; } = Path.Combine(Root, "images");
    public static string CardsFile { get; } = Path.Combine(Root, "cards.json");
    public static string SettingsFile { get; } = Path.Combine(Root, "settings.json");

    public static void EnsureCreated()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(Images);
    }
}
