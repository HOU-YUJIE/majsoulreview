using System.Text.Json.Serialization;

namespace MajsoulReview.Models;

public sealed class AppSettings
{
    private static readonly string[] DefaultCategories =
        ["牌效", "押引", "副露", "立直判断", "防守", "点数判断", "其他"];

    public string Hotkey { get; set; } = "F8";
    public bool UseControl { get; set; }
    public bool UseAlt { get; set; }
    public bool UseShift { get; set; }
    public bool UseWindows { get; set; }
    public NormalizedCrop? LastCrop { get; set; }
    public List<string> Categories { get; set; } = [.. DefaultCategories];

    [JsonIgnore]
    public string HotkeyDisplay
    {
        get
        {
            var parts = new List<string>();
            if (UseControl) parts.Add("Ctrl");
            if (UseAlt) parts.Add("Alt");
            if (UseShift) parts.Add("Shift");
            if (UseWindows) parts.Add("Win");
            parts.Add(FormatKeyName(Hotkey));
            return string.Join(" + ", parts);
        }
    }

    public void Normalize()
    {
        Categories = Categories
            .Select(category => category.Trim())
            .Where(category => !string.IsNullOrWhiteSpace(category))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (Categories.Count == 0)
        {
            Categories = [.. DefaultCategories];
        }
    }

    private static string FormatKeyName(string key) => key switch
    {
        "D0" => "0",
        "D1" => "1",
        "D2" => "2",
        "D3" => "3",
        "D4" => "4",
        "D5" => "5",
        "D6" => "6",
        "D7" => "7",
        "D8" => "8",
        "D9" => "9",
        "OemPlus" => "+",
        "OemMinus" => "-",
        "OemComma" => ",",
        "OemPeriod" => ".",
        "OemQuestion" => "/",
        "OemSemicolon" => ";",
        "OemQuotes" => "'",
        "OemOpenBrackets" => "[",
        "OemCloseBrackets" => "]",
        "OemPipe" => "\\",
        "OemTilde" => "`",
        _ => key
    };
}

public sealed record NormalizedCrop(double X, double Y, double Width, double Height);
