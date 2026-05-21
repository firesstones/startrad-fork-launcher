using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;

namespace StarTradForLauncher
{
    internal sealed class LibraryCandidate
    {
        public string PathValue;
        public string Source;
        public bool Valid;
    }

    internal sealed class StarCitizenInstall
    {
        public string Channel;
        public string ChannelPath;
        public string LibraryPath;
        public string GameVersion;
        public string GameBranch;
        public string GameBuild;
        public string GameBuildDate;
        public bool TranslationInstalled;
        public string TranslationVersion;
        public string LatestTranslationVersion;
        public string GlobalIniPath;
        public string UserCfgPath;
        public bool UserCfgUsesFrench;
        public string Source;
    }

    internal sealed class DetectionResult
    {
        public List<LibraryCandidate> Libraries = new List<LibraryCandidate>();
        public List<StarCitizenInstall> Installs = new List<StarCitizenInstall>();
        public DateTime CheckedAt;
    }

    internal static class StarTradCore
    {
        public const string Version = "1.0.0";

        private const string TranslationHost = "https://traduction.circuspes.fr";
        private const string FrenchLanguageLine = "g_language = french_(france)";

        public static string AppDataDir
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Circus",
                    "StarTradForLauncher"
                );
            }
        }

        public static string ManualLibrariesFile
        {
            get { return Path.Combine(AppDataDir, "libraries.txt"); }
        }

        public static DetectionResult Detect()
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            DetectionResult result = new DetectionResult();
            result.CheckedAt = DateTime.Now;

            List<LibraryCandidate> libraries = ListLibraryCandidates();
            foreach (LibraryCandidate library in libraries)
            {
                library.Valid = Directory.Exists(Path.Combine(library.PathValue, "StarCitizen"));
                result.Libraries.Add(library);

                List<string> channels = FindChannelPathsUnderLibrary(library.PathValue);
                foreach (string channelPath in channels)
                {
                    if (result.Installs.Any(i => SamePath(i.ChannelPath, channelPath)))
                    {
                        continue;
                    }

                    StarCitizenInstall install = BuildInstall(library, channelPath);
                    result.Installs.Add(install);
                }
            }

            result.Installs = SortInstalls(result.Installs);
            FillLatestTranslationVersions(result.Installs);
            return result;
        }

        public static string NormalizeLibrarySelection(string selectedPath)
        {
            if (string.IsNullOrWhiteSpace(selectedPath))
            {
                return null;
            }

            string normalized = CleanPath(selectedPath);
            string fromStarCitizen = LibraryFromStarCitizenPath(normalized);
            if (!string.IsNullOrWhiteSpace(fromStarCitizen))
            {
                return Path.GetFullPath(fromStarCitizen);
            }

            if (String.Equals(Path.GetFileName(normalized), "StarCitizen", StringComparison.OrdinalIgnoreCase))
            {
                return Path.GetFullPath(Path.GetDirectoryName(normalized));
            }

            return Path.GetFullPath(normalized);
        }

        public static void AddManualLibrary(string selectedPath)
        {
            string library = NormalizeLibrarySelection(selectedPath);
            if (string.IsNullOrWhiteSpace(library))
            {
                throw new InvalidOperationException("Dossier Star Citizen invalide.");
            }

            Directory.CreateDirectory(AppDataDir);
            List<string> entries = ReadManualLibraries();
            if (!entries.Any(e => SamePath(e, library)))
            {
                entries.Add(library);
            }
            File.WriteAllLines(ManualLibrariesFile, entries.ToArray());
        }

        public static void RemoveManualLibrary(string libraryPath)
        {
            if (!File.Exists(ManualLibrariesFile))
            {
                return;
            }

            List<string> entries = ReadManualLibraries()
                .Where(e => !SamePath(e, libraryPath))
                .ToList();
            File.WriteAllLines(ManualLibrariesFile, entries.ToArray());
        }

        public static void InstallTranslation(StarCitizenInstall install)
        {
            if (install == null)
            {
                throw new ArgumentNullException("install");
            }

            string route = String.Equals(install.Channel, "LIVE", StringComparison.OrdinalIgnoreCase)
                ? "/download/global.ini"
                : "/download_ptu/global.ini";
            string url = TranslationHost + route;
            string tempDir = Path.Combine(Path.GetTempPath(), "StarTradForLauncher");
            string tempFile = Path.Combine(tempDir, install.Channel + "-" + DateTime.Now.Ticks + "-global.ini");

            Directory.CreateDirectory(tempDir);
            using (WebClient client = NewWebClient())
            {
                client.DownloadFile(url, tempFile);
            }

            FileInfo downloaded = new FileInfo(tempFile);
            if (!downloaded.Exists || downloaded.Length <= 0)
            {
                throw new InvalidOperationException("Le fichier global.ini telecharge est vide.");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(install.GlobalIniPath));
            File.Copy(tempFile, install.GlobalIniPath, true);
            TryDelete(tempFile);
            CreateOrUpdateUserCfg(install.UserCfgPath);
        }

        public static void UninstallTranslation(StarCitizenInstall install)
        {
            if (install == null)
            {
                throw new ArgumentNullException("install");
            }

            if (File.Exists(install.GlobalIniPath))
            {
                File.Delete(install.GlobalIniPath);
                RemoveEmptyParents(Path.GetDirectoryName(install.GlobalIniPath), install.ChannelPath);
            }

            RemoveFrenchLanguageFromUserCfg(install.UserCfgPath);
        }

        public static void OpenPath(string targetPath)
        {
            if (string.IsNullOrWhiteSpace(targetPath))
            {
                return;
            }

            ProcessStartInfo info = new ProcessStartInfo();
            info.FileName = "explorer.exe";
            info.Arguments = "\"" + targetPath + "\"";
            info.UseShellExecute = true;
            Process.Start(info);
        }

        private static WebClient NewWebClient()
        {
            WebClient client = new WebClient();
                client.Headers.Add("User-Agent", "StarTradForkLauncher/" + Version);
            return client;
        }

        private static void FillLatestTranslationVersions(List<StarCitizenInstall> installs)
        {
            string live = null;
            string ptu = null;

            foreach (StarCitizenInstall install in installs)
            {
                bool isLive = String.Equals(install.Channel, "LIVE", StringComparison.OrdinalIgnoreCase);
                if (isLive && live == null)
                {
                    live = QueryLatestTranslationVersion(true);
                }
                if (!isLive && ptu == null)
                {
                    ptu = QueryLatestTranslationVersion(false);
                }
                install.LatestTranslationVersion = isLive ? live : ptu;
            }
        }

        private static string QueryLatestTranslationVersion(bool live)
        {
            string route = live ? "/download/version.html" : "/download_ptu/version.html";
            try
            {
                using (WebClient client = NewWebClient())
                {
                    return (client.DownloadString(TranslationHost + route) ?? "").Trim();
                }
            }
            catch
            {
                return null;
            }
        }

        private static StarCitizenInstall BuildInstall(LibraryCandidate library, string channelPath)
        {
            string globalIniPath = Path.Combine(channelPath, "data", "Localization", "french_(france)", "global.ini");
            string userCfgPath = Path.Combine(channelPath, "user.cfg");

            Dictionary<string, string> manifest = ParseGameVersion(channelPath);
            StarCitizenInstall install = new StarCitizenInstall();
            install.Channel = Path.GetFileName(channelPath);
            install.ChannelPath = channelPath;
            install.LibraryPath = library.PathValue;
            install.GameVersion = GetValue(manifest, "Version");
            install.GameBranch = GetValue(manifest, "Branch");
            install.GameBuild = GetValue(manifest, "RequestedP4ChangeNum");
            install.GameBuildDate = GetValue(manifest, "BuildDateStamp");
            install.TranslationVersion = ReadTranslationVersion(globalIniPath);
            install.TranslationInstalled = install.TranslationVersion != null;
            install.GlobalIniPath = globalIniPath;
            install.UserCfgPath = userCfgPath;
            install.UserCfgUsesFrench = UserCfgUsesFrench(userCfgPath);
            install.Source = library.Source;
            return install;
        }

        private static string GetValue(Dictionary<string, string> values, string key)
        {
            string value;
            return values.TryGetValue(key, out value) ? value : null;
        }

        private static Dictionary<string, string> ParseGameVersion(string channelPath)
        {
            Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string manifest = Path.Combine(channelPath, "build_manifest.id");
            if (!File.Exists(manifest))
            {
                return values;
            }

            string raw = ReadTextSafe(manifest);
            foreach (string key in new[] { "Version", "Branch", "RequestedP4ChangeNum", "BuildDateStamp" })
            {
                Match match = Regex.Match(raw, "\"" + Regex.Escape(key) + "\"\\s*:\\s*\"([^\"]*)\"", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    values[key] = match.Groups[1].Value.Trim();
                }
            }
            return values;
        }

        private static List<LibraryCandidate> ListLibraryCandidates()
        {
            List<LibraryCandidate> candidates = new List<LibraryCandidate>();
            AddManualCandidates(candidates);
            AddStarTradSettingsCandidates(candidates);
            AddRsiLauncherCandidates(candidates);
            AddLauncherLogCandidates(candidates);
            AddLauncherStoreCandidates(candidates);
            AddCommonCandidates(candidates);
            return candidates;
        }

        private static void AddManualCandidates(List<LibraryCandidate> candidates)
        {
            foreach (string library in ReadManualLibraries())
            {
                UniquePush(candidates, library, "Dossier manuel");
            }
        }

        private static List<string> ReadManualLibraries()
        {
            if (!File.Exists(ManualLibrariesFile))
            {
                return new List<string>();
            }

            return File.ReadAllLines(ManualLibrariesFile)
                .Select(l => CleanPath(l))
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static void AddRsiLauncherCandidates(List<LibraryCandidate> candidates)
        {
            foreach (string launcherFolder in FindRsiLauncherFolderCandidates())
            {
                UniquePush(candidates, Path.GetDirectoryName(launcherFolder), "RSI Launcher detecte");
            }
        }

        private static List<string> FindRsiLauncherFolderCandidates()
        {
            List<string> candidates = new List<string>();
            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            candidates.Add(Path.Combine(programFiles, "Roberts Space Industries", "RSI Launcher"));
            candidates.Add(Path.Combine(programFilesX86, "Roberts Space Industries", "RSI Launcher"));
            candidates.Add(Path.Combine(localAppData, "Programs", "RSI Launcher"));

            AddUninstallRegistryCandidates(candidates, Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Uninstall\RSI Launcher");
            AddUninstallRegistryCandidates(candidates, Registry.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Uninstall\RSI Launcher");
            AddUninstallRegistryCandidates(candidates, Registry.LocalMachine, @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\RSI Launcher");

            return candidates
                .Select(c => CleanPath(c))
                .Where(c => Directory.Exists(c))
                .Where(c => File.Exists(Path.Combine(c, "RSI Launcher.exe")) || String.Equals(Path.GetFileName(c), "RSI Launcher", StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static void AddUninstallRegistryCandidates(List<string> candidates, RegistryKey root, string subkey)
        {
            try
            {
                using (RegistryKey key = root.OpenSubKey(subkey))
                {
                    if (key == null)
                    {
                        return;
                    }

                    object installLocation = key.GetValue("InstallLocation");
                    if (installLocation != null)
                    {
                        candidates.Add(Convert.ToString(installLocation));
                    }

                    object displayIcon = key.GetValue("DisplayIcon");
                    if (displayIcon != null)
                    {
                        string icon = Convert.ToString(displayIcon);
                        icon = Regex.Replace(icon, @",\d+$", "");
                        if (!string.IsNullOrWhiteSpace(icon))
                        {
                            candidates.Add(Path.GetDirectoryName(icon));
                        }
                    }
                }
            }
            catch
            {
            }
        }

        private static void AddStarTradSettingsCandidates(List<LibraryCandidate> candidates)
        {
            foreach (string configFile in FindStarTradConfigFiles())
            {
                string raw = ReadTextSafe(configFile);
                if (string.IsNullOrWhiteSpace(raw))
                {
                    continue;
                }

                Dictionary<string, string> settings = ParseSettingsValues(raw);
                string library;
                if (settings.TryGetValue("RsiLauncherLibraryFolder", out library))
                {
                    UniquePush(candidates, library, "Config StarTrad");
                }

                string launcher;
                if (settings.TryGetValue("RsiLauncherFolderPath", out launcher))
                {
                    UniquePush(candidates, Path.GetDirectoryName(CleanPath(launcher)), "Config StarTrad launcher");
                }
            }
        }

        private static List<string> FindStarTradConfigFiles()
        {
            List<string> files = new List<string>();
            string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Startrad");
            if (!Directory.Exists(root))
            {
                return files;
            }

            Queue<string> queue = new Queue<string>();
            queue.Enqueue(root);
            while (queue.Count > 0)
            {
                string dir = queue.Dequeue();
                try
                {
                    foreach (string subdir in Directory.GetDirectories(dir))
                    {
                        queue.Enqueue(subdir);
                    }
                    foreach (string file in Directory.GetFiles(dir))
                    {
                        string name = Path.GetFileName(file);
                        if (String.Equals(name, "user.config", StringComparison.OrdinalIgnoreCase) ||
                            String.Equals(name, "startrad.dll.config", StringComparison.OrdinalIgnoreCase))
                        {
                            files.Add(file);
                        }
                    }
                }
                catch
                {
                }
            }
            return files;
        }

        private static Dictionary<string, string> ParseSettingsValues(string xml)
        {
            Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Regex regex = new Regex("<setting\\s+name=\"([^\"]+)\"[\\s\\S]*?<value>([\\s\\S]*?)</value>[\\s\\S]*?</setting>", RegexOptions.IgnoreCase);
            foreach (Match match in regex.Matches(xml))
            {
                values[match.Groups[1].Value] = match.Groups[2].Value.Trim();
            }
            return values;
        }

        private static void AddLauncherLogCandidates(List<LibraryCandidate> candidates)
        {
            foreach (string logPath in FindLauncherLogFiles())
            {
                string raw = ReadTextSafe(logPath);
                if (string.IsNullOrWhiteSpace(raw))
                {
                    continue;
                }

                string[] lines = Regex.Split(raw, "\r?\n");
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];

                    if (line.IndexOf("CHANGE_LIBRARY_FOLDER", StringComparison.OrdinalIgnoreCase) >= 0 && i + 3 < lines.Length)
                    {
                        UniquePush(candidates, lines[i + 3], "RSI launcher log");
                    }

                    Match launchMatch = Regex.Match(line, "\\[Launcher::launch\\]\\s+Launching Star Citizen\\s+.+?\\s+from\\s+\\((.+?)\\)", RegexOptions.IgnoreCase);
                    if (launchMatch.Success)
                    {
                        AddStarCitizenPathCandidate(candidates, launchMatch.Groups[1].Value, "RSI launcher launch log");
                    }

                    Match deleteMatch = Regex.Match(line, "\\[Launcher::launch\\]\\s+Deleting\\s+(.+?)[\\\\/]loginData\\.json", RegexOptions.IgnoreCase);
                    if (deleteMatch.Success)
                    {
                        AddStarCitizenPathCandidate(candidates, deleteMatch.Groups[1].Value, "RSI launcher loginData log");
                    }
                }

                AddStarCitizenPathsFromText(candidates, raw, "RSI launcher log path");
            }
        }

        private static List<string> FindLauncherLogFiles()
        {
            string logRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "rsilauncher", "logs");
            if (!Directory.Exists(logRoot))
            {
                return new List<string>();
            }

            try
            {
                return Directory.GetFiles(logRoot, "*.log")
                    .OrderByDescending(f => File.GetLastWriteTimeUtc(f))
                    .Take(5)
                    .ToList();
            }
            catch
            {
                return new List<string>();
            }
        }

        private static void AddLauncherStoreCandidates(List<LibraryCandidate> candidates)
        {
            string storePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "rsilauncher", "launcher store.json");
            string raw = ReadTextSafe(storePath);
            if (!string.IsNullOrWhiteSpace(raw))
            {
                AddStarCitizenPathsFromText(candidates, raw, "RSI launcher store");
            }
        }

        private static void AddStarCitizenPathsFromText(List<LibraryCandidate> candidates, string raw, string source)
        {
            Regex regex = new Regex("[A-Za-z]:\\\\[^\"'\\r\\n<>|]*?\\\\StarCitizen(?:\\\\[^\"'\\r\\n<>|,)\\]}]*)?", RegexOptions.IgnoreCase);
            foreach (Match match in regex.Matches(raw))
            {
                AddStarCitizenPathCandidate(candidates, match.Value, source);
            }
        }

        private static void AddCommonCandidates(List<LibraryCandidate> candidates)
        {
            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            UniquePush(candidates, Path.Combine(programFiles, "Roberts Space Industries"), "Chemin RSI standard");
            UniquePush(candidates, Path.Combine(programFilesX86, "Roberts Space Industries"), "Chemin RSI standard x86");

            foreach (string drive in new[] { "C:", "D:", "E:", "F:" })
            {
                UniquePush(candidates, drive + "\\Jeux", "Dossier jeux courant");
                UniquePush(candidates, drive + "\\Games", "Dossier games courant");
                UniquePush(candidates, drive + "\\Roberts Space Industries", "Dossier RSI courant");
                UniquePush(candidates, drive + "\\Star Citizen", "Dossier Star Citizen courant");
            }
        }

        private static void AddStarCitizenPathCandidate(List<LibraryCandidate> candidates, string candidatePath, string source)
        {
            string library = LibraryFromStarCitizenPath(candidatePath);
            UniquePush(candidates, library, source);
        }

        private static void UniquePush(List<LibraryCandidate> candidates, string candidatePath, string source)
        {
            if (string.IsNullOrWhiteSpace(candidatePath))
            {
                return;
            }

            string normalized = CleanPath(candidatePath);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return;
            }

            if (candidates.Any(c => SamePath(c.PathValue, normalized)))
            {
                return;
            }

            LibraryCandidate candidate = new LibraryCandidate();
            candidate.PathValue = normalized;
            candidate.Source = source;
            candidate.Valid = false;
            candidates.Add(candidate);
        }

        private static string LibraryFromStarCitizenPath(string candidatePath)
        {
            if (string.IsNullOrWhiteSpace(candidatePath))
            {
                return null;
            }

            string normalized = CleanPath(candidatePath);
            Match match = Regex.Match(normalized, "^(.+?)[\\\\/]+StarCitizen(?:[\\\\/].*)?$", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                return CleanPath(match.Groups[1].Value);
            }
            return null;
        }

        private static List<string> FindChannelPathsUnderLibrary(string libraryPath)
        {
            List<string> channels = new List<string>();
            if (string.IsNullOrWhiteSpace(libraryPath))
            {
                return channels;
            }

            Queue<Tuple<string, int>> queue = new Queue<Tuple<string, int>>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AddSearchRoot(queue, seen, Path.Combine(libraryPath, "StarCitizen"));
            AddSearchRoot(queue, seen, libraryPath);

            while (queue.Count > 0)
            {
                Tuple<string, int> item = queue.Dequeue();
                string dir = item.Item1;
                int depth = item.Item2;

                if (File.Exists(Path.Combine(dir, "Data.p4k")))
                {
                    channels.Add(dir);
                    continue;
                }

                if (depth >= 4)
                {
                    continue;
                }

                try
                {
                    foreach (string child in Directory.GetDirectories(dir))
                    {
                        if (seen.Contains(child))
                        {
                            continue;
                        }
                        seen.Add(child);
                        queue.Enqueue(Tuple.Create(child, depth + 1));
                    }
                }
                catch
                {
                }
            }

            return SortChannelPaths(channels.Distinct(StringComparer.OrdinalIgnoreCase).ToList());
        }

        private static void AddSearchRoot(Queue<Tuple<string, int>> queue, HashSet<string> seen, string root)
        {
            if (!Directory.Exists(root) || seen.Contains(root))
            {
                return;
            }
            seen.Add(root);
            queue.Enqueue(Tuple.Create(root, 0));
        }

        private static List<string> SortChannelPaths(List<string> channels)
        {
            return channels
                .OrderBy(c => String.Equals(Path.GetFileName(c), "LIVE", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(c => Path.GetFileName(c))
                .ToList();
        }

        private static List<StarCitizenInstall> SortInstalls(List<StarCitizenInstall> installs)
        {
            return installs
                .OrderBy(i => String.Equals(i.Channel, "LIVE", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(i => i.Channel)
                .ToList();
        }

        private static string ReadTranslationVersion(string globalIniPath)
        {
            string raw = ReadTextSafe(globalIniPath);
            if (string.IsNullOrEmpty(raw))
            {
                return null;
            }

            foreach (string line in Regex.Split(raw, "\r?\n"))
            {
                if (line.StartsWith("; Version :", StringComparison.OrdinalIgnoreCase))
                {
                    return line.Replace("; Version :", "").Trim();
                }
            }
            return null;
        }

        private static bool UserCfgUsesFrench(string userCfgPath)
        {
            string raw = ReadTextSafe(userCfgPath);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return false;
            }
            return Regex.IsMatch(raw, "^g_language\\s*=\\s*french_\\(france\\)\\s*$", RegexOptions.IgnoreCase | RegexOptions.Multiline);
        }

        private static void CreateOrUpdateUserCfg(string userCfgPath)
        {
            if (!File.Exists(userCfgPath))
            {
                File.WriteAllText(userCfgPath, FrenchLanguageLine);
                return;
            }

            List<string> lines = Regex.Split(File.ReadAllText(userCfgPath), "\r?\n").ToList();
            bool found = false;
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].TrimStart().StartsWith("g_language", StringComparison.OrdinalIgnoreCase))
                {
                    lines[i] = FrenchLanguageLine;
                    found = true;
                    break;
                }
            }
            if (!found)
            {
                lines.Add(FrenchLanguageLine);
            }
            File.WriteAllText(userCfgPath, String.Join(Environment.NewLine, lines.ToArray()));
        }

        private static void RemoveFrenchLanguageFromUserCfg(string userCfgPath)
        {
            if (!File.Exists(userCfgPath))
            {
                return;
            }

            List<string> lines = Regex.Split(File.ReadAllText(userCfgPath), "\r?\n")
                .Where(line => !line.TrimStart().StartsWith("g_language", StringComparison.OrdinalIgnoreCase))
                .ToList();

            string remaining = String.Join(Environment.NewLine, lines.ToArray()).Trim();
            if (remaining.Length == 0)
            {
                File.Delete(userCfgPath);
                return;
            }

            File.WriteAllText(userCfgPath, String.Join(Environment.NewLine, lines.ToArray()));
        }

        private static void RemoveEmptyParents(string startDir, string stopDir)
        {
            if (string.IsNullOrWhiteSpace(startDir) || string.IsNullOrWhiteSpace(stopDir))
            {
                return;
            }

            string stop = Path.GetFullPath(stopDir).TrimEnd('\\').ToLowerInvariant();
            string current = Path.GetFullPath(startDir).TrimEnd('\\');
            while (current.ToLowerInvariant().StartsWith(stop))
            {
                if (String.Equals(current, stop, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                try
                {
                    if (!Directory.Exists(current) || Directory.GetFileSystemEntries(current).Length > 0)
                    {
                        return;
                    }
                    Directory.Delete(current);
                }
                catch
                {
                    return;
                }

                current = Path.GetDirectoryName(current);
            }
        }

        private static string CleanPath(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            string cleaned = value.Trim().Trim('"').Replace("/", "\\");
            cleaned = Regex.Replace(cleaned, "[),.;\\]]+$", "");
            try
            {
                return Path.GetFullPath(cleaned);
            }
            catch
            {
                return cleaned;
            }
        }

        private static string ReadTextSafe(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    return null;
                }
                return File.ReadAllText(filePath);
            }
            catch
            {
                return null;
            }
        }

        private static bool SamePath(string a, string b)
        {
            if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b))
            {
                return false;
            }
            return String.Equals(CleanPath(a), CleanPath(b), StringComparison.OrdinalIgnoreCase);
        }

        private static void TryDelete(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
            catch
            {
            }
        }
    }
}
