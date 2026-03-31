using System;
using System.IO;
using Newtonsoft.Json;
using GtavModManager.Core;

namespace GtavModManager.Data
{
    public class SettingsRepository
    {
        private readonly string _filePath;

        public SettingsRepository(string filePath)
        {
            _filePath = filePath;
        }

        public AppSettings Load()
        {
            if (!File.Exists(_filePath))
                return new AppSettings();

            try
            {
                string json = File.ReadAllText(_filePath);
                var settings = JsonConvert.DeserializeObject<AppSettings>(json);
                return settings ?? new AppSettings();
            }
            catch (JsonException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsRepository] Corrupted JSON: {ex.Message}");
                return new AppSettings();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsRepository] Load error: {ex.Message}");
                return new AppSettings();
            }
        }

        public void Save(AppSettings settings)
        {
            try
            {
                string dir = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                string json = JsonConvert.SerializeObject(settings, Formatting.Indented,
                    new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

                string tmp = _filePath + ".tmp";
                File.WriteAllText(tmp, json);
                File.Copy(tmp, _filePath, overwrite: true);
                File.Delete(tmp);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsRepository] Save error: {ex.Message}");
                throw;
            }
        }
    }
}
