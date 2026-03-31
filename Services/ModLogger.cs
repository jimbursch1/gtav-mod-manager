using System;
using System.IO;

namespace GtavModManager.Services
{
    /// <summary>
    /// Appends one line per file operation to operations.log.
    /// Format: 2026-03-31T18:00:00Z | OPERATION | detail | OK/FAIL reason
    /// Thread-safe for single-process use via lock.
    /// </summary>
    public class ModLogger
    {
        private readonly string _logPath;
        private readonly object _lock = new object();

        public ModLogger(string logPath)
        {
            _logPath = logPath;
            try { Directory.CreateDirectory(Path.GetDirectoryName(logPath)); }
            catch { }
        }

        public void Log(string operation, string detail, bool success, string reason = null)
        {
            string status = success ? "OK" : $"FAIL {reason}";
            string line = $"{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ} | {operation,-12} | {detail} | {status}";
            try
            {
                lock (_lock)
                    File.AppendAllText(_logPath, line + Environment.NewLine);
            }
            catch { } // never crash the app because logging failed
        }

        public void LogSection(string heading)
        {
            string line = $"{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ} | ---- {heading} ----";
            try
            {
                lock (_lock)
                    File.AppendAllText(_logPath, line + Environment.NewLine);
            }
            catch { }
        }
    }
}
