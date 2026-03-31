using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using GtavModManager.Core;

namespace GtavModManager.Data
{
    public class InventoryRepository
    {
        private readonly string _filePath;

        public InventoryRepository(string inventoryFolder)
        {
            _filePath = Path.Combine(inventoryFolder, "inventory.json");
        }

        public List<Mod> Load()
        {
            if (!File.Exists(_filePath))
                return new List<Mod>();

            try
            {
                string json = File.ReadAllText(_filePath);
                var mods = JsonConvert.DeserializeObject<List<Mod>>(json);
                return mods ?? new List<Mod>();
            }
            catch (JsonException ex)
            {
                BackupCorrupted(_filePath);
                System.Diagnostics.Debug.WriteLine($"[InventoryRepository] Corrupted JSON: {ex.Message}");
                return new List<Mod>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[InventoryRepository] Load error: {ex.Message}");
                return new List<Mod>();
            }
        }

        public void Save(List<Mod> mods)
        {
            try
            {
                string dir = Path.GetDirectoryName(_filePath);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                string json = JsonConvert.SerializeObject(mods, Formatting.Indented,
                    new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

                string tmp = _filePath + ".tmp";
                File.WriteAllText(tmp, json);
                File.Copy(tmp, _filePath, overwrite: true);
                File.Delete(tmp);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[InventoryRepository] Save error: {ex.Message}");
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
