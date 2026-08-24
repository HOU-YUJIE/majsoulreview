using System.Text.Json;

namespace MajsoulReview.Services;

public static class JsonStorage
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static T Load<T>(string path, T fallback)
    {
        if (!File.Exists(path))
        {
            return fallback;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(File.ReadAllText(path), Options) ?? fallback;
        }
        catch (JsonException)
        {
            var backup = path + ".broken-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
            File.Copy(path, backup, overwrite: true);
            return fallback;
        }
    }

    public static void Save<T>(string path, T value)
    {
        var tempPath = path + ".tmp";
        File.WriteAllText(tempPath, JsonSerializer.Serialize(value, Options));
        File.Move(tempPath, path, overwrite: true);
    }
}
