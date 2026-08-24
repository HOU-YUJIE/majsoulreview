using MajsoulReview.Models;

namespace MajsoulReview.Services;

public sealed class SettingsRepository
{
    public AppSettings Load()
    {
        AppPaths.EnsureCreated();
        var settings = JsonStorage.Load(AppPaths.SettingsFile, new AppSettings());
        settings.Normalize();
        return settings;
    }

    public void Save(AppSettings settings) => JsonStorage.Save(AppPaths.SettingsFile, settings);
}
