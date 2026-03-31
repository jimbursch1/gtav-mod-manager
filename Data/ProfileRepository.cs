using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using GtavModManager.Core;

namespace GtavModManager.Data
{
    public class ProfileRepository
    {
        private readonly string _filePath;

        public ProfileRepository(string inventoryFolder)
        {
            _filePath = Path.Combine(inventoryFolder, "profiles.json");
        }

        public List<Profile> Load()
        {
            if (!File.Exists(_filePath))
                return new List<Profile>();

            try
            {
                string json = File.ReadAllText(_filePath);
                var profiles = JsonConvert.DeserializeObject<List<Profile>>(json);
                return profiles ?? new List<Profile>();
            }
            catch (JsonException ex)
            {
                BackupCorrupted(_filePath);
                System.Diagnostics.Debug.WriteLine($"[ProfileRepository] Corrupted JSON: {ex.Message}");
                return new List<Profile>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ProfileRepository] Load error: {ex.Message}");
                return new List<Profile>();
            }
        }

        public void Save(List<Profile> profiles)
        {
            try
            {
                string dir = Path.GetDirectoryName(_filePath);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                string json = JsonConvert.SerializeObject(profiles, Formatting.Indented,
                    new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

                string tmp = _filePath + ".tmp";
                File.WriteAllText(tmp, json);
                File.Copy(tmp, _filePath, overwrite: true);
                File.Delete(tmp);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ProfileRepository] Save error: {ex.Message}");
                throw;
            }
        }

        private static void BackupCorrupted(string path)
        {
            try
            {
                string backup = path + ".corrupted." + DateTime.UtcNow.ToString("yyyyMMddHHmmss");
                File.Copy(path, backup, overwrite: true);
            }
            catch { }
        }
    }
}
