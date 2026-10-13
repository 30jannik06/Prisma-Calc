// Prisma - core: logging, settings, theme resolution and palettes. C# 5.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace Prisma
{
    public enum ThemeMode { Default, Light, Dark }
    public enum EffectiveTheme { Light, Dark, HighContrast }

    public enum AppMode
    {
        Standard, Scientific, Programmer, Graphing, Date,
        Currency, Volume, Length, Weight, Temperature, Energy, Area, Speed, Time, Power, Data, Pressure, Angle,
        Subnet, IpConvert, Transfer,
        Settings
    }

    // ------------------------------------------------------------ path checks
    // Paths given on the command line are checked before anything is written there.
    public static class SafePath
    {
        static readonly string[] Reserved = { "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" };

        public static bool TryNormalize(string input, out string full, out string error)
        {
            full = null; error = null;
            if (string.IsNullOrWhiteSpace(input)) { error = "empty path"; return false; }
            if (input.Length > 240) { error = "path too long"; return false; }
            if (input.IndexOf('\0') >= 0) { error = "invalid character"; return false; }
            try { full = Path.GetFullPath(input); }
            catch (Exception) { error = "invalid path"; return false; }
            if (full.StartsWith(@"\\", StringComparison.Ordinal)) { error = "network and device paths are not allowed"; full = null; return false; }
            string name = Path.GetFileNameWithoutExtension(full) ?? "";
            foreach (string r in Reserved)
                if (string.Equals(name, r, StringComparison.OrdinalIgnoreCase)) { error = "reserved device name"; full = null; return false; }
            if (Directory.Exists(full)) { error = "path is a folder"; full = null; return false; }
            return true;
        }
    }

    // ---------------------------------------------------------------- logging
    public static class Log
    {
        public static string Path;
        public static long MaxBytes = 1024 * 1024;   // above this the log is rolled to prisma.log.1
        static readonly object Gate = new object();

        public static string DefaultDir()
        {
            return System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Prisma");
        }
        public static string DefaultPath() { return System.IO.Path.Combine(DefaultDir(), "prisma.log"); }

        // One log entry is one line: line breaks and control characters cannot forge extra entries.
        public static string Clean(string m)
        {
            if (m == null) return "";
            if (m.Length > 4000) m = m.Substring(0, 4000) + "...";
            var sb = new StringBuilder(m.Length);
            foreach (char c in m)
            {
                if (c == '\r') continue;
                if (c == '\n') sb.Append(" | ");
                else if (c < ' ' && c != '\t') sb.Append('?');
                else sb.Append(c);
            }
            return sb.ToString();
        }

        public static void Write(string level, string message)
        {
            try
            {
                lock (Gate)
                {
                    string p = Path ?? DefaultPath();
                    Directory.CreateDirectory(System.IO.Path.GetDirectoryName(p));
                    var fi = new FileInfo(p);
                    if (fi.Exists && fi.Length > MaxBytes)
                    {
                        try { if (File.Exists(p + ".1")) File.Delete(p + ".1"); File.Move(p, p + ".1"); } catch (Exception) { }
                    }
                    File.AppendAllText(p, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture)
                        + " [" + level + "] " + Clean(message) + Environment.NewLine, new UTF8Encoding(false));
                }
            }
            catch (Exception) { /* logging must never crash the app */ }
        }
        public static void Info(string m) { Write("INFO", m); }
        public static void Warn(string m) { Write("WARN", m); }
        public static void Error(string m) { Write("ERROR", m); }
    }

    // --------------------------------------------------------------- settings
    // A small key=value file. Only known keys with valid values are read or written; the file is size-capped
    // and written through a temporary file so a crash cannot leave it half written.
    public static class Settings
    {
        public static string Path;
        public const long MaxFileBytes = 16 * 1024;

        public static string DefaultPath() { return System.IO.Path.Combine(Log.DefaultDir(), "settings.ini"); }

        public static bool TryParseMode(string s, out ThemeMode mode)
        {
            mode = ThemeMode.Default;
            if (s == null) return false;
            foreach (ThemeMode m in Enum.GetValues(typeof(ThemeMode)))
                if (string.Equals(m.ToString(), s.Trim(), StringComparison.OrdinalIgnoreCase)) { mode = m; return true; }
            return false;
        }

        public static bool TryParseApp(string s, out AppMode mode)
        {
            mode = AppMode.Standard;
            if (s == null) return false;
            foreach (AppMode m in Enum.GetValues(typeof(AppMode)))
                if (string.Equals(m.ToString(), s.Trim(), StringComparison.OrdinalIgnoreCase)) { mode = m; return true; }
            return false;
        }

        public static bool IsValid(string key, string value)
        {
            if (key == null || value == null || value.Length > 64) return false;
            foreach (char c in value) if (c < ' ' || c == '=') return false;
            ThemeMode tm; AppMode am; Color col;
            switch (key)
            {
                case "Theme": return TryParseMode(value, out tm);
                case "Mode": return TryParseApp(value, out am);
                case "Style": foreach (string st in ThemeManager.Styles) if (string.Equals(st, value, StringComparison.OrdinalIgnoreCase)) return true; return false;
                case "Accent": return value == "Theme" || value == "System" || (value.StartsWith("#") && ThemeManager.TryParseHex(value, out col));
                case "Glass": case "Animate": return value == "0" || value == "1";
                case "Corners": return value == "Square" || value == "Soft" || value == "Round";
                case "WindowWidth": case "WindowHeight": { int n; return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out n) && n >= 300 && n <= 8000; }
                case "Language": return string.Equals(value, "System", StringComparison.OrdinalIgnoreCase) || Loc.IsSupported(value);
            }
            return false;
        }

        static Dictionary<string, string> ReadAll()
        {
            var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string p = Path ?? DefaultPath();
            try
            {
                var fi = new FileInfo(p);
                if (!fi.Exists) return d;
                if (fi.Length > MaxFileBytes) { Log.Warn("Settings file is too large and was ignored"); return d; }
                foreach (string line in File.ReadAllLines(p))
                {
                    int i = line.IndexOf('=');
                    if (i <= 0) continue;
                    string k = line.Substring(0, i).Trim(), v = line.Substring(i + 1).Trim();
                    if (IsValid(k, v)) d[k] = v;
                    else Log.Warn("Ignored invalid setting '" + k + "'");
                }
            }
            catch (Exception ex) { Log.Warn("Could not read settings: " + ex.Message); }
            return d;
        }

        public static string Get(string key, string fallback)
        {
            string v;
            return ReadAll().TryGetValue(key, out v) ? v : fallback;
        }

        public static bool Set(string key, string value)
        {
            if (!IsValid(key, value)) { Log.Warn("Refused to save invalid setting '" + key + "'"); return false; }
            string p = Path ?? DefaultPath();
            string tmp = p + ".tmp";
            try
            {
                Dictionary<string, string> d = ReadAll();
                d[key] = value;
                var sb = new StringBuilder();
                foreach (KeyValuePair<string, string> kv in d) sb.Append(kv.Key + "=" + kv.Value + Environment.NewLine);
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(p));
                File.WriteAllText(tmp, sb.ToString(), new UTF8Encoding(false));
                if (File.Exists(p)) File.Replace(tmp, p, null); else File.Move(tmp, p);
                return true;
            }
            catch (Exception ex)
            {
                Log.Warn("Could not save settings: " + ex.Message);
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch (Exception) { }
                return false;
            }
        }

        public static ThemeMode Load()
        {
            string v = Get("Theme", null);
            ThemeMode m;
            return v != null && TryParseMode(v, out m) ? m : ThemeMode.Default;
        }

        public static bool Save(ThemeMode mode) { return Set("Theme", mode.ToString()); }

        public static AppMode LoadApp()
        {
            AppMode m;
            return TryParseApp(Get("Mode", null), out m) ? m : AppMode.Standard;
        }
    }

    // ----------------------------------------------------------------- theme
    public static class ThemeManager
    {
        public static readonly string[] Keys = {
            "WinBg", "BgA", "BgB", "Text", "SubText", "NumBg", "NumText", "FuncBg", "FuncText", "Border", "Panel", "Sheen",
            "AccentBg", "AccentBg2", "AccentText", "Brand", "GraphBg", "PopupBg", "Blob1", "Blob2", "Blob3" };

        public const string DefaultStyle = "Aurora";
        public static readonly string[] Styles = { "Aurora", "Sunset", "Ocean", "Forest", "Rose", "Graphite", "Classic" };

        // Glass styles: { gradient A, gradient B, blob 1, blob 2, blob 3, accent }.
        static readonly Dictionary<string, string[]> Dark = new Dictionary<string, string[]>
        {
            { "Aurora",   new[] { "0B1026", "102A43", "00A8CC", "7A4DFF", "D63FB5", "4DE1D2" } },
            { "Sunset",   new[] { "1E1033", "3D1536", "E5602B", "D6336C", "7A44E0", "FF8A5B" } },
            { "Ocean",    new[] { "04202F", "053B5A", "0074B0", "119E86", "3F55C8", "3FB8FF" } },
            { "Forest",   new[] { "0A1F14", "12301F", "1A9650", "6E9A2E", "128A72", "52E08A" } },
            { "Rose",     new[] { "2A0F1F", "3A1230", "E04683", "A64DE0", "E0735A", "FF6FA8" } },
            { "Graphite", new[] { "121316", "1E2025", "566074", "6F7D96", "3C4452", "9AB0FF" } }
        };

        static readonly Dictionary<string, string[]> Light = new Dictionary<string, string[]>
        {
            { "Aurora",   new[] { "E6F4FF", "F3E8FF", "7CC8FF", "C4A8FF", "FFB3E6", "08708F" } },
            { "Sunset",   new[] { "FFF1E6", "FFE3EC", "FFB482", "FF9EC4", "C6A5FF", "B93C0A" } },
            { "Ocean",    new[] { "E3F6FF", "E3FFF6", "7AD3FF", "8AF0D8", "9BB3FF", "0369A1" } },
            { "Forest",   new[] { "EAF7EC", "F6FBE6", "8EE6A0", "D4F27A", "8FE3D1", "15803D" } },
            { "Rose",     new[] { "FFEAF1", "FFF0E6", "FFA8C8", "E3B5FF", "FFC9A8", "BE185D" } },
            { "Graphite", new[] { "F1F2F5", "E4E7EC", "C8CED8", "B6C2D6", "DADFE8", "334155" } }
        };

        public static bool IsGlassStyle(string name) { return name != null && Dark.ContainsKey(name); }

        public static string NormalizeStyle(string name)
        {
            foreach (string n in Styles) if (string.Equals(n, name, StringComparison.OrdinalIgnoreCase)) return n;
            return DefaultStyle;
        }

        public static EffectiveTheme Resolve(ThemeMode mode, bool osUsesLightApps, bool highContrast)
        {
            if (highContrast) return EffectiveTheme.HighContrast;
            if (mode == ThemeMode.Light) return EffectiveTheme.Light;
            if (mode == ThemeMode.Dark) return EffectiveTheme.Dark;
            return osUsesLightApps ? EffectiveTheme.Light : EffectiveTheme.Dark;
        }

        public static bool OsUsesLightApps()
        {
            try
            {
                object v = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 1);
                if (v is int) return (int)v != 0;
            }
            catch (Exception ex) { Log.Warn("Could not read OS theme: " + ex.Message); }
            return true;
        }

        public static bool HighContrastOn()
        {
            try { return SystemParameters.HighContrast; } catch (Exception) { return false; }
        }

        public static Color SystemAccent()
        {
            try { Color c = SystemParameters.WindowGlassColor; return Color.FromRgb(c.R, c.G, c.B); }
            catch (Exception) { }
            return Color.FromRgb(0x00, 0x67, 0xC0);
        }

        public static Color C(string hex)
        {
            return Color.FromRgb(Convert.ToByte(hex.Substring(0, 2), 16), Convert.ToByte(hex.Substring(2, 2), 16), Convert.ToByte(hex.Substring(4, 2), 16));
        }

        public static Color A(byte alpha, string hex) { Color c = C(hex); return Color.FromArgb(alpha, c.R, c.G, c.B); }

        public static bool TryParseHex(string s, out Color c)
        {
            c = Colors.Black;
            if (s == null) return false;
            s = s.Trim().TrimStart('#');
            if (s.Length != 6) return false;
            try { c = C(s); return true; } catch (Exception) { return false; }
        }

        public static Color Mix(Color a, Color b, double t)
        {
            return Color.FromArgb(255, (byte)Math.Round(a.R + (b.R - a.R) * t), (byte)Math.Round(a.G + (b.G - a.G) * t), (byte)Math.Round(a.B + (b.B - a.B) * t));
        }

        // Alpha-composite fg over an opaque background.
        public static Color Composite(Color fg, Color bg)
        {
            double a = fg.A / 255.0;
            return Color.FromRgb((byte)Math.Round(fg.R * a + bg.R * (1 - a)), (byte)Math.Round(fg.G * a + bg.G * (1 - a)), (byte)Math.Round(fg.B * a + bg.B * (1 - a)));
        }

        public static double Luminance(Color c)
        {
            Func<byte, double> f = delegate(byte b)
            {
                double s = b / 255.0;
                return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
            };
            return 0.2126 * f(c.R) + 0.7152 * f(c.G) + 0.0722 * f(c.B);
        }

        public static double Contrast(Color a, Color b)
        {
            double la = Luminance(a), lb = Luminance(b);
            return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
        }

        public static Color BestTextOn(Color bg)
        {
            return Contrast(bg, Colors.White) >= Contrast(bg, Colors.Black) ? Colors.White : Colors.Black;
        }

        public static Color BestTextOn(Color bg1, Color bg2)
        {
            double w = Math.Min(Contrast(bg1, Colors.White), Contrast(bg2, Colors.White));
            double k = Math.Min(Contrast(bg1, Colors.Black), Contrast(bg2, Colors.Black));
            return w >= k ? Colors.White : Colors.Black;
        }

        // Every opaque colour the translucent surfaces can end up on: the two gradient ends and each blob over them.
        public static List<Color> Bases(Dictionary<string, Color> p)
        {
            var l = new List<Color>();
            foreach (string end in new[] { "BgA", "BgB" })
            {
                l.Add(p[end]);
                foreach (string b in new[] { "Blob1", "Blob2", "Blob3" }) l.Add(Composite(p[b], p[end]));
            }
            return l;
        }

        public static double WorstContrast(Color fg, Dictionary<string, Color> p, string surfaceKey)
        {
            double worst = 99;
            foreach (Color b in Bases(p))
            {
                Color surface = surfaceKey == null ? b : Composite(p[surfaceKey], b);
                Color text = fg.A < 255 ? Composite(fg, surface) : fg;
                worst = Math.Min(worst, Contrast(text, surface));
            }
            return worst;
        }

        public static Dictionary<string, Color> Classic(EffectiveTheme t, Color accent)
        {
            var d = new Dictionary<string, Color>();
            if (t == EffectiveTheme.Light)
            {
                d["WinBg"] = C("F3F3F3"); d["Text"] = C("1A1A1A"); d["SubText"] = C("5C5C5C");
                d["NumBg"] = C("FFFFFF"); d["NumText"] = C("1A1A1A");
                d["FuncBg"] = C("E9E9E9"); d["FuncText"] = C("1A1A1A"); d["Border"] = C("D0D0D0");
            }
            else if (t == EffectiveTheme.Dark)
            {
                d["WinBg"] = C("202020"); d["Text"] = C("FFFFFF"); d["SubText"] = C("C5C5C5");
                d["NumBg"] = C("3B3B3B"); d["NumText"] = C("FFFFFF");
                d["FuncBg"] = C("2B2B2B"); d["FuncText"] = C("FFFFFF"); d["Border"] = C("484848");
            }
            else
            {
                d["WinBg"] = SystemColors.WindowColor; d["Text"] = SystemColors.WindowTextColor; d["SubText"] = SystemColors.WindowTextColor;
                d["NumBg"] = SystemColors.WindowColor; d["NumText"] = SystemColors.WindowTextColor;
                d["FuncBg"] = SystemColors.WindowColor; d["FuncText"] = SystemColors.WindowTextColor; d["Border"] = SystemColors.WindowTextColor;
                accent = SystemColors.HighlightColor;
            }
            Color none = Color.FromArgb(0, 255, 255, 255);
            d["BgA"] = d["WinBg"]; d["BgB"] = d["WinBg"]; d["Panel"] = d["FuncBg"]; d["Sheen"] = none;
            d["GraphBg"] = d["NumBg"]; d["PopupBg"] = d["NumBg"];
            d["Blob1"] = none; d["Blob2"] = none; d["Blob3"] = none;
            d["AccentBg"] = accent; d["AccentBg2"] = accent;
            d["AccentText"] = t == EffectiveTheme.HighContrast ? SystemColors.HighlightTextColor : BestTextOn(accent);
            d["Brand"] = t == EffectiveTheme.HighContrast ? SystemColors.WindowTextColor : (Contrast(accent, d["WinBg"]) >= 3.0 ? accent : d["Text"]);
            return d;
        }

        // Old name, still used for the flat palette.
        public static Dictionary<string, Color> Palette(EffectiveTheme t, Color accent) { return Classic(t, accent); }

        public static Dictionary<string, Color> Build(string style, EffectiveTheme t, Color? accentOverride, bool glass)
        {
            style = NormalizeStyle(style);
            if (t == EffectiveTheme.HighContrast) return Classic(t, SystemColors.HighlightColor);
            if (style == "Classic") return Classic(t, accentOverride.HasValue ? accentOverride.Value : SystemAccent());
            bool dark = t == EffectiveTheme.Dark;
            string[] spec = (dark ? Dark : Light)[style];
            var d = new Dictionary<string, Color>();
            Color a = C(spec[0]), b = C(spec[1]);
            d["BgA"] = a; d["BgB"] = b; d["WinBg"] = Mix(a, b, 0.5);
            byte blobAlpha = dark ? (byte)0x5C : (byte)0x80;
            d["Blob1"] = A(blobAlpha, spec[2]); d["Blob2"] = A(blobAlpha, spec[3]); d["Blob3"] = A(blobAlpha, spec[4]);
            Color acc = accentOverride.HasValue ? accentOverride.Value : C(spec[5]);
            if (dark)
            {
                d["Text"] = C("FFFFFF"); d["SubText"] = C("E4E7F5");
                d["NumBg"] = A(0x24, "FFFFFF"); d["FuncBg"] = A(0x14, "FFFFFF"); d["Border"] = A(0x40, "FFFFFF");
                d["Panel"] = A(0x12, "FFFFFF"); d["Sheen"] = A(0x34, "FFFFFF");
                d["GraphBg"] = Mix(b, Colors.Black, 0.35); d["PopupBg"] = Mix(b, Colors.White, 0.14);
            }
            else
            {
                d["Text"] = C("1B1B2F"); d["SubText"] = C("464B66");
                d["NumBg"] = A(0x99, "FFFFFF"); d["FuncBg"] = A(0x5C, "FFFFFF"); d["Border"] = A(0xB8, "FFFFFF");
                d["Panel"] = A(0x66, "FFFFFF"); d["Sheen"] = A(0x99, "FFFFFF");
                d["GraphBg"] = Mix(b, Colors.White, 0.65); d["PopupBg"] = Mix(b, Colors.White, 0.85);
            }
            d["NumText"] = d["Text"]; d["FuncText"] = d["Text"];
            Color accText = BestTextOn(acc);
            Color acc2 = accText == Colors.White ? Mix(acc, Colors.Black, 0.28) : Mix(acc, Colors.White, 0.22);
            d["AccentBg"] = acc; d["AccentBg2"] = acc2; d["AccentText"] = accText;
            d["Brand"] = WorstContrast(acc, d, null) >= 3.0 ? acc : d["Text"];
            if (!glass)
            {
                foreach (string k in new[] { "NumBg", "FuncBg", "Border", "Panel" }) d[k] = Composite(d[k], d["WinBg"]);
                d["Sheen"] = Color.FromArgb(0, 255, 255, 255);
                foreach (string k in new[] { "Blob1", "Blob2", "Blob3" }) d[k] = Color.FromArgb(0, d[k].R, d[k].G, d[k].B);
            }
            return d;
        }

        public static Color[] Preview(string style, bool dark)
        {
            style = NormalizeStyle(style);
            if (style == "Classic") return dark ? new[] { C("202020"), C("3B3B3B"), SystemAccent() } : new[] { C("F3F3F3"), C("FFFFFF"), SystemAccent() };
            string[] s = (dark ? Dark : Light)[style];
            return new[] { C(s[0]), C(s[1]), C(s[5]) };
        }

        // Curve colours for the graphing view, readable on GraphBg.
        public static Color[] CurveColors(EffectiveTheme t)
        {
            if (t == EffectiveTheme.HighContrast)
                return new[] { SystemColors.WindowTextColor, SystemColors.HighlightColor, SystemColors.WindowTextColor, SystemColors.HighlightColor, SystemColors.WindowTextColor, SystemColors.HighlightColor };
            if (t == EffectiveTheme.Dark)
                return new[] { C("4CC2FF"), C("FF7A7A"), C("6CE08A"), C("FFC24C"), C("D49CFF"), C("5EE6D8") };
            return new[] { C("0067C0"), C("C42B1C"), C("107C10"), C("B25E00"), C("7A3EB5"), C("007A78") };
        }
    }
}
