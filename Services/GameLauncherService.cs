using System;
using System.Diagnostics;
using System.IO;

namespace GtavModManager.Services
{
    public enum LaunchMethod { RagePluginHook, Direct, Steam }

    public class GameLauncherService
    {
        private string _gtavRoot;

        public void Configure(string gtavRoot)
        {
            _gtavRoot = gtavRoot;
        }

        public bool CanLaunch(LaunchMethod method)
        {
            switch (method)
            {
                case LaunchMethod.RagePluginHook:
                    return File.Exists(RphPath);
                case LaunchMethod.Direct:
                    return File.Exists(GtaExePath);
                case LaunchMethod.Steam:
                    return true; // steam:// URIs always "work" if Steam is installed
                default:
                    return false;
            }
        }

        public OperationResult Launch(LaunchMethod method)
        {
            try
            {
                switch (method)
                {
                    case LaunchMethod.RagePluginHook:
                        if (!File.Exists(RphPath))
                            return OperationResult.Fail($"RAGEPluginHook.exe not found at:\n{RphPath}");
                        Process.Start(new ProcessStartInfo(RphPath) { WorkingDirectory = _gtavRoot });
                        return OperationResult.Ok();

                    case LaunchMethod.Direct:
                        if (!File.Exists(GtaExePath))
                            return OperationResult.Fail($"GTA5.exe not found at:\n{GtaExePath}");
                        Process.Start(new ProcessStartInfo(GtaExePath) { WorkingDirectory = _gtavRoot });
                        return OperationResult.Ok();

                    case LaunchMethod.Steam:
                        Process.Start("steam://run/271590");
                        return OperationResult.Ok();

                    default:
                        return OperationResult.Fail("Unknown launch method.");
                }
            }
            catch (Exception ex)
            {
                return OperationResult.Fail($"Failed to launch: {ex.Message}");
            }
        }

        private string RphPath => string.IsNullOrEmpty(_gtavRoot)
            ? "" : Path.Combine(_gtavRoot, "RAGEPluginHook.exe");

        private string GtaExePath => string.IsNullOrEmpty(_gtavRoot)
            ? "" : Path.Combine(_gtavRoot, "GTA5.exe");
    }
}
