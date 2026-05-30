using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BrowseSafe
{
    /// <summary>How a report email is composed.</summary>
    public enum EmailMethod
    {
        DefaultMailApp,   // Simple MAPI - opens the default desktop mail client
        Gmail,            // Gmail web compose
        OutlookWeb,       // Outlook on the web compose
    }

    /// <summary>Which browser to open web-based mail compose in.</summary>
    public enum BrowserChoice
    {
        Default,
        Chrome,
        Firefox,
        Edge,
    }

    /// <summary>User preferences persisted to %LOCALAPPDATA%\BrowseSafe\settings.json.</summary>
    public sealed class AppSettings
    {
        public EmailMethod EmailMethod { get; set; } = EmailMethod.DefaultMailApp;
        public BrowserChoice EmailBrowser { get; set; } = BrowserChoice.Default;

        private static readonly JsonSerializerOptions Opts = new()
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() },
        };

        private static string FilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BrowseSafe", "settings.json");

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(FilePath))
                    return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), Opts) ?? new AppSettings();
            }
            catch { /* fall back to defaults */ }
            return new AppSettings();
        }

        public void Save()
        {
            try
            {
                string dir = Path.GetDirectoryName(FilePath)!;
                Directory.CreateDirectory(dir);
                File.WriteAllText(FilePath, JsonSerializer.Serialize(this, Opts));
            }
            catch { /* non-fatal */ }
        }
    }
}
