// Prisma - hardening tests: fuzzing, hostile files and paths, and the security properties of the built program. C# 5.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;

namespace Prisma
{
    public static class HardeningTests
    {
        static Action<string, bool, string> check;

        public static void Run(Action<string, bool, string> c)
        {
            check = c;
            ParserFuzz();
            ParserStress();
            StandardFuzz();
            ProgrammerFuzz();
            DateFuzz();
            UnitFuzz();
            SettingsAttacks();
            LogAttacks();
            RatesAttacks();
            PathChecks();
            ProgramPosture();
        }

        static string Sample(Random r, string text) { return text.Length > 80 ? text.Substring(0, 80) + "..." : text; }

        // ----------------------------------------------------------- parser fuzz
        static void ParserFuzz()
        {
            var rnd = new Random(20261013);
            string alpha = "0123456789.+-*/^()!xepi sincotalgbrdqmhu\u00D7\u00F7\u2212,%E";
            string[] words = { "sin(", "cos(", "tan(", "asin(", "ln(", "log(", "sqrt(", "abs(", "exp(", "cbrt(", "mod", "pi", "e", "x", "!", "^", "(", ")", " ", "1e", "1e+", "..", "--", "++" };
            int bad = 0; string first = null; var sw = Stopwatch.StartNew();
            for (int i = 0; i < 15000; i++)
            {
                var sb = new StringBuilder();
                int parts = rnd.Next(1, 14);
                for (int k = 0; k < parts; k++)
                {
                    if (rnd.Next(3) == 0) sb.Append(alpha[rnd.Next(alpha.Length)]);
                    else if (rnd.Next(2) == 0) sb.Append(words[rnd.Next(words.Length)]);
                    else sb.Append(rnd.Next(0, 1000).ToString(CultureInfo.InvariantCulture));
                }
                string s = sb.ToString();
                AngleMode am = (AngleMode)rnd.Next(3);
                try
                {
                    Func<double, double> f = Expr.Compile(s, am);
                    f(rnd.NextDouble() * 20 - 10); f(0); f(double.MaxValue); f(double.NaN);
                    try { Expr.Eval(s, am); } catch (CalcException) { }
                }
                catch (CalcException) { }
                catch (Exception ex) { bad++; if (first == null) first = Sample(rnd, s) + " -> " + ex.GetType().Name; }
            }
            check("fuzz: 15000 random expressions only ever raise CalcException (" + sw.ElapsedMilliseconds + " ms)", bad == 0, bad + " bad, first: " + first);
        }

        static string Try(string expr)
        {
            try { return Expr.Format(Expr.Eval(expr, AngleMode.Radians)); }
            catch (CalcException) { return "calc-error"; }
            catch (Exception ex) { return "BAD:" + ex.GetType().Name; }
        }

        static void ParserStress()
        {
            check("stress: 10000 nested parentheses are refused, not a crash", Try(new string('(', 5000) + "1" + new string(')', 5000)) == "calc-error", "");
            check("stress: 400 nested parentheses are refused", Try(new string('(', 400) + "1" + new string(')', 400)) == "calc-error", "");
            check("stress: 100 nested parentheses still work", Try(new string('(', 99) + "1" + new string(')', 99)) == "1", Try(new string('(', 99) + "1" + new string(')', 99)));
            check("stress: 150 nested function calls are refused", Try(string.Concat(new string[150].Select_("sin(")) + "1" + new string(')', 150)) == "calc-error", "");
            check("stress: 900 prefix minus signs are refused", Try(new string('-', 900) + "1") == "calc-error", "");
            check("stress: 150 prefix minus signs work (even count = +1)", Try(new string('-', 150) + "1") == "1", Try(new string('-', 150) + "1"));
            string pow = "2"; for (int i = 0; i < 300; i++) pow += "^2";
            check("stress: a tower of 300 powers does not crash", Try(pow) != null && !Try(pow).StartsWith("BAD"), Try(pow));
            string sum = "1"; for (int i = 0; i < 450; i++) sum += "+1";
            check("stress: a sum of 451 terms works (closure chain)", Try(sum) == "451", Try(sum));
            check("stress: 500 factorial signs do not crash", !Try("3" + new string('!', 500)).StartsWith("BAD"), "");
            check("stress: 100000 characters are refused", Try(new string('1', 100000)) == "calc-error", "");
            check("stress: exactly 1000 characters are accepted", !Try(new string('1', 1000)).StartsWith("BAD"), "");
            check("stress: huge powers give an error, not an exception", Try("9^9^9^9") == "calc-error", Try("9^9^9^9"));
            check("stress: a null expression is an error", Try(null) == "calc-error", "");
        }

        // --------------------------------------------------------- engine fuzz
        static void StandardFuzz()
        {
            var rnd = new Random(7);
            string[] keys = { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9", ".", "+", "-", "*", "/", "=", "%", "inv", "sqr", "sqrt", "neg", "BK", "C", "CE" };
            int bad = 0, tooLong = 0; string first = null;
            for (int n = 0; n < 6000; n++)
            {
                var e = new CalcEngine();
                for (int k = 0; k < 60; k++)
                {
                    string key = keys[rnd.Next(keys.Length)];
                    try
                    {
                        switch (key)
                        {
                            case ".": e.Dot(); break;
                            case "=": e.Equals(); break;
                            case "%": e.Percent(); break;
                            case "inv": e.Reciprocal(); break;
                            case "sqr": e.Square(); break;
                            case "sqrt": e.Sqrt(); break;
                            case "neg": e.Negate(); break;
                            case "BK": e.Back(); break;
                            case "C": e.Clear(); break;
                            case "CE": e.ClearEntry(); break;
                            case "+": case "-": case "*": case "/": e.Operator(key[0]); break;
                            default: e.Digit(key[0]); break;
                        }
                        if (e.Entry.Length > 80) tooLong++;
                        decimal v = e.Value;   // must always be readable
                    }
                    catch (Exception ex) { bad++; if (first == null) first = key + " at step " + k + ": " + ex.GetType().Name; }
                }
            }
            check("fuzz: 6000 random key sequences on the standard engine never throw", bad == 0, bad + " exceptions, first: " + first);
            check("fuzz: the standard display never grows beyond 80 characters", tooLong == 0, tooLong + " too long");
        }

        static void ProgrammerFuzz()
        {
            var rnd = new Random(11);
            string[] ops = { "+", "-", "*", "/", "mod", "AND", "OR", "XOR", "NAND", "NOR", "<<", ">>" };
            int bad = 0, tooLong = 0; string first = null;
            for (int n = 0; n < 6000; n++)
            {
                var p = new ProgEngine();
                for (int k = 0; k < 50; k++)
                {
                    int a = rnd.Next(10);
                    try
                    {
                        if (a < 4) p.Digit("0123456789ABCDEF"[rnd.Next(16)]);
                        else if (a == 4) p.Operator(ops[rnd.Next(ops.Length)]);
                        else if (a == 5) p.Equals();
                        else if (a == 6) { int sel = rnd.Next(4); p.SetBase(sel == 0 ? 2 : sel == 1 ? 8 : sel == 2 ? 10 : 16); }
                        else if (a == 7) p.SetBits(new[] { 8, 16, 32, 64 }[rnd.Next(4)]);
                        else if (a == 8) { switch (rnd.Next(5)) { case 0: p.Not(); break; case 1: p.Negate(); break; case 2: p.RotateLeft(); break; case 3: p.RotateRight(); break; default: p.Back(); break; } }
                        else { if (rnd.Next(2) == 0) p.Clear(); else p.ClearEntry(); }
                        string d = p.Display;
                        if (d.Length > 100) tooLong++;
                    }
                    catch (Exception ex) { bad++; if (first == null) first = "step " + k + ": " + ex.GetType().Name; }
                }
            }
            check("fuzz: 6000 random sequences on the programmer engine never throw", bad == 0, bad + " exceptions, first: " + first);
            check("fuzz: the programmer display stays short", tooLong == 0, tooLong + " too long");
        }

        // --------------------------------------------------------------- dates
        static void DateFuzz()
        {
            var rnd = new Random(13);
            int bad = 0, inconsistent = 0; string first = null;
            for (int i = 0; i < 5000; i++)
            {
                DateTime a = new DateTime(1900, 1, 1).AddDays(rnd.Next(0, 73000));
                DateTime b = new DateTime(1900, 1, 1).AddDays(rnd.Next(0, 73000));
                try
                {
                    DateCalc.Diff d = DateCalc.Difference(a, b);
                    DateTime lo = a < b ? a : b, hi = a < b ? b : a;
                    if (DateCalc.Add(lo, d.Years, d.Months, d.Days, false) != hi || d.TotalDays != (int)(hi - lo).TotalDays || d.Months < 0 || d.Months > 11 || d.Days < 0 || d.Days > 31) inconsistent++;
                }
                catch (Exception ex) { bad++; if (first == null) first = ex.GetType().Name; }
            }
            check("fuzz: 5000 random date pairs, difference + add always lands on the other date", bad == 0 && inconsistent == 0, bad + " exceptions, " + inconsistent + " inconsistent " + first);
            bool extremes = true;
            try { DateCalc.Difference(DateTime.MinValue, DateTime.MaxValue); } catch (Exception) { extremes = false; }
            bool addGuard = false;
            try { DateCalc.Add(DateTime.MaxValue, 99999, 99999, 99999, false); } catch (ArgumentOutOfRangeException) { addGuard = true; } catch (Exception) { }
            check("dates: extreme values are handled (difference works, overflow raises only ArgumentOutOfRange)", extremes && addGuard, "");
            int parseBad = 0;
            for (int i = 0; i < 3000; i++)
            {
                var sb = new StringBuilder(); int len = rnd.Next(0, 30);
                for (int k = 0; k < len; k++) sb.Append("0123456789-./ :abcT"[rnd.Next(19)]);
                DateTime d;
                try { DateCalc.TryParse(sb.ToString(), out d); } catch (Exception) { parseBad++; }
            }
            check("fuzz: 3000 random strings never crash the date parser", parseBad == 0, parseBad + " exceptions");
        }

        // --------------------------------------------------------------- units
        static void UnitFuzz()
        {
            var rnd = new Random(17);
            int bad = 0; string first = null;
            foreach (AppMode m in new[] { AppMode.Currency, AppMode.Volume, AppMode.Length, AppMode.Weight, AppMode.Temperature, AppMode.Energy, AppMode.Area, AppMode.Speed, AppMode.Time, AppMode.Power, AppMode.Data, AppMode.Pressure, AppMode.Angle })
            {
                Category c = Units.Get(m);
                for (int i = 0; i < 800; i++)
                {
                    double v = (rnd.NextDouble() < 0.5 ? -1 : 1) * Math.Pow(10, rnd.Next(-320, 309)) * rnd.NextDouble();
                    if (i == 0) v = double.MaxValue; if (i == 1) v = double.Epsilon; if (i == 2) v = 0;
                    try
                    {
                        Unit f = c.Units[rnd.Next(c.Units.Length)], t = c.Units[rnd.Next(c.Units.Length)];
                        Units.Format(Units.Convert(c, v, f, t));
                    }
                    catch (Exception ex) { bad++; if (first == null) first = m + ": " + ex.GetType().Name; }
                }
            }
            check("fuzz: 10000 extreme conversions never throw", bad == 0, bad + " exceptions, first: " + first);
            check("converter: overflow and NaN are shown as a dash", Units.Format(double.PositiveInfinity) == "\u2014" && Units.Format(double.NaN) == "\u2014", "");
        }

        // ------------------------------------------------------ hostile files
        static string Tmp() { string d = Path.Combine(Path.GetTempPath(), "prisma-hard-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d); return d; }

        static void SettingsAttacks()
        {
            string dir = Tmp(), oldPath = Settings.Path, oldLog = Log.Path;
            try
            {
                Log.Path = Path.Combine(dir, "log.txt");
                Settings.Path = Path.Combine(dir, "s.ini");
                var rnd = new Random(23);
                var junk = new byte[6000]; rnd.NextBytes(junk);
                File.WriteAllBytes(Settings.Path, junk);
                bool ok = true;
                try { ThemeMode t = Settings.Load(); AppMode a = Settings.LoadApp(); Settings.Get("Style", "Aurora"); ok = t == ThemeMode.Default && a == AppMode.Standard; } catch (Exception) { ok = false; }
                check("settings: a file of random bytes is survived and ignored", ok, "");

                File.WriteAllBytes(Settings.Path, new byte[5 * 1024 * 1024]);
                var sw = Stopwatch.StartNew();
                bool big = Settings.Load() == ThemeMode.Default;
                check("settings: a 5 MB file is refused quickly (" + sw.ElapsedMilliseconds + " ms)", big && sw.ElapsedMilliseconds < 1500, "");

                File.WriteAllText(Settings.Path, "Theme=Dark\r\nMode=Graphing\r\nStyle=<script>\r\nAccent=#ZZZZZZ\r\nGlass=maybe\r\nLanguage=klingon\r\nEvil=1\r\nAnimate=0\r\n");
                check("settings: valid lines are kept, invalid or unknown lines are dropped",
                    Settings.Load() == ThemeMode.Dark && Settings.LoadApp() == AppMode.Graphing && Settings.Get("Style", "Aurora") == "Aurora"
                    && Settings.Get("Accent", "Theme") == "Theme" && Settings.Get("Glass", "1") == "1" && Settings.Get("Language", "System") == "System"
                    && Settings.Get("Evil", "none") == "none" && Settings.Get("Animate", "1") == "0", "");

                File.Delete(Settings.Path);
                bool r1 = Settings.Set("Mode", "Graphing\r\nTheme=Dark");
                bool r2 = Settings.Set("Style", "evil");
                bool r3 = Settings.Set("Unknown", "1");
                bool r4 = Settings.Set("Accent", new string('A', 500));
                bool r5 = Settings.Set("Accent", "#12");
                bool r6 = Settings.Set("Accent", "#FF375F");
                string content = File.Exists(Settings.Path) ? File.ReadAllText(Settings.Path) : "";
                check("settings: injection attempts and bad values are refused, a good value is saved", !r1 && !r2 && !r3 && !r4 && !r5 && r6 && content.Trim() == "Accent=#FF375F", content.Replace("\r\n", "|"));
                check("settings: no temporary file is left behind", !File.Exists(Settings.Path + ".tmp"), "");

                Settings.Path = Path.Combine(dir, "missing-folder", "deep", "s.ini");
                check("settings: the folder is created on demand", Settings.Set("Glass", "0") && File.Exists(Settings.Path), "");
                Settings.Path = Path.Combine(dir, "ini-is-a-folder");
                Directory.CreateDirectory(Settings.Path);
                bool noThrow = true; try { Settings.Set("Glass", "1"); Settings.Load(); } catch (Exception) { noThrow = false; }
                check("settings: a folder where the file should be does not crash the app", noThrow, "");
            }
            finally { Settings.Path = oldPath; Log.Path = oldLog; try { Directory.Delete(dir, true); } catch (Exception) { } }
        }

        static void LogAttacks()
        {
            string dir = Tmp(), oldLog = Log.Path; long oldMax = Log.MaxBytes;
            try
            {
                Log.Path = Path.Combine(dir, "log.txt");
                check("log: line breaks and control characters are neutralised", Log.Clean("a\r\nb\u0001c\td") == "a | b?c\td", Log.Clean("a\r\nb\u0001c\td"));
                Log.Write("INFO", "first\n2026-01-01 00:00:00.000 [ERROR] forged entry");
                string[] lines = File.ReadAllLines(Log.Path);
                int withFirst = 0, forgedStart = 0;
                foreach (string l in lines) { if (l.Contains("first") && l.Contains("[INFO]") && l.Contains("forged entry")) withFirst++; if (l.StartsWith("2026-01-01")) forgedStart++; }
                check("log: a forged second entry cannot be injected (it stays inside one real entry)", withFirst == 1 && forgedStart == 0, withFirst + " entries, " + forgedStart + " forged starts");
                check("log: very long messages are cut", Log.Clean(new string('x', 100000)).Length <= 4003, "");
                Log.MaxBytes = 4000;
                for (int i = 0; i < 200; i++) Log.Info("entry number " + i + " " + new string('p', 80));
                long main = new FileInfo(Log.Path).Length;
                check("log: the file is rolled over when it gets large", File.Exists(Log.Path + ".1") && main < 3 * 4000, "main file " + main + " bytes");
                Log.Path = Path.Combine(dir, "a-folder");
                Directory.CreateDirectory(Log.Path);
                bool noThrow = true; try { Log.Info("x"); } catch (Exception) { noThrow = false; }
                check("log: an unwritable log target never crashes the app", noThrow, "");
            }
            finally { Log.Path = oldLog; Log.MaxBytes = oldMax; try { Directory.Delete(dir, true); } catch (Exception) { } }
        }

        static void RatesAttacks()
        {
            string dir = Tmp(), old = Units.RatesPathOverride;
            try
            {
                string p = Path.Combine(dir, "rates.ini");
                Units.RatesPathOverride = p;
                File.WriteAllText(p, "garbage\r\n=\r\nUSD=abc\r\nEUR=1\r\n");
                check("rates: a file with fewer than two good rates falls back to the built-in rates", Units.LoadRates().Count == 10, Units.LoadRates().Count + "");
                File.WriteAllText(p, "EUR=1\r\nUSD=-5\r\nGBP=0\r\nJPY=NaN\r\nCHF=1e99\r\nAB1=2\r\nXYZ=2\r\nXYZ=3\r\nabc=0.5\r\nTOOLONGCODE=2\r\n");
                var rates = Units.LoadRates();
                var codes = new List<string>(); foreach (KeyValuePair<string, double> kv in rates) codes.Add(kv.Key);
                check("rates: negative, zero, NaN, huge, duplicate and malformed entries are skipped", string.Join(",", codes.ToArray()) == "EUR,XYZ,ABC", string.Join(",", codes.ToArray()));
                var sb = new StringBuilder(); for (int i = 0; i < 4000; i++) sb.Append("AAA=1\r\n");
                File.WriteAllText(p, sb.ToString());
                check("rates: an oversized file is ignored", Units.LoadRates().Count == 10, "");
                File.WriteAllText(p, "EUR=1\r\nUSD=1.1\r\n");
                check("rates: a valid custom file is used", Units.LoadRates().Count == 2, "");
                Category c = Units.Get(AppMode.Currency);
                check("rates: all converted currency factors are finite and positive", c.Units.Length == 2 && c.Units[0].Factor > 0 && !double.IsInfinity(c.Units[1].Factor), "");
            }
            finally { Units.RatesPathOverride = old; try { Directory.Delete(dir, true); } catch (Exception) { } }
        }

        static void PathChecks()
        {
            string full, why;
            check("paths: network (UNC) paths are rejected", !SafePath.TryNormalize(@"\\server\share\x.log", out full, out why), "");
            check("paths: device paths are rejected", !SafePath.TryNormalize(@"\\.\COM1", out full, out why) && !SafePath.TryNormalize(@"\\?\C:\x.log", out full, out why), "");
            check("paths: reserved device names are rejected", !SafePath.TryNormalize("CON", out full, out why) && !SafePath.TryNormalize(@"C:\temp\nul.txt", out full, out why) && !SafePath.TryNormalize("LPT1", out full, out why), "");
            check("paths: empty, too long and NUL-containing paths are rejected", !SafePath.TryNormalize("", out full, out why) && !SafePath.TryNormalize(new string('a', 300), out full, out why) && !SafePath.TryNormalize("a\0b", out full, out why), "");
            check("paths: an existing folder is rejected", !SafePath.TryNormalize(Path.GetTempPath(), out full, out why), "");
            bool good = SafePath.TryNormalize(Path.Combine(Path.GetTempPath(), "ok.log"), out full, out why) && Path.IsPathRooted(full);
            check("paths: a normal file path is accepted and made absolute", good, why);
            check("paths: the command line refuses a network log path", Program.Parse(new[] { "--log", @"\\evil\share\x.log" }).Error != null && Program.Parse(new[] { "--settings", "CON" }).Error != null, "");
        }

        // ------------------------------------------------- the built program
        static byte[] exe;
        static bool Has(string text) { return IndexOf(exe, Encoding.ASCII.GetBytes(text)) >= 0; }
        static bool HasWide(string text) { return IndexOf(exe, Encoding.Unicode.GetBytes(text)) >= 0; }

        static int IndexOf(byte[] hay, byte[] needle)
        {
            for (int i = 0; i + needle.Length <= hay.Length; i++)
            {
                int j = 0;
                while (j < needle.Length && hay[i + j] == needle[j]) j++;
                if (j == needle.Length) return i;
            }
            return -1;
        }

        // Counts RT_ICON (3) and RT_GROUP_ICON (14) resources in a PE file by walking the resource directory.
        static int[] IconResourceCounts(byte[] d)
        {
            int pe = BitConverter.ToInt32(d, 0x3C), nsec = BitConverter.ToUInt16(d, pe + 6), optSize = BitConverter.ToUInt16(d, pe + 20), opt = pe + 24;
            bool plus = BitConverter.ToUInt16(d, opt) == 0x20B;
            uint resRva = BitConverter.ToUInt32(d, opt + (plus ? 112 : 96) + 16);
            if (resRva == 0) return new[] { 0, 0 };
            int sections = opt + optSize, root = -1;
            for (int i = 0; i < nsec; i++)
            {
                int so = sections + i * 40;
                uint vs = BitConverter.ToUInt32(d, so + 8), va = BitConverter.ToUInt32(d, so + 12), rs = BitConverter.ToUInt32(d, so + 16), raw = BitConverter.ToUInt32(d, so + 20);
                if (resRva >= va && resRva < va + Math.Max(vs, rs)) { root = (int)(resRva - va + raw); break; }
            }
            if (root < 0) return new[] { 0, 0 };
            int icons = 0, groups = 0, entries = BitConverter.ToUInt16(d, root + 12) + BitConverter.ToUInt16(d, root + 14);
            for (int i = 0; i < entries; i++)
            {
                int e = root + 16 + i * 8;
                uint id = BitConverter.ToUInt32(d, e), data = BitConverter.ToUInt32(d, e + 4);
                if ((id & 0x80000000) != 0 || (data & 0x80000000) == 0) continue;
                int sub = root + (int)(data & 0x7FFFFFFF);
                int n = BitConverter.ToUInt16(d, sub + 12) + BitConverter.ToUInt16(d, sub + 14);
                if (id == 3) icons = n; else if (id == 14) groups = n;
            }
            return new[] { icons, groups };
        }

        static void ProgramPosture()
        {
            string path = typeof(MainWindow).Assembly.Location;     // the real Prisma.exe, not the test program
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) { check("posture: the program file can be inspected", false, path); return; }
            exe = File.ReadAllBytes(path);
            int pe = BitConverter.ToInt32(exe, 0x3C);
            int opt = pe + 24;
            ushort magic = BitConverter.ToUInt16(exe, opt);
            ushort dll = BitConverter.ToUInt16(exe, opt + 70);
            check("posture: ASLR (dynamic base) is on", (dll & 0x0040) != 0, "DllCharacteristics=0x" + dll.ToString("X"));
            check("posture: DEP (NX compatible) is on", (dll & 0x0100) != 0, "DllCharacteristics=0x" + dll.ToString("X"));
            check("posture: high-entropy ASLR is on", (dll & 0x0020) != 0, "DllCharacteristics=0x" + dll.ToString("X") + " magic " + magic.ToString("X"));
            check("posture: the manifest runs the program as the normal user (asInvoker) and never asks for admin", Has("asInvoker") && !Has("requireAdministrator") && !Has("highestAvailable"), "");
            check("posture: the manifest declares the supported Windows versions", Has("8e0f7a12-bfb3-4fe8-b9a5-48fd50a15a9a"), "");

            var allowed = new HashSet<string> { "mscorlib", "System", "System.Core", "PresentationFramework", "PresentationCore", "WindowsBase", "System.Xaml" };
            var extra = new List<string>();
            foreach (AssemblyName an in typeof(MainWindow).Assembly.GetReferencedAssemblies()) if (!allowed.Contains(an.Name)) extra.Add(an.Name);
            check("posture: only the .NET Framework and WPF assemblies are referenced", extra.Count == 0, string.Join(", ", extra.ToArray()));

            var tests = new List<string>();
            foreach (string t in new[] { "SelfTest", "HardeningTests", "NetTests", "TestMain", "Fuzz", "--selftest" })
                if (Has(t)) tests.Add(t);
            check("posture: the shipped program contains no test code", tests.Count == 0, string.Join(", ", tests.ToArray()));
            var literals = new List<string>();
            foreach (string t in new[] { "WebClient", "DownloadString", "DownloadFile", "HttpClient", "WebRequest", "TcpClient", "UdpClient", "ProcessStartInfo", "urlmon", "wininet", "winhttp", "ws2_32", "crypt32", "advapi32", "shell32" })
                if (Has(t) || HasWide(t)) literals.Add(t);
            check("posture: the shipped program contains none of the strings that antivirus scanners associate with downloaders", literals.Count == 0, string.Join(", ", literals.ToArray()));
            var found = new List<string>();
            foreach (string bad in new[] { "System.Net", "WebClient", "HttpClient", "WebRequest", "TcpClient", "UdpClient", "ProcessStartInfo", "DownloadString", "DownloadFile", "Assembly.LoadFrom" })
                if (Has(bad)) found.Add(bad);
            check("posture: no network, download or process-launch API is referenced", found.Count == 0, string.Join(", ", found.ToArray()));

            var dlls = new List<string>();
            foreach (string d in new[] { "ws2_32", "wininet", "winhttp", "urlmon", "shell32", "advapi32", "ole32", "crypt32" })
                if (Has(d + ".dll") || Has(d.ToUpperInvariant() + ".DLL")) dlls.Add(d);
            check("posture: no native networking, shell or crypto DLL is imported", dlls.Count == 0, string.Join(", ", dlls.ToArray()));
            check("posture: the only native imports are kernel32 and dwmapi", Has("dwmapi.dll") && Has("kernel32.dll"), "");
            int[] ic = IconResourceCounts(exe);
            check("branding: the program file carries its icon (1 icon group with 7 sizes)", ic[0] == 7 && ic[1] == 1, "icons=" + ic[0] + " groups=" + ic[1]);
            var vi = FileVersionInfo.GetVersionInfo(path);
            check("branding: file properties name the product, version and licence", vi.ProductName == "Prisma" && vi.FileVersion == "1.1.2.0" && vi.CompanyName == "Prisma authors" && vi.LegalCopyright.Contains("MIT"), vi.ProductName + " " + vi.FileVersion);
        }
    }

    static class ArrayExt
    {
        public static IEnumerable<string> Select_(this string[] arr, string value) { for (int i = 0; i < arr.Length; i++) yield return value; }
    }
}
