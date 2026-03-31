using System;
using System.Collections.Generic;
using System.IO;

namespace GtavModManager.Services
{
    public class OperationResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }

        public static OperationResult Ok() => new OperationResult { Success = true };
        public static OperationResult Fail(string message) => new OperationResult { Success = false, ErrorMessage = message };
    }

    public class FileOperationService
    {
        public void EnsureDirectory(string path)
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
        }

        public bool MoveFile(string source, string destination)
        {
            try
            {
                EnsureDirectory(Path.GetDirectoryName(destination));
                File.Move(source, destination);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FileOp] MoveFile failed {source} -> {destination}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Moves all files in the list. On any failure, reverses all completed moves and returns Failed.
        /// </summary>
        public OperationResult MoveFilesWithRollback(List<(string src, string dst)> moves)
        {
            var completed = new List<(string src, string dst)>();

            foreach (var (src, dst) in moves)
            {
                if (!File.Exists(src))
                {
                    Rollback(completed);
                    return OperationResult.Fail($"Source file not found: {src}");
                }

                if (!MoveFile(src, dst))
                {
                    Rollback(completed);
                    return OperationResult.Fail($"Failed to move: {src}");
                }

                completed.Add((src, dst));
            }

            return OperationResult.Ok();
        }

        private void Rollback(List<(string src, string dst)> completed)
        {
            for (int i = completed.Count - 1; i >= 0; i--)
            {
                var (src, dst) = completed[i];
                try
                {
                    if (File.Exists(dst))
                    {
                        EnsureDirectory(Path.GetDirectoryName(src));
                        File.Move(dst, src);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[FileOp] Rollback failed {dst} -> {src}: {ex.Message}");
                }
            }
        }
    }
}
