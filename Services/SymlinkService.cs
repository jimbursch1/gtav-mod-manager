using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace GtavModManager.Services
{
    /// <summary>
    /// Creates and removes NTFS junctions and hard links for non-destructive mod enabling/disabling.
    ///
    /// Strategy:
    ///   Directories → NTFS junctions (no admin rights required)
    ///   Files        → hard links (no admin rights required; must be same drive)
    ///   Fallback     → file move (if hard link fails, e.g. cross-drive)
    /// </summary>
    public class SymlinkService
    {
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CreateHardLink(string lpFileName, string lpExistingFileName, IntPtr lpSecurityAttributes);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool RemoveDirectory(string lpPathName);

        /// <summary>
        /// Creates an NTFS junction (directory reparse point) at <paramref name="junctionPath"/>
        /// pointing to <paramref name="targetPath"/>. No admin rights required.
        /// </summary>
        public bool CreateJunction(string junctionPath, string targetPath)
        {
            if (Directory.Exists(junctionPath))
            {
                if (IsReparsePoint(junctionPath))
                    return true; // already a junction to this target
                return false; // real directory exists — refuse to overwrite
            }

            try
            {
                // Use cmd /c mklink /J — most reliable cross-version approach for .NET 4.8
                var psi = new ProcessStartInfo("cmd.exe", $"/c mklink /J \"{junctionPath}\" \"{targetPath}\"")
                {
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using (var p = Process.Start(psi))
                {
                    p.WaitForExit(5000);
                    return p.ExitCode == 0;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Symlink] CreateJunction failed {junctionPath}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Removes a junction directory. Does NOT delete the target.
        /// </summary>
        public bool DeleteJunction(string junctionPath)
        {
            if (!Directory.Exists(junctionPath)) return true;
            if (!IsReparsePoint(junctionPath)) return false; // safety: refuse to delete real directory

            try
            {
                // RemoveDirectory on a junction removes only the junction, not the target
                return RemoveDirectory(junctionPath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Symlink] DeleteJunction failed {junctionPath}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Creates a hard link at <paramref name="linkPath"/> pointing to <paramref name="targetPath"/>.
        /// Both paths must be on the same volume. No admin rights required.
        /// Returns false if hard link creation fails (caller should fall back to file move).
        /// </summary>
        public bool CreateHardLink(string linkPath, string targetPath)
        {
            try
            {
                EnsureParentDirectory(linkPath);
                return CreateHardLink(linkPath, targetPath, IntPtr.Zero);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Symlink] CreateHardLink failed {linkPath}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Deletes a hard link (or regular file). Safe to call on hard links — does not affect other links
        /// to the same data until the last link is removed.
        /// </summary>
        public bool DeleteLink(string linkPath)
        {
            try
            {
                if (File.Exists(linkPath))
                {
                    File.Delete(linkPath);
                    return true;
                }
                return true; // already gone
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Symlink] DeleteLink failed {linkPath}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Returns true if <paramref name="path"/> is an NTFS junction or symbolic link.
        /// </summary>
        public bool IsReparsePoint(string path)
        {
            try
            {
                if (Directory.Exists(path))
                    return (new DirectoryInfo(path).Attributes & FileAttributes.ReparsePoint) != 0;
                if (File.Exists(path))
                    return (new FileInfo(path).Attributes & FileAttributes.ReparsePoint) != 0;
                return false;
            }
            catch
            {
                return false;
            }
        }

        private static void EnsureParentDirectory(string path)
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }
    }
}
