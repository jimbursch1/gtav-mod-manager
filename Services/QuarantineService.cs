using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GtavModManager.Core;

namespace GtavModManager.Services
{
    public class QuarantineService
    {
        private readonly FileOperationService _fileOps;
        private string _gtavRoot;
        private string _quarantineDir;

        public QuarantineService(FileOperationService fileOps)
        {
            _fileOps = fileOps;
        }

        public void Configure(string gtavRoot, string quarantineDir)
        {
            _gtavRoot = gtavRoot;
            _quarantineDir = quarantineDir;
        }

        public List<string> ValidateCanDisable(Mod mod, IReadOnlyList<Mod> allEnabledMods)
        {
            var blockers = new List<string>();
            foreach (var other in allEnabledMods)
            {
                if (other.Id == mod.Id) continue;
                if (other.Dependencies != null && other.Dependencies.Any(d => d.ModId == mod.Id))
                    blockers.Add(other.Name);
            }
            return blockers;
        }

        public OperationResult DisableMod(Mod mod)
        {
            if (string.IsNullOrEmpty(_gtavRoot))
                return OperationResult.Fail("GTA V root path not configured.");

            var moves = new List<(string src, string dst)>();
            foreach (var relativePath in mod.Files)
            {
                string src = Path.Combine(_gtavRoot, relativePath);
                string dst = Path.Combine(_quarantineDir, mod.Id, relativePath);
                moves.Add((src, dst));
            }

            var result = _fileOps.MoveFilesWithRollback(moves);
            if (result.Success)
                mod.Status = ModStatus.Disabled;

            return result;
        }

        public OperationResult EnableMod(Mod mod)
        {
            if (string.IsNullOrEmpty(_gtavRoot))
                return OperationResult.Fail("GTA V root path not configured.");

            var moves = new List<(string src, string dst)>();
            foreach (var relativePath in mod.Files)
            {
                string src = Path.Combine(_quarantineDir, mod.Id, relativePath);
                string dst = Path.Combine(_gtavRoot, relativePath);
                moves.Add((src, dst));
            }

            var result = _fileOps.MoveFilesWithRollback(moves);
            if (result.Success)
                mod.Status = ModStatus.Enabled;

            return result;
        }

        public string GetQuarantinePath(Mod mod)
        {
            return Path.Combine(_quarantineDir, mod.Id);
        }
    }
}
