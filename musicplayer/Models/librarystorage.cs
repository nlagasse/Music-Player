using System.IO;
using System.Text.Json;

// Storage of albums
namespace musicplayer.Models
{
    public static class LibraryStorage
    {
        private static readonly string AppFolderPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "musicplayer"
        );

        private static readonly string LibraryFilePath = Path.Combine(
            AppFolderPath,
            "library.json"
        );

        public static void SaveLibrary()
        {
            if (!Directory.Exists(AppFolderPath))
                Directory.CreateDirectory(AppFolderPath);

            JsonSerializerOptions options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            string json = JsonSerializer.Serialize(AppData.Library, options);

            File.WriteAllText(LibraryFilePath, json);
        }

        public static void LoadLibrary()
        {
            if (!File.Exists(LibraryFilePath))
                return;

            string json = File.ReadAllText(LibraryFilePath);

            MusicLibrary? loadedLibrary = JsonSerializer.Deserialize<MusicLibrary>(json);

            if (loadedLibrary != null)
                AppData.Library = loadedLibrary;
        }
    }
}