using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using GtavModManager.Core;

namespace GtavModManager.Data
{
    public class SnapshotRepository
    {
        private readonly string _filePath;

        public SnapshotRepository(string inventoryFolder)
        {
            _filePath = Path.Combine(inventoryFolder, "snapshots.json");
        }

        public List<GameVersionSnapshot> Load()
        {
            if (!File.Exists(_filePath))
                return new List<GameVersionSnapshot>();

            try
            {
                string json = File.ReadAllText(_filePath);
                var snapshots = JsonConvert.DeserializeObject<List<GameVersionSnapshot>>(json);
                return snapshots ?? new List<GameVersionSnapshot>();
            }
            catch (JsonException ex)
            {
                BackupCorrupted(_filePath);
                System.Diagnostics.Debug.WriteLine($"[SnapshotRepository] Corrupted JSON: {ex.Message}");
                return new List<GameVersionSnapshot>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SnapshotRepository] Load error: {ex.Message}");
                return new List<GameVersionSnapshot>();
            }
        }

        public void Save(List<GameVersionSnapshot> snapshots)
        {
            try
            {
                string dir = Path.GetDirectoryName(_filePath);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                string json = JsonConvert.SerializeObject(snapshots, Formatting.Indented,
                    new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

                string tmp = _filePath + ".tmp";
                File.WriteAllText(tmp, json);
                File.Copy(tmp, _filePath, overwrite: true);
                File.Delete(tmp);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SnapshotRepository] Save error: {ex.Message}");
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
