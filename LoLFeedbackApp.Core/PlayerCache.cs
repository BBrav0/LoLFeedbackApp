using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace LoLFeedbackApp.Core
{
    public class PlayerCache
    {
        private static readonly string CacheFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "LoLFeedbackApp",
            "player_cache.json"
        );

        public class CacheData
        {
            public string Puuid { get; set; } = string.Empty;
            public string GameName { get; set; } = string.Empty;
            public string TagLine { get; set; } = string.Empty;
            public DateTime LastUpdated { get; set; }
        }

        public static async Task SaveCacheDataAsync(string puuid, string gameName, string tagLine)
        {
            var cacheData = new CacheData
            {
                Puuid = puuid,
                GameName = gameName,
                TagLine = tagLine,
                LastUpdated = DateTime.UtcNow
            };

            // Ensure directory exists
            Directory.CreateDirectory(Path.GetDirectoryName(CacheFilePath)!);

            var json = JsonSerializer.Serialize(cacheData, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(CacheFilePath, json);
        }

        public static async Task<CacheData?> LoadCacheDataAsync()
        {
            try
            {
                if (!File.Exists(CacheFilePath))
                    return null;

                var json = await File.ReadAllTextAsync(CacheFilePath);
                return JsonSerializer.Deserialize<CacheData>(json);
            }
            catch
            {
                return null;
            }
        }

        public static bool IsCacheValid(CacheData? cacheData)
        {
            if (cacheData == null)
                return false;

            // Consider cache valid if it's less than 24 hours old
            return (DateTime.UtcNow - cacheData.LastUpdated).TotalHours < 24;
        }
    }
} 