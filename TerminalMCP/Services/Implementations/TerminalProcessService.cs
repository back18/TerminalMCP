using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;
using TerminalMCP.Interop;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TerminalMCP.Services.Implementations
{
    public class TerminalProcessService : ITerminalProcessService
    {
        public TerminalProcessService(ILogger<TerminalProcessService> logger)
        {
            ArgumentNullException.ThrowIfNull(logger, nameof(logger));

            _logger = logger;
            _profiles = LoadProfiles();
        }

        private readonly ILogger<TerminalProcessService> _logger;
        private readonly Dictionary<string, ProfileInfo> _profiles;

        public IReadOnlyList<string> GetProfiles()
        {
            if (_profiles.Count == 0)
                return ["命令行终端", "Windows PowerShell"];

            return _profiles.Keys.ToArray();
        }

        public nint Open(string profileName, string? workingDirectory)
        {
            ArgumentException.ThrowIfNullOrEmpty(profileName, nameof(profileName));

            if (!string.IsNullOrEmpty(workingDirectory) && !Directory.Exists(workingDirectory))
                throw new DirectoryNotFoundException($"Working directory '{workingDirectory}' does not exist.");

            string profile = _profiles.GetValueOrDefault(profileName)?.Guid ?? profileName;
            return LaunchTerminal(profile, workingDirectory);
        }

        private nint LaunchTerminal(string profile, string? workingDirectory)
        {
            try
            {
                StringBuilder stringBuilder = new();
                stringBuilder.Append("-p ");
                stringBuilder.AppendFormat("\"{0}\"", profile);

                if (!string.IsNullOrEmpty(workingDirectory))
                {
                    stringBuilder.Append(" -d ");
                    stringBuilder.AppendFormat("\"{0}\"", workingDirectory);
                }

                ProcessStartInfo startInfo = new()
                {
                    FileName = "wt.exe",
                    Arguments = stringBuilder.ToString(),
                    UseShellExecute = true,
                    CreateNoWindow = true,
                };

                HashSet<nint> beforeHwnds = EnumTerminalWindows();

                Process? process = Process.Start(startInfo);
                if (process is null)
                {
                    _logger.LogError("LaunchTerminal: Process.Start returned null for profile={profile}", profile);
                    return nint.Zero;
                }

                try
                {
                    for (int i = 0; i < 10; i++)
                    {
                        Thread.Sleep(500);

                        HashSet<nint> afterHwnds = EnumTerminalWindows();
                        afterHwnds.ExceptWith(beforeHwnds);

                        if (afterHwnds.Count > 0)
                        {
                            nint hwnd = afterHwnds.First();

                            if (_logger.IsEnabled(LogLevel.Information))
                                _logger.LogInformation("LaunchTerminal: detected new terminal window 0x{hwnd:X} after {attempt} attempts", (ulong)hwnd, i + 1);

                            return hwnd;
                        }
                    }
                }
                finally
                {
                    process.Dispose();
                }

                _logger.LogWarning("LaunchTerminal: no new terminal window detected within timeout, profile={profile}", profile);
                return nint.Zero;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "LaunchTerminal failed for profile={profile}", profile);
                return nint.Zero;
            }
        }

        private static HashSet<nint> EnumTerminalWindows()
        {
            HashSet<nint> result = [];
            NativeMethods.EnumWindows((hWnd, _) =>
            {
                char[] buffer = new char[256];
                int len = NativeMethods.GetClassNameW(hWnd, buffer, buffer.Length);
                if (len > 0 && new string(buffer, 0, len) == NativeMethods.WtClassName)
                    result.Add(hWnd);

                return true;
            },
            nint.Zero);

            return result;
        }

        private Dictionary<string, ProfileInfo> LoadProfiles()
        {
            Dictionary<string, ProfileInfo> result = [];
            string? settingsPath = FindSettingsPath();

            if (string.IsNullOrEmpty(settingsPath))
            {
                _logger.LogWarning("LoadProfiles: settings.json not found");
                return result;
            }

            try
            {
                string json = File.ReadAllText(settingsPath, Encoding.UTF8);
                JObject jObject = JObject.Parse(json);

                if (jObject["profiles"]?["list"] is not JArray jArray)
                {
                    _logger.LogWarning("LoadProfiles: no 'profiles.list' array in settings.json");
                    return result;
                }

                List<ProfileInfo> profiles = jArray.ToObject<List<ProfileInfo>>() ?? [];
                foreach (ProfileInfo profile in profiles)
                    result[profile.Name] = profile;

                if (_logger.IsEnabled(LogLevel.Information))
                    _logger.LogInformation("LoadProfiles: loaded {count} profiles", result.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "LoadProfiles: failed to parse settings.json");
            }

            return result;
        }

        private static string? FindSettingsPath()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string packages = Path.Combine(appData, "Packages");

            // Packaged (Store) install: enumerate subdirectories matching the pattern
            if (Directory.Exists(packages))
            {
                string settings = Path.Combine(packages, "Microsoft.WindowsTerminal_8wekyb3d8bbwe", "LocalState", "settings.json");
                if (File.Exists(settings))
                    return settings;

                string[] matching = Directory.GetDirectories(packages, "Microsoft.WindowsTerminal_*");
                if (matching.Length > 0)
                {
                    settings = Path.Combine(matching[0], "LocalState", "settings.json");
                    if (File.Exists(settings))
                        return settings;
                }
            }

            // Unpackaged (scoop/choco/msi) install
            string unpackaged = Path.Combine(appData, "Microsoft", "Windows Terminal", "settings.json");
            if (File.Exists(unpackaged))
                return unpackaged;

            return null;
        }

        private record ProfileInfo(
            [property: JsonProperty("name")] string Name,
            [property: JsonProperty("guid")] string Guid,
            [property: JsonProperty("commandline")] string? CommandLine,
            [property: JsonProperty("source")] string? Source
        );
    }
}
