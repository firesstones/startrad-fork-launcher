using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;

namespace StarTradForLauncher
{
    internal static class LauncherCommand
    {
        public static int Run(string[] args)
        {
            try
            {
                string command = args.Length > 1 ? args[1].ToLowerInvariant() : "detect";
                object payload;

                switch (command)
                {
                    case "detect":
                        payload = new Dictionary<string, object>
                        {
                            { "ok", true },
                            { "detection", ToDetection(StarTradCore.Detect()) }
                        };
                        break;
                    case "install":
                        payload = Install(args.Length > 2 ? args[2] : null);
                        break;
                    case "uninstall":
                        payload = Uninstall(args.Length > 2 ? args[2] : null);
                        break;
                    case "open":
                        StarTradCore.OpenPath(args.Length > 2 ? args[2] : null);
                        payload = new Dictionary<string, object> { { "ok", true } };
                        break;
                    default:
                        payload = Error("Commande StarTrad inconnue : " + command);
                        break;
                }

                Console.WriteLine(Json.Write(payload));
                return IsOk(payload) ? 0 : 2;
            }
            catch (Exception ex)
            {
                Console.WriteLine(Json.Write(Error(ex.Message)));
                return 1;
            }
        }

        private static object Install(string channelPath)
        {
            StarCitizenInstall install = FindInstall(channelPath);
            if (install == null)
            {
                return Error("Version Star Citizen inconnue : " + (channelPath ?? ""));
            }

            StarTradCore.InstallTranslation(install);
            StarCitizenInstall updated = FindInstall(channelPath) ?? install;
            return new Dictionary<string, object>
            {
                { "ok", true },
                { "install", ToInstall(updated) }
            };
        }

        private static object Uninstall(string channelPath)
        {
            StarCitizenInstall install = FindInstall(channelPath);
            if (install == null)
            {
                return Error("Version Star Citizen inconnue : " + (channelPath ?? ""));
            }

            StarTradCore.UninstallTranslation(install);
            StarCitizenInstall updated = FindInstall(channelPath) ?? install;
            return new Dictionary<string, object>
            {
                { "ok", true },
                { "install", ToInstall(updated) }
            };
        }

        private static StarCitizenInstall FindInstall(string channelPath)
        {
            if (String.IsNullOrWhiteSpace(channelPath))
            {
                return null;
            }
            return StarTradCore.Detect().Installs.FirstOrDefault(i =>
                String.Equals(i.ChannelPath, channelPath, StringComparison.OrdinalIgnoreCase)
            );
        }

        private static Dictionary<string, object> Error(string message)
        {
            return new Dictionary<string, object>
            {
                { "ok", false },
                { "error", message }
            };
        }

        private static bool IsOk(object payload)
        {
            Dictionary<string, object> dictionary = payload as Dictionary<string, object>;
            return dictionary != null && dictionary.ContainsKey("ok") && dictionary["ok"] is bool && (bool)dictionary["ok"];
        }

        private static Dictionary<string, object> ToDetection(DetectionResult detection)
        {
            return new Dictionary<string, object>
            {
                { "libraries", detection.Libraries.Select(ToLibrary).ToList() },
                { "installs", detection.Installs.Select(ToInstall).ToList() },
                { "checkedAt", detection.CheckedAt.ToUniversalTime().ToString("o") }
            };
        }

        private static Dictionary<string, object> ToLibrary(LibraryCandidate library)
        {
            return new Dictionary<string, object>
            {
                { "path", library.PathValue },
                { "source", library.Source },
                { "valid", library.Valid }
            };
        }

        private static Dictionary<string, object> ToInstall(StarCitizenInstall install)
        {
            return new Dictionary<string, object>
            {
                { "channel", install.Channel },
                { "channelPath", install.ChannelPath },
                { "libraryPath", install.LibraryPath },
                { "hasDataP4k", true },
                { "gameVersion", install.GameVersion },
                { "gameBranch", install.GameBranch },
                { "gameBuild", install.GameBuild },
                { "gameBuildDate", install.GameBuildDate },
                { "translationInstalled", install.TranslationInstalled },
                { "translationVersion", install.TranslationVersion },
                { "latestTranslationVersion", install.LatestTranslationVersion },
                { "globalIniPath", install.GlobalIniPath },
                { "userCfgPath", install.UserCfgPath },
                { "userCfgUsesFrench", install.UserCfgUsesFrench },
                { "source", install.Source }
            };
        }
    }

    internal static class Json
    {
        public static string Write(object value)
        {
            StringBuilder sb = new StringBuilder();
            WriteValue(sb, value);
            return sb.ToString();
        }

        private static void WriteValue(StringBuilder sb, object value)
        {
            if (value == null)
            {
                sb.Append("null");
                return;
            }

            if (value is string)
            {
                WriteString(sb, (string)value);
                return;
            }

            if (value is bool)
            {
                sb.Append((bool)value ? "true" : "false");
                return;
            }

            if (value is int || value is long || value is double || value is float || value is decimal)
            {
                sb.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                return;
            }

            IDictionary dictionary = value as IDictionary;
            if (dictionary != null)
            {
                WriteObject(sb, dictionary);
                return;
            }

            IEnumerable enumerable = value as IEnumerable;
            if (enumerable != null)
            {
                WriteArray(sb, enumerable);
                return;
            }

            WriteObjectFromFields(sb, value);
        }

        private static void WriteObject(StringBuilder sb, IDictionary dictionary)
        {
            sb.Append('{');
            bool first = true;
            foreach (DictionaryEntry entry in dictionary)
            {
                if (!first) sb.Append(',');
                first = false;
                WriteString(sb, Convert.ToString(entry.Key));
                sb.Append(':');
                WriteValue(sb, entry.Value);
            }
            sb.Append('}');
        }

        private static void WriteObjectFromFields(StringBuilder sb, object value)
        {
            sb.Append('{');
            bool first = true;
            foreach (FieldInfo field in value.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                if (!first) sb.Append(',');
                first = false;
                WriteString(sb, field.Name);
                sb.Append(':');
                WriteValue(sb, field.GetValue(value));
            }
            sb.Append('}');
        }

        private static void WriteArray(StringBuilder sb, IEnumerable values)
        {
            sb.Append('[');
            bool first = true;
            foreach (object item in values)
            {
                if (!first) sb.Append(',');
                first = false;
                WriteValue(sb, item);
            }
            sb.Append(']');
        }

        private static void WriteString(StringBuilder sb, string value)
        {
            sb.Append('"');
            foreach (char c in value ?? "")
            {
                switch (c)
                {
                    case '"':
                        sb.Append("\\\"");
                        break;
                    case '\\':
                        sb.Append("\\\\");
                        break;
                    case '\b':
                        sb.Append("\\b");
                        break;
                    case '\f':
                        sb.Append("\\f");
                        break;
                    case '\n':
                        sb.Append("\\n");
                        break;
                    case '\r':
                        sb.Append("\\r");
                        break;
                    case '\t':
                        sb.Append("\\t");
                        break;
                    default:
                        if (c < 32)
                        {
                            sb.Append("\\u");
                            sb.Append(((int)c).ToString("x4"));
                        }
                        else
                        {
                            sb.Append(c);
                        }
                        break;
                }
            }
            sb.Append('"');
        }
    }
}
