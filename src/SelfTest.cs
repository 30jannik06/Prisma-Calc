// Prisma - self tests. No window is shown, no network, no account. C# 5.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Prisma
{
    public static class SelfTest
    {
        static int pass, fail;

        static void Check(string name, bool ok, string detail)
        {
            if (ok) pass++; else fail++;
            string line = (ok ? "PASS " : "FAIL ") + name + (ok ? "" : " -> " + detail);
            Console.Out.Write(line + "\r\n");
            Log.Info("selftest " + line);
        }

        static void Eq(string name, string actual, string expected) { Check(name, actual == expected, "got '" + actual + "', expected '" + expected + "'"); }

        static string Ev(string e, AngleMode am)
        {
            try { return Expr.Format(Expr.Eval(e, am)); } catch (CalcException ex) { return "ERR:" + ex.Message; }
        }

        static void Pump(int ms)
        {
            var frame = new DispatcherFrame();
            var guard = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ms) };
            guard.Tick += delegate { guard.Stop(); frame.Continue = false; };
            guard.Start(); Dispatcher.PushFrame(frame);
        }

        public static int Run()
        {
            pass = 0; fail = 0;
            string oldLang = Loc.Lang; Loc.Lang = "en";
            ThemeTests();
            LocTests();
            RadiusTests();
            LayoutTests();
            BrandingTests();
            SettingsTests();
            ParserTests();
            StandardTests();
            ProgrammerTests();
            DateTests();
            UnitTests();
            ArgTests();
            HardeningTests.Run(Check);
            try { UiTests(); }
            catch (Exception ex) { Check("ui tests ran", false, ex.ToString()); }
            Loc.Lang = oldLang;
            Console.Out.Write("RESULT " + pass + " passed, " + fail + " failed\r\n");
            return fail == 0 ? 0 : 1;
        }

        // ------------------------------------------------------------- theme
        static string Worst(string style, EffectiveTheme t, bool glass, Color? accent)
        {
            var d = ThemeManager.Build(style, t, accent, glass);
            var bad = new List<string>();
            if (ThemeManager.WorstContrast(d["Text"], d, null) < 4.5) bad.Add("Text on background");
            if (ThemeManager.WorstContrast(d["SubText"], d, null) < 4.0) bad.Add("SubText on background");
            if (ThemeManager.WorstContrast(d["NumText"], d, "NumBg") < 4.5) bad.Add("NumText on NumBg");
            if (ThemeManager.WorstContrast(d["FuncText"], d, "FuncBg") < 4.5) bad.Add("FuncText on FuncBg");
            if (ThemeManager.WorstContrast(d["SubText"], d, "FuncBg") < 4.0) bad.Add("SubText on FuncBg");
            if (ThemeManager.WorstContrast(d["Text"], d, "Panel") < 4.5) bad.Add("Text on Panel");
            if (ThemeManager.Contrast(d["AccentText"], d["AccentBg"]) < 4.5 || ThemeManager.Contrast(d["AccentText"], d["AccentBg2"]) < 4.5) bad.Add("AccentText on accent");
            if (ThemeManager.Contrast(d["NumText"], d["PopupBg"]) < 4.5) bad.Add("NumText on PopupBg");
            Color[] cur = ThemeManager.CurveColors(t);
            foreach (Color c in cur) if (ThemeManager.Contrast(c, d["GraphBg"]) < 3.0) { bad.Add("curve colour " + c + " on GraphBg"); break; }
            foreach (string k in ThemeManager.Keys) if (!d.ContainsKey(k)) bad.Add("missing key " + k);
            if (style != "Classic" && ThemeManager.WorstContrast(d["Brand"], d, null) < 3.0) bad.Add("Brand on background");
            return string.Join("; ", bad.ToArray());
        }

        static void ThemeTests()
        {
            foreach (string style in ThemeManager.Styles)
            {
                foreach (EffectiveTheme t in new[] { EffectiveTheme.Light, EffectiveTheme.Dark })
                {
                    var problems = new List<string>();
                    foreach (bool glass in new[] { true, false })
                        foreach (Color? accent in new Color?[] { null, Color.FromRgb(0xFF, 0xB9, 0x00), Color.FromRgb(0xE8, 0x11, 0x23), Color.FromRgb(0x0A, 0x84, 0xFF) })
                        {
                            string w = Worst(style, t, glass, accent);
                            // an accent chosen by the user may be too faint to use as brand text; that is handled by the Brand fallback
                            if (w.Length > 0) problems.Add("glass=" + glass + " accent=" + (accent.HasValue ? accent.Value.ToString() : "default") + ": " + w);
                        }
                    Check("theme " + style + " " + t + " is readable (glass on/off, 4 accents)", problems.Count == 0, string.Join(" | ", problems.ToArray()));
                }
            }
            var hc = ThemeManager.Build("Aurora", EffectiveTheme.HighContrast, null, true);
            Check("high contrast is flat: no glass, no sheen, no blobs", hc["Sheen"].A == 0 && hc["Blob1"].A == 0 && hc["NumBg"].A == 255 && hc["BgA"] == hc["BgB"], "");
            var on = ThemeManager.Build("Aurora", EffectiveTheme.Dark, null, true);
            var off = ThemeManager.Build("Aurora", EffectiveTheme.Dark, null, false);
            Check("glass on is translucent, glass off is opaque", on["NumBg"].A < 255 && off["NumBg"].A == 255 && off["Sheen"].A == 0 && off["Blob1"].A == 0, "");
            Check("accent override changes the accent", ThemeManager.Build("Sunset", EffectiveTheme.Dark, Color.FromRgb(1, 2, 3), true)["AccentBg"] == Color.FromRgb(1, 2, 3), "");
            Check("7 styles, 6 with glass, all have previews", ThemeManager.Styles.Length == 7 && ThemeManager.IsGlassStyle("Aurora") && !ThemeManager.IsGlassStyle("Classic")
                && ThemeManager.Preview("Ocean", true).Length == 3 && ThemeManager.Preview("Classic", false).Length == 3, "");
            Check("unknown style falls back to the default", ThemeManager.NormalizeStyle("nope") == ThemeManager.DefaultStyle && ThemeManager.NormalizeStyle("rOsE") == "Rose", "");
            Color hx;
            Check("hex colour parsing", ThemeManager.TryParseHex("#FF375F", out hx) && hx == Color.FromRgb(0xFF, 0x37, 0x5F) && !ThemeManager.TryParseHex("zzz", out hx) && !ThemeManager.TryParseHex("Theme", out hx), "");
            Check("composite maths", ThemeManager.Composite(Color.FromArgb(128, 255, 255, 255), Colors.Black).R == 128, "");
            Check("resolve default+osLight", ThemeManager.Resolve(ThemeMode.Default, true, false) == EffectiveTheme.Light, "");
            Check("resolve default+osDark", ThemeManager.Resolve(ThemeMode.Default, false, false) == EffectiveTheme.Dark, "");
            Check("resolve light overrides osDark", ThemeManager.Resolve(ThemeMode.Light, false, false) == EffectiveTheme.Light, "");
            Check("resolve dark overrides osLight", ThemeManager.Resolve(ThemeMode.Dark, true, false) == EffectiveTheme.Dark, "");
            Check("resolve high contrast wins", ThemeManager.Resolve(ThemeMode.Dark, true, true) == EffectiveTheme.HighContrast
                && ThemeManager.Resolve(ThemeMode.Light, false, true) == EffectiveTheme.HighContrast, "");
            bool osOk = true; try { ThemeManager.OsUsesLightApps(); ThemeManager.HighContrastOn(); ThemeManager.SystemAccent(); } catch (Exception) { osOk = false; }
            Check("OS theme readers do not throw", osOk, "");
        }

        // ------------------------------------------------------------ window placement
        static void LayoutTests()
        {
            Rect wa = new Rect(0, 0, 1920, 1040);
            Rect tall = MainWindow.ClampRect(new Rect(300, 400, 700, 850), wa);
            Check("placement: a window that grew past the taskbar is moved up", tall.Bottom <= 1040 + 1e-9 && tall.Top >= 0 && tall.Width == 700 && tall.Height == 850, tall.ToString());
            Rect wide = MainWindow.ClampRect(new Rect(1500, 100, 800, 600), wa);
            Check("placement: a window that grew past the right edge is moved left", wide.Right <= 1920 + 1e-9 && wide.Left >= 0, wide.ToString());
            Rect huge = MainWindow.ClampRect(new Rect(-50, -50, 3000, 2000), wa);
            Check("placement: a window larger than the screen is cut to the work area", huge.Width == 1920 && huge.Height == 1040 && huge.Left == 0 && huge.Top == 0, huge.ToString());
            Rect ok = new Rect(100, 100, 600, 500);
            Check("placement: a window that already fits is not moved", MainWindow.ClampRect(ok, wa) == ok, "");
            Size work = new Size(1920, 1040), pref = new Size(800, 720), min = new Size(600, 560);
            Check("size: without a user choice the mode's preferred size is used", MainWindow.TargetSize(false, new Size(1000, 900), pref, min, work) == pref, "");
            Check("size: a size chosen by the user is kept even if the mode prefers another", MainWindow.TargetSize(true, new Size(1000, 900), pref, min, work) == new Size(1000, 900), "");
            Check("size: a user size below the mode's minimum is raised to the minimum", MainWindow.TargetSize(true, new Size(300, 300), pref, min, work) == min, "");
            Check("size: a user size larger than the screen is cut to the work area", MainWindow.TargetSize(true, new Size(5000, 4000), pref, min, work) == work, "");
            Check("size: window size settings are validated", Settings.IsValid("WindowWidth", "900") && !Settings.IsValid("WindowWidth", "50") && !Settings.IsValid("WindowHeight", "99999") && !Settings.IsValid("WindowHeight", "-700") && !Settings.IsValid("WindowWidth", "1e3") && !Settings.IsValid("WindowWidth", "9 0"), "");
            Rect other = new Rect(2500, 100, 600, 500);
            Check("placement: a window on another monitor is left alone", MainWindow.ClampRect(other, wa) == other, "");
        }

        // ---------------------------------------------------------------- branding
        static void BrandingTests()
        {
            Stream st = Assembly.GetExecutingAssembly().GetManifestResourceStream("Prisma.ico");
            Check("branding: the icon is embedded in the program", st != null, "");
            if (st == null) return;
            byte[] ico;
            using (var ms = new MemoryStream()) { st.CopyTo(ms); ico = ms.ToArray(); st.Dispose(); }
            int count = BitConverter.ToUInt16(ico, 4);
            var sizes = new List<int>(); bool structure = BitConverter.ToUInt16(ico, 0) == 0 && BitConverter.ToUInt16(ico, 2) == 1 && count == 7;
            for (int i = 0; i < count && structure; i++)
            {
                int o = 6 + 16 * i; int w = ico[o] == 0 ? 256 : ico[o]; sizes.Add(w);
                int len = (int)BitConverter.ToUInt32(ico, o + 8), off = (int)BitConverter.ToUInt32(ico, o + 12);
                if (off < 0 || len <= 0 || off + len > ico.Length) { structure = false; break; }
                bool png = ico[off] == 0x89 && ico[off + 1] == 0x50 && ico[off + 2] == 0x4E && ico[off + 3] == 0x47;
                bool dib = BitConverter.ToUInt32(ico, off) == 40 && BitConverter.ToInt32(ico, off + 4) == w && BitConverter.ToInt32(ico, off + 8) == 2 * w;
                if (w == 256 ? !png : !dib) structure = false;
            }
            sizes.Sort();
            Check("branding: the icon holds 16, 24, 32, 48, 64, 128 and 256 px (bitmaps below 256, PNG at 256)", structure && string.Join(",", sizes.ConvertAll(x => x.ToString()).ToArray()) == "16,24,32,48,64,128,256", string.Join(",", sizes.ConvertAll(x => x.ToString()).ToArray()));
            var dec = new IconBitmapDecoder(new MemoryStream(ico), BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            var widths = new List<int>(); foreach (BitmapFrame f in dec.Frames) widths.Add(f.PixelWidth);
            widths.Sort();
            Check("branding: Windows' image decoder reads all 7 icon sizes", string.Join(",", widths.ConvertAll(x => x.ToString()).ToArray()) == "16,24,32,48,64,128,256", string.Join(",", widths.ConvertAll(x => x.ToString()).ToArray()));
            var a = Assembly.GetExecutingAssembly().GetName();
            Check("branding: version is 1.0.0.0", a.Version.ToString() == "1.0.0.0", a.Version.ToString());

            if (Application.Current == null) new Application();
            string oldS = Settings.Path;
            Settings.Path = Path.Combine(Path.GetTempPath(), "prisma-brand-" + Guid.NewGuid().ToString("N") + ".ini");
            try
            {
                var w = new MainWindow(ThemeMode.Dark, AppMode.Standard);
                var bf = w.Icon as BitmapSource;
                Check("branding: the window has the icon", bf != null && bf.PixelWidth >= 16, "");
                foreach (double sz in new double[] { 24, 32, 64, 128 })
                {
                    FrameworkElement logo = Ui.Logo(sz);
                    logo.Measure(new Size(sz, sz)); logo.Arrange(new Rect(0, 0, sz, sz)); logo.UpdateLayout();
                    int px = (int)sz;
                    var rtb = new RenderTargetBitmap(px, px, 96, 96, PixelFormats.Pbgra32);
                    rtb.Render(logo);
                    var data = new byte[px * px * 4]; rtb.CopyPixels(data, px * 4, 0);
                    var colours = new HashSet<int>(); for (int i = 0; i < data.Length; i += 4) colours.Add(data[i] | (data[i + 1] << 8) | (data[i + 2] << 16));
                    bool cornerClear = data[3] < 40;                       // top-left pixel (alpha) is outside the rounded tile
                    bool centreFilled = data[((px / 2) * px + px / 2) * 4 + 3] > 200;
                    Check("branding: the vector logo renders at " + px + " px (rounded tile, several colours)", colours.Count >= 6 && cornerClear && centreFilled, colours.Count + " colours, corner alpha " + data[3]);
                }
            }
            finally { try { File.Delete(Settings.Path); } catch (Exception) { } Settings.Path = oldS; }
        }

        // ------------------------------------------------------- corner radii
        static void RadiusTests()
        {
            double old = Ui.CornerScale;
            try
            {
                Ui.CornerScale = 1.0;
                Check("corners: a tall key (66 px) gets the maximum radius of 12", Math.Abs(Ui.Radius(66) - 12) < 1e-9, Ui.Radius(66).ToString());
                double narrow = Ui.Radius(28);
                Check("corners: a narrow button (28 px) gets a clearly smaller radius, about 6", narrow > 5 && narrow < 7, narrow.ToString());
                bool never = true;
                foreach (double sc in new[] { 0.25, 1.0, 1.7 })
                {
                    Ui.CornerScale = sc;
                    for (double h = 0; h <= 400; h += 1) { double r = Ui.Radius(h); if (r < 0 || r > 12 * sc + 1e-9 || (h >= 20 && r > h * 0.4)) never = false; }
                }
                Check("corners: no height ever becomes a pill (radius stays under 40% of the height) and none goes negative", never, "");
                Ui.CornerScale = Ui.ScaleFor("Square"); double sq = Ui.Radius(66);
                Ui.CornerScale = Ui.ScaleFor("Soft"); double so = Ui.Radius(66);
                Ui.CornerScale = Ui.ScaleFor("Round"); double ro = Ui.Radius(66);
                Check("corners: Square < Soft < Round", sq < so && so < ro, sq + " / " + so + " / " + ro);
                Ui.CornerScale = 1.0;
                var cr = (CornerRadius)RadiusConverter.Instance.Convert(66.0, typeof(CornerRadius), null, CultureInfo.InvariantCulture);
                var ci = (CornerRadius)RadiusConverter.Instance.Convert(66.0, typeof(CornerRadius), "inner", CultureInfo.InvariantCulture);
                Check("corners: the inner (sheen) radius is one less than the outer one", Math.Abs(cr.TopLeft - 12) < 1e-9 && Math.Abs(ci.TopLeft - 11) < 1e-9, cr + " / " + ci);
                Check("corners: setting values", Settings.IsValid("Corners", "Soft") && Settings.IsValid("Corners", "Square") && Settings.IsValid("Corners", "Round") && !Settings.IsValid("Corners", "Pill"), "");
            }
            finally { Ui.CornerScale = old; }
        }

        // ------------------------------------------------------ translations
        static int Count(string s, string part) { int n = 0, i = 0; while ((i = s.IndexOf(part, i, StringComparison.Ordinal)) >= 0) { n++; i += part.Length; } return n; }

        static void LocTests()
        {
            string old = Loc.Lang;
            try
            {
                var incomplete = new List<string>(); var placeholders = new List<string>();
                foreach (string k in Loc.Keys)
                {
                    string[] r = Loc.Row(k);
                    int n0 = Count(k, "{0}");
                    foreach (string t in r) { if (string.IsNullOrEmpty(t)) incomplete.Add(k); else if (Count(t, "{0}") != n0) placeholders.Add(k); }
                }
                Check("translations: every entry has German, French and Spanish text", incomplete.Count == 0, string.Join(", ", incomplete.ToArray()));
                var garbled = new List<string>();
                foreach (string k in Loc.Keys) foreach (string t in Loc.Row(k)) if (t.Contains("00E") || t.IndexOf((char)92) >= 0 || t.IndexOf((char)0xFFFD) >= 0) garbled.Add(k);
                Check("translations: no broken characters", garbled.Count == 0, string.Join(", ", garbled.ToArray()));
                Check("translations: {0} placeholders are kept in every language", placeholders.Count == 0, string.Join(", ", placeholders.ToArray()));

                var missing = new List<string>();
                foreach (AppMode m in Enum.GetValues(typeof(AppMode)))
                {
                    string key = m == AppMode.Date ? "Date calculation" : m == AppMode.Weight ? "Weight and mass" : m.ToString();
                    if (!Loc.Has(key)) missing.Add(key);
                }
                foreach (string st in ThemeManager.Styles) if (!Loc.Has(st)) missing.Add(st);
                var neutral = new HashSet<string> { "Celsius", "Fahrenheit", "Kelvin", "Bit", "Byte", "Kilobyte", "Kibibyte", "Megabyte", "Mebibyte", "Gigabyte", "Gibibyte", "Terabyte", "Tebibyte", "Pascal", "Kilopascal", "Bar", "Torr", "Acre", "Mach", "Stone" };
                foreach (AppMode m in new[] { AppMode.Volume, AppMode.Length, AppMode.Weight, AppMode.Temperature, AppMode.Energy, AppMode.Area, AppMode.Speed, AppMode.Time, AppMode.Power, AppMode.Data, AppMode.Pressure, AppMode.Angle })
                    foreach (Unit u in Units.Get(m).Units) if (!neutral.Contains(u.Name) && !Loc.Has(u.Name)) missing.Add(u.Name);
                foreach (string k in new[] { "Division by zero", "Result too large", "Invalid input", "Empty expression", "Unbalanced parentheses", "Unexpected '{0}'", "Incomplete expression", "Missing )", "Unknown name '{0}'", "Bad number '{0}'", "Unknown function '{0}'", "Undefined result", "Result is not finite" })
                    if (!Loc.Has(k)) missing.Add(k);
                Check("translations cover all modes, styles, units and error messages", missing.Count == 0, string.Join(", ", missing.ToArray()));

                Loc.Lang = "en";
                Eq("English keeps the decimal point", Loc.Num("1.5"), "1.5");
                Eq("English text is returned as is", Loc.T("History"), "History");
                Loc.Lang = "de";
                Eq("German uses the decimal comma", Loc.Num("1.5"), "1,5");
                Eq("German: History = Verlauf", Loc.T("History"), "Verlauf");
                Eq("German: format with a placeholder", Loc.F("Unexpected '{0}'", ")"), "Unerwartetes Zeichen ')'");
                Eq("German: unknown text falls back to itself", Loc.T("no such text"), "no such text");
                Eq("German date", Loc.DateText(new DateTime(2024, 2, 29)), "Donnerstag, 29.02.2024");
                Eq("German error from the parser", Ev("2+", AngleMode.Degrees), "ERR:Unvollst\u00E4ndiger Ausdruck");
                Loc.Lang = "fr";
                Eq("French: History = Historique", Loc.T("History"), "Historique");
                Eq("French plural", Loc.Plural(2, "year", "years"), "2 ans");
                Eq("French singular", Loc.Plural(1, "day", "days"), "1 jour");
                Eq("French date", Loc.DateText(new DateTime(2024, 2, 29)), "jeudi 29/02/2024");
                DateTime fr;
                Check("French reads 05/06/2025 as day/month", DateCalc.TryParse("05/06/2025", out fr) && fr.Month == 6 && fr.Day == 5, "");
                Loc.Lang = "es";
                Eq("Spanish: History = Historial", Loc.T("History"), "Historial");
                Eq("Spanish date", Loc.DateText(new DateTime(2024, 2, 29)), "jueves, 29/02/2024");
                Loc.Lang = "en";
                Eq("English date", Loc.DateText(new DateTime(2024, 2, 29)), "Thursday, 2024-02-29");
                DateTime us;
                Check("English reads 05/06/2025 as month/day", DateCalc.TryParse("05/06/2025", out us) && us.Month == 5 && us.Day == 6, "");
                Check("language resolution", Loc.Resolve("xx") == "en" && Loc.Resolve("DE") == "de" && Loc.IsSupported("fr") && !Loc.IsSupported("it") && Loc.IsSupported(Loc.Resolve("System")), "");
            }
            finally { Loc.Lang = old; }
        }

        // ---------------------------------------------------------- settings
        static void SettingsTests()
        {
            string tmp = Path.Combine(Path.GetTempPath(), "prisma-" + Guid.NewGuid().ToString("N"));
            string oldS = Settings.Path, oldL = Log.Path;
            try
            {
                Settings.Path = Path.Combine(tmp, "s.ini");
                Check("settings default when missing", Settings.Load() == ThemeMode.Default && Settings.LoadApp() == AppMode.Standard, "");
                Check("settings round trip Dark", Settings.Save(ThemeMode.Dark) && Settings.Load() == ThemeMode.Dark, "");
                Settings.Set("Mode", "Graphing");
                Check("mode persists and keeps other keys", Settings.LoadApp() == AppMode.Graphing && Settings.Load() == ThemeMode.Dark, "");
                File.WriteAllText(Settings.Path, "Theme=Purple\r\nMode=Nope\r\n");
                Check("settings invalid values -> defaults", Settings.Load() == ThemeMode.Default && Settings.LoadApp() == AppMode.Standard, "");
                ThemeMode pm; AppMode pa;
                Check("parse is case-insensitive", Settings.TryParseMode("lIgHt", out pm) && pm == ThemeMode.Light && Settings.TryParseApp("scientific", out pa) && pa == AppMode.Scientific, "");
                Log.Path = Path.Combine(tmp, "t.log");
                Log.Info("probe");
                Check("logger writes file", File.Exists(Log.Path) && File.ReadAllText(Log.Path).Contains("probe"), "");
            }
            finally
            {
                Settings.Path = oldS; Log.Path = oldL;
                try { Directory.Delete(tmp, true); } catch (Exception) { }
            }
        }

        // ------------------------------------------------------------ parser
        static void ParserTests()
        {
            AngleMode D = AngleMode.Degrees, R = AngleMode.Radians, G = AngleMode.Gradians;
            Eq("expr 1+2", Ev("1+2", D), "3");
            Eq("expr precedence 2+3*4", Ev("2+3*4", D), "14");
            Eq("expr power is right-associative 2^3^2", Ev("2^3^2", D), "512");
            Eq("expr unary minus below power -2^2", Ev("-2^2", D), "-4");
            Eq("expr implicit multiplication 2(3+4)", Ev("2(3+4)", D), "14");
            Eq("expr 0.1+0.2", Ev("0.1+0.2", D), "0.3");
            Eq("expr sin(30) degrees", Ev("sin(30)", D), "0.5");
            Eq("expr sin(pi/2) radians", Ev("sin(pi/2)", R), "1");
            Eq("expr cos(180) degrees", Ev("cos(180)", D), "-1");
            Eq("expr tan(45) degrees", Ev("tan(45)", D), "1");
            Eq("expr asin(1) degrees", Ev("asin(1)", D), "90");
            Eq("expr sin(100) gradians", Ev("sin(100)", G), "1");
            Eq("expr sqrt(16)", Ev("sqrt(16)", D), "4");
            Eq("expr cbrt(27)", Ev("cbrt(27)", D), "3");
            Eq("expr 5!", Ev("5!", D), "120");
            Eq("expr 10 mod 3", Ev("10 mod 3", D), "1");
            Eq("expr ln(e)", Ev("ln(e)", D), "1");
            Eq("expr log(1000)", Ev("log(1000)", D), "3");
            Eq("expr abs(-3)", Ev("abs(-3)", D), "3");
            Eq("expr exp(0)", Ev("exp(0)", D), "1");
            Eq("expr 2*pi", Ev("2pi", D), "6.28318530717959");
            Eq("expr scientific notation 1e3+1", Ev("1e3+1", D), "1001");
            Eq("expr unicode operators", Ev("6×7−2÷2", D), "41");
            Eq("expr missing ) is closed automatically", Ev("(1+2", D), "3");
            Check("expr 1/0 is an error", Ev("1/0", D).StartsWith("ERR:"), Ev("1/0", D));
            Check("expr sqrt(-1) is an error", Ev("sqrt(-1)", D).StartsWith("ERR:"), Ev("sqrt(-1)", D));
            Check("expr incomplete is an error", Ev("2+", D).StartsWith("ERR:"), Ev("2+", D));
            Check("expr unknown function is an error", Ev("foo(1)", D).StartsWith("ERR:"), Ev("foo(1)", D));
            Check("expr empty is an error", Ev("   ", D).StartsWith("ERR:"), Ev("   ", D));
            Func<double, double> sq = Expr.Compile("x^2", R);
            Func<double, double> inv = Expr.Compile("1/x", R);
            Check("graph function x^2 at 3", Math.Abs(sq(3) - 9) < 1e-12, "");
            Check("graph function 1/x at 0 is infinite, not an exception", double.IsInfinity(inv(0)), "");
        }

        // ---------------------------------------------------------- standard
        static string Run1(string keys)
        {
            var e = new CalcEngine();
            foreach (string k in keys.Split(' '))
            {
                if (k == "=") e.Equals(); else if (k == ".") e.Dot(); else if (k == "C") e.Clear();
                else if (k == "+/-") e.Negate(); else if (k == "BK") e.Back(); else if (k == "%") e.Percent();
                else if (k == "sqrt") e.Sqrt(); else if (k == "sqr") e.Square(); else if (k == "inv") e.Reciprocal();
                else if (k.Length == 1 && "+-*/".IndexOf(k[0]) >= 0) e.Operator(k[0]);
                else foreach (char c in k) e.Digit(c);
            }
            return e.Entry;
        }

        static void StandardTests()
        {
            Eq("standard 1+2=3", Run1("1 + 2 ="), "3");
            Eq("standard 7/2=3.5", Run1("7 / 2 ="), "3.5");
            Eq("standard 0.1+0.2=0.3 (decimal)", Run1("0 . 1 + 0 . 2 ="), "0.3");
            Eq("standard 2+3*4=20 (immediate execution)", Run1("2 + 3 * 4 ="), "20");
            Eq("standard division by zero", Run1("5 / 0 ="), "Division by zero");
            Eq("standard negate", Run1("5 +/-"), "-5");
            Eq("standard backspace", Run1("1 2 3 BK"), "12");
            Eq("standard operator replace", Run1("6 + * 2 ="), "12");
            Eq("standard recovers after error", Run1("5 / 0 = 7"), "7");
            Eq("standard 200+10% = 220", Run1("2 0 0 + 1 0 % ="), "220");
            Eq("standard 200*10% = 20", Run1("2 0 0 * 1 0 % ="), "20");
            Eq("standard sqrt(9)", Run1("9 sqrt"), "3");
            Eq("standard sqr(5)", Run1("5 sqr"), "25");
            Eq("standard 1/4", Run1("4 inv"), "0.25");
            Eq("standard 1/0", Run1("0 inv"), "Division by zero");
            Eq("standard sqrt(-4) is invalid", Run1("4 +/- sqrt"), "Invalid input");
            var e = new CalcEngine();
            e.Digit('2'); e.Operator('+'); e.Digit('3'); e.Equals();
            Check("standard reports a history entry", e.HistExpr == "2 + 3" && e.HistResult == "5", e.HistExpr + " / " + e.HistResult);
        }

        // -------------------------------------------------------- programmer
        static void ProgrammerTests()
        {
            var p = new ProgEngine();
            p.SetBase(16); p.Digit('F'); p.Digit('F');
            Eq("programmer hex FF is 255", p.FormatIn(10), "255");
            Eq("programmer 255 in binary", p.FormatIn(2), "1111 1111");
            Eq("programmer 255 in octal", p.FormatIn(8), "377");
            p.Clear(); p.SetBase(10);
            Check("programmer rejects hex digit in DEC", !p.DigitAllowed('A'), "");
            p.Digit('1'); p.Digit('2'); p.Operator("AND"); p.Digit('1'); p.Digit('0'); p.Equals();
            Eq("programmer 12 AND 10 = 8", p.FormatIn(10), "8");
            p.Clear(); p.Digit('1'); p.Digit('2'); p.Operator("OR"); p.Digit('1'); p.Digit('0'); p.Equals();
            Eq("programmer 12 OR 10 = 14", p.FormatIn(10), "14");
            p.Clear(); p.Digit('1'); p.Digit('2'); p.Operator("XOR"); p.Digit('1'); p.Digit('0'); p.Equals();
            Eq("programmer 12 XOR 10 = 6", p.FormatIn(10), "6");
            p.Clear(); p.Not();
            Eq("programmer NOT 0 = -1", p.FormatIn(10), "-1");
            Eq("programmer NOT 0 in hex is all ones", p.FormatIn(16).Replace(" ", ""), "FFFFFFFFFFFFFFFF");
            p.Clear(); p.SetBits(8); p.Digit('1'); p.Digit('2'); p.Digit('7'); p.Operator("+"); p.Digit('1'); p.Equals();
            Eq("programmer BYTE 127+1 wraps to -128", p.FormatIn(10), "-128");
            p.Clear(); p.Digit('3'); p.Digit('0'); p.Digit('0');
            Eq("programmer BYTE refuses 300", p.FormatIn(10), "30");
            p.Clear(); p.SetBits(32); p.Not();
            Eq("programmer DWORD NOT 0 in hex", p.FormatIn(16).Replace(" ", ""), "FFFFFFFF");
            p.Clear(); p.SetBits(64); p.Digit('1'); p.Operator("<<"); p.Digit('4'); p.Equals();
            Eq("programmer 1 << 4 = 16", p.FormatIn(10), "16");
            p.Clear(); p.Digit('1'); p.Digit('6'); p.Negate(); p.Operator(">>"); p.Digit('2'); p.Equals();
            Eq("programmer -16 >> 2 = -4", p.FormatIn(10), "-4");
            p.Clear(); p.SetBits(8); p.SetBase(16); p.Digit('8'); p.Digit('1'); p.RotateLeft();
            Eq("programmer BYTE 0x81 RoL = 0x03", p.FormatIn(16), "3");
            p.Clear(); p.SetBits(64); p.SetBase(10); p.Digit('5'); p.Operator("/"); p.Digit('0'); p.Equals();
            Check("programmer division by zero", p.Error, "");
            p.Clear(); p.Digit('9'); p.Operator("mod"); p.Digit('4'); p.Equals();
            Eq("programmer 9 mod 4 = 1", p.FormatIn(10), "1");
        }

        // -------------------------------------------------------------- dates
        static void DateTests()
        {
            DateTime a = new DateTime(2024, 1, 31), b = new DateTime(2024, 3, 1);
            DateCalc.Diff d = DateCalc.Difference(a, b);
            Check("date diff 2024-01-31 -> 2024-03-01 = 0y 1m 1d, 30 days", d.Years == 0 && d.Months == 1 && d.Days == 1 && d.TotalDays == 30, d.Years + "y " + d.Months + "m " + d.Days + "d " + d.TotalDays);
            d = DateCalc.Difference(new DateTime(2000, 1, 1), new DateTime(2026, 10, 13));
            Check("date diff 2000-01-01 -> 2026-10-13 = 26y 9m 12d", d.Years == 26 && d.Months == 9 && d.Days == 12, d.Years + "y " + d.Months + "m " + d.Days + "d");
            DateCalc.Diff r = DateCalc.Difference(b, a);
            Check("date diff is symmetric", r.TotalDays == 30 && r.Months == 1, "");
            Check("date add 1 month to 2024-01-31 = 2024-02-29", DateCalc.Add(a, 0, 1, 0, false) == new DateTime(2024, 2, 29), "");
            Check("date subtract 1 year from 2024-02-29 = 2023-02-28", DateCalc.Add(new DateTime(2024, 2, 29), 1, 0, 0, true) == new DateTime(2023, 2, 28), "");
            DateTime p1, p2, p3;
            Check("date parse formats", DateCalc.TryParse("2026-10-13", out p1) && DateCalc.TryParse("13.10.2026", out p2) && DateCalc.TryParse("10/13/2026", out p3) && p1 == p2 && p2 == p3, "");
            DateTime bad;
            Check("date parse rejects text", !DateCalc.TryParse("tomorrow-ish", out bad), "");
        }

        // -------------------------------------------------------------- units
        static double Cv(AppMode m, double v, string from, string to)
        {
            Category c = Units.Get(m);
            Unit f = null, t = null;
            foreach (Unit u in c.Units) { if (u.Name == from) f = u; if (u.Name == to) t = u; }
            if (f == null || t == null) throw new InvalidOperationException("unit missing " + from + "/" + to);
            return Units.Convert(c, v, f, t);
        }

        static void UnitTests()
        {
            Check("units 1 km = 1000 m", Math.Abs(Cv(AppMode.Length, 1, "Kilometer", "Meter") - 1000) < 1e-9, "");
            Check("units 1 mile = 1.609344 km", Math.Abs(Cv(AppMode.Length, 1, "Mile", "Kilometer") - 1.609344) < 1e-9, "");
            Check("units 0 C = 32 F", Math.Abs(Cv(AppMode.Temperature, 0, "Celsius", "Fahrenheit") - 32) < 1e-9, "");
            Check("units 100 C = 373.15 K", Math.Abs(Cv(AppMode.Temperature, 100, "Celsius", "Kelvin") - 373.15) < 1e-9, "");
            Check("units -40 F = -40 C", Math.Abs(Cv(AppMode.Temperature, -40, "Fahrenheit", "Celsius") + 40) < 1e-9, "");
            Check("units 1 hour = 3600 s", Math.Abs(Cv(AppMode.Time, 1, "Hour", "Second") - 3600) < 1e-9, "");
            Check("units 1 GiB = 1024 MiB", Math.Abs(Cv(AppMode.Data, 1, "Gibibyte", "Mebibyte") - 1024) < 1e-9, "");
            Check("units 180 degrees = pi radians", Math.Abs(Cv(AppMode.Angle, 180, "Degree", "Radian") - Math.PI) < 1e-12, "");
            Check("units 1 kg = 2.20462 lb", Math.Abs(Cv(AppMode.Weight, 1, "Kilogram", "Pound") - 2.20462262) < 1e-6, "");
            Check("units 1 gallon (US) = 3.78541 l", Math.Abs(Cv(AppMode.Volume, 1, "Gallon (US)", "Liter") - 3.78541178) < 1e-6, "");
            Check("units 1 hp = 745.7 W", Math.Abs(Cv(AppMode.Power, 1, "Horsepower (mechanical)", "Watt") - 745.699872) < 1e-6, "");
            Check("units 1 atm = 101325 Pa", Math.Abs(Cv(AppMode.Pressure, 1, "Atmosphere", "Pascal") - 101325) < 1e-9, "");
            Check("units 1 kWh = 3.6e6 J", Math.Abs(Cv(AppMode.Energy, 1, "Kilowatt-hour", "Joule") - 3.6e6) < 1e-3, "");
            Check("units 1 ha = 10000 m2", Math.Abs(Cv(AppMode.Area, 1, "Hectare", "Square meter") - 10000) < 1e-9, "");
            Check("units 36 km/h = 10 m/s", Math.Abs(Cv(AppMode.Speed, 36, "Kilometer per hour", "Meter per second") - 10) < 1e-9, "");
            Check("units sample rates: 10 EUR = 10.8 USD", Math.Abs(Cv(AppMode.Currency, 10, "EUR", "USD") - 10.8) < 1e-9, "");
            bool all = true;
            foreach (AppMode m in new[] { AppMode.Currency, AppMode.Volume, AppMode.Length, AppMode.Weight, AppMode.Temperature, AppMode.Energy, AppMode.Area, AppMode.Speed, AppMode.Time, AppMode.Power, AppMode.Data, AppMode.Pressure, AppMode.Angle })
            {
                Category c = Units.Get(m);
                if (c.Units.Length < 3) all = false;
                var seen = new HashSet<string>();
                foreach (Unit u in c.Units)
                {
                    if (!seen.Add(u.Name)) all = false;
                    double back = Units.Convert(c, Units.Convert(c, 7.5, u, c.Units[0]), c.Units[0], u);
                    if (Math.Abs(back - 7.5) > 1e-6 * 7.5) all = false;
                }
            }
            Check("units: 13 categories, unique names, round trips are lossless", all, "");
            Eq("units format 1000", Units.Format(1000), "1000");
        }

        // --------------------------------------------------------------- args
        static void ArgTests()
        {
            Program.Options o = Program.Parse(new[] { "--theme", "dark", "--mode", "scientific", "--log", "x.log" });
            Check("args --theme dark --mode scientific", o.Error == null && o.Theme == ThemeMode.Dark && o.Mode == AppMode.Scientific && o.LogPath != null && o.LogPath.EndsWith("x.log"), "");
            Check("args unknown option -> error", Program.Parse(new[] { "--nope" }).Error != null, "");
            Check("args bad theme -> error", Program.Parse(new[] { "--theme", "pink" }).Error != null, "");
            Check("args bad mode -> error", Program.Parse(new[] { "--mode", "banana" }).Error != null, "");
            Check("args --language de", Program.Parse(new[] { "--language", "de" }).Language == "de" && Program.Parse(new[] { "--language", "System" }).Error == null, "");
            Check("args bad language -> error", Program.Parse(new[] { "--language", "klingon" }).Error != null, "");
            Check("args missing value -> error", Program.Parse(new[] { "--theme" }).Error != null, "");
        }

        // ---------------------------------------------------------------- UI
        static void UiTests()
        {
            if (Application.Current == null) new Application();
            string oldS = Settings.Path;
            Settings.Path = Path.Combine(Path.GetTempPath(), "prisma-ui-" + Guid.NewGuid().ToString("N") + ".ini");
            try
            {
                var w = new MainWindow(ThemeMode.Dark, AppMode.Standard);
                bool hc = ThemeManager.HighContrastOn();
                bool created = true; string failed = "";
                foreach (AppMode m in Enum.GetValues(typeof(AppMode)))
                {
                    try { w.SwitchTo(m); if (w.View == null || w.View.Root == null) { created = false; failed = m.ToString(); } }
                    catch (Exception ex) { created = false; failed = m + ": " + ex.Message; }
                }
                Check("all " + Enum.GetValues(typeof(AppMode)).Length + " modes build their view", created, failed);

                var dark = ((SolidColorBrush)w.Resources["WinBg"]).Color;
                w.SetMode(ThemeMode.Light);
                var light = ((SolidColorBrush)w.Resources["WinBg"]).Color;
                Check("window applies Dark then Light palettes", hc || (dark != light && light == ThemeManager.Build(w.StyleName, EffectiveTheme.Light, null, w.Glass)["WinBg"]), "dark=" + dark + " light=" + light);
                w.Mode = ThemeMode.Dark; w.ApplyTheme(false);
                w.Mode = ThemeMode.Light; w.ApplyTheme(true);
                Check("fade starts for a real change", hc || w.Animating, "no fade started");
                Pump(700);
                var faded = ((SolidColorBrush)w.Resources["WinBg"]).Color;
                Check("fade ends on the Light palette", hc || (!w.Animating && faded == ThemeManager.Build(w.StyleName, EffectiveTheme.Light, null, w.Glass)["WinBg"]), "colour=" + faded);


                // Settings page and look preferences
                w.SwitchTo(AppMode.Settings);
                var sv = (SettingsView)w.View;
                Check("settings page shows 7 style cards and 8 accent swatches", sv.StyleCardCount == 7 && sv.AccentSwatchCount == 8, sv.StyleCardCount + " / " + sv.AccentSwatchCount);
                int changes = 0; w.PrefsChanged += delegate { changes++; };
                w.SetStyle("Sunset");
                Check("style is applied and saved", w.StyleName == "Sunset" && Settings.Get("Style", "") == "Sunset" && (hc || w.Shown("BgA") == ThemeManager.Build("Sunset", w.Current, null, w.Glass)["BgA"]), w.StyleName);
                Check("glass style shows the liquid backdrop and animates it", hc || (w.BlobsVisible && w.BlobsAnimating), "visible=" + w.BlobsVisible + " animating=" + w.BlobsAnimating);
                w.SetAnimate(false);
                Check("animation off stops the blobs but keeps them", w.BlobsVisible == !hc && !w.BlobsAnimating && Settings.Get("Animate", "") == "0", "");
                w.SetAnimate(true);
                w.SetGlass(false);
                Check("glass off makes the surfaces opaque", hc || (w.Shown("NumBg").A == 255 && !w.BlobsAnimating), "");
                w.SetGlass(true);
                w.SetStyle("Classic");
                Check("Classic style is flat (no backdrop blobs)", !w.BlobsVisible && !w.BlobsAnimating, "");
                w.SetStyle("Aurora");
                w.SetAccent("#FF375F");
                Check("custom accent is applied and saved", hc || (w.Shown("AccentBg") == Color.FromRgb(0xFF, 0x37, 0x5F) && Settings.Get("Accent", "") == "#FF375F"), w.Shown("AccentBg").ToString());
                w.SetAccent("Theme");
                Check("accent back to the theme default", hc || w.Shown("AccentBg") == ThemeManager.Build("Aurora", w.Current, null, w.Glass)["AccentBg"], "");
                w.SetMode(ThemeMode.Dark);
                w.ResetLook();
                Check("restore default look", w.StyleName == "Aurora" && w.Mode == ThemeMode.Default && w.Glass && w.Animate && w.AccentChoice == "Theme" && Settings.Get("Style", "") == "Aurora", "");
                Check("settings page refreshes on every change", changes >= 8, "changes=" + changes);
                w.SetMode(ThemeMode.Light); w.SetMode(ThemeMode.Dark);


                // Corner setting rebuilds the window and keeps the mode
                w.SwitchTo(AppMode.Settings);
                Check("settings page has three corner buttons", ((SettingsView)w.View).CornerButtonCount == 3, "");
                w.SetCorners("Round");
                Check("corners: Round is applied, saved and the mode is kept", w.Corners == "Round" && Math.Abs(Ui.CornerScale - 1.7) < 1e-9 && Settings.Get("Corners", "") == "Round" && w.Active == AppMode.Settings, w.Corners + " " + Ui.CornerScale + " " + w.Active);
                w.SetCorners("Square");
                Check("corners: Square is applied", Math.Abs(Ui.CornerScale - 0.25) < 1e-9, "");
                w.SetCorners("bogus");
                Check("corners: an invalid name is ignored", w.Corners == "Square", w.Corners);
                w.ResetLook();
                Check("restore default look also restores Soft corners", w.Corners == "Soft" && Math.Abs(Ui.CornerScale - 1.0) < 1e-9 && Settings.Get("Corners", "") == "Soft", w.Corners);

                // Standard + history + memory
                w.SwitchTo(AppMode.Standard);
                var std = (StandardView)w.View;
                int h0 = w.Ses.History.Count;
                std.Press("2"); std.Press("+"); std.Press("3"); std.Press("=");
                Check("standard view 2+3=5", std.ResultText == "5" && std.ExprText.Length > 0, std.ResultText);
                Check("standard view writes history", w.Ses.History.Count == h0 + 1 && w.Ses.History[0].Result == "5", "");
                std.Recall("42");
                Check("history recall sets the value", std.ResultText == "42", std.ResultText);
                w.Ses.Memory.Clear();
                std.Press("C"); std.Press("7");
                var host = (System.Windows.Controls.Panel)((System.Windows.Controls.Grid)std.Root).Children[1];
                ((System.Windows.Controls.Button)host.Children[4]).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)); // MS
                ((System.Windows.Controls.Button)host.Children[2]).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)); // M+
                Check("memory store and add (7, then +7 = 14)", w.Ses.Memory.Count == 1 && Math.Abs(w.Ses.Memory[0] - 14) < 1e-9, w.Ses.Memory.Count + " items");
                ((System.Windows.Controls.Button)host.Children[0]).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)); // MC
                Check("memory clear", w.Ses.Memory.Count == 0, "");

                // Scientific
                w.SwitchTo(AppMode.Scientific);
                var sci = (ScientificView)w.View;
                sci.Insert("sin("); sci.Insert("30"); sci.Insert(")"); sci.Evaluate();
                Check("scientific view sin(30)=0.5 in degrees", sci.ResultText == "0.5", sci.ResultText);
                sci.Insert("*"); sci.Insert("4"); sci.Evaluate();
                Check("scientific view continues from the last result (0.5*4=2)", sci.ResultText == "2", sci.ResultText);
                sci.Insert("1"); sci.Insert("/"); sci.Insert("0"); sci.Evaluate();
                Check("scientific view shows an error for 1/0", sci.ResultText.Length > 0 && sci.ResultText != "0", sci.ResultText);

                // Programmer
                w.SwitchTo(AppMode.Programmer);
                var prog = (ProgrammerView)w.View;
                prog.Engine.SetBase(16);
                prog.Press("F"); prog.Press("F");
                Check("programmer view shows FF as 255 / 1111 1111", prog.ValueIn(10) == "255" && prog.ValueIn(2) == "1111 1111", prog.ValueIn(10) + " / " + prog.ValueIn(2));

                // Converter
                w.SwitchTo(AppMode.Length);
                var conv = (ConverterView)w.View;
                conv.Select(4, 5); conv.SetInput("1000");
                Check("converter view 1000 m = 1 km", conv.OutputText == "1", conv.OutputText);
                w.SwitchTo(AppMode.Currency);
                var cur = (ConverterView)w.View;
                Check("currency view builds and is marked as sample", cur.Cat.Sample && cur.OutputText.Length > 0, "");

                // Date
                w.SwitchTo(AppMode.Date);
                var dv = (DateView)w.View;
                dv.SetDifference("2024-01-31", "2024-03-01");
                Check("date view difference text", dv.DifferenceText == "1 month, 1 day", dv.DifferenceText);
                dv.SetAdd("2024-01-31", "0", "1", "0", false);
                Check("date view add one month", dv.AddResultText.EndsWith("2024-02-29"), dv.AddResultText);

                // Graphing, rendered into a bitmap (nothing is shown on screen)
                w.SwitchTo(AppMode.Graphing);
                var gv = (GraphView)w.View;
                gv.SetEquation(0, "x^2-4"); gv.SetEquation(1, "1/x"); gv.SetEquation(2, "sin(");
                Check("graph view compiles valid equations and rejects broken ones", gv.Canvas.Funcs[0] != null && gv.Canvas.Funcs[1] != null && gv.Canvas.Funcs[2] == null, "");
                string an = gv.AnalyzeText(0, 600);
                Check("graph analysis finds zeros -2 and 2 and a minimum at (0, -4)", an.Contains("-2") && an.Contains("2") && an.Contains("(0, -4)"), an);
                GraphCanvas gc = gv.Canvas;
                gc.Measure(new Size(500, 400)); gc.Arrange(new Rect(0, 0, 500, 400)); gc.UpdateLayout();
                var rtb = new RenderTargetBitmap(500, 400, 96, 96, PixelFormats.Pbgra32);
                rtb.Render(gc);
                var px = new byte[500 * 400 * 4]; rtb.CopyPixels(px, 500 * 4, 0);
                var colours = new HashSet<int>();
                for (int i = 0; i < px.Length; i += 4) colours.Add(px[i] | (px[i + 1] << 8) | (px[i + 2] << 16));
                Check("graph renders grid, axes and curves (many distinct colours)", colours.Count > 6, colours.Count + " colours");
                gc.ZoomBy(2); gc.Reset();
                Check("graph zoom and reset keep working", Math.Abs(gc.Scale - 50) < 1e-9 && gc.Cx == 0, "");


                // The window keeps the size the user chose when the category changes
                Check("size: before the user resizes, no size is remembered", !w.UserSized, "");
                w.RememberUserSize(900, 700);
                w.SwitchTo(AppMode.Standard);
                bool kept1 = Math.Abs(w.Width - 900) < 1 && Math.Abs(w.Height - 700) < 1;
                w.SwitchTo(AppMode.Date);
                bool kept2 = Math.Abs(w.Width - 900) < 1 && Math.Abs(w.Height - 700) < 1;
                w.SwitchTo(AppMode.Length);
                bool kept3 = Math.Abs(w.Width - 900) < 1 && Math.Abs(w.Height - 700) < 1;
                Check("size: switching categories keeps the size the user chose (900 x 700)", w.UserSized && kept1 && kept2 && kept3, w.Width + " x " + w.Height);
                w.SwitchTo(AppMode.Graphing);
                bool grew = w.Width > 900 && Math.Abs(w.Height - 700) < 1;
                w.SwitchTo(AppMode.Standard);
                Check("size: a mode that needs more room grows the window, and going back restores the chosen size", grew && Math.Abs(w.Width - 900) < 1 && Math.Abs(w.Height - 700) < 1, w.Width + " x " + w.Height);
                Check("size: the chosen size is saved for the next start", Settings.Get("WindowWidth", "") == "900" && Settings.Get("WindowHeight", "") == "700", Settings.Get("WindowWidth", "") + " x " + Settings.Get("WindowHeight", ""));
                var w2 = new MainWindow(ThemeMode.Dark, AppMode.Scientific);
                Check("size: a new window starts at the saved size", w2.UserSized && Math.Abs(w2.Width - 900) < 1 && Math.Abs(w2.Height - 700) < 1, w2.Width + " x " + w2.Height);

                // Language switch in the running window
                w.SwitchTo(AppMode.Standard);
                int langBefore = w.Ses.History.Count;
                w.SetLanguage("de");
                Check("language switch rebuilds the window and keeps the mode", Loc.Lang == "de" && w.Active == AppMode.Standard && Settings.Get("Language", "") == "de", Loc.Lang + " " + w.Active);
                std = (StandardView)w.View;
                std.Press("4"); std.Press("inv");
                Check("German window shows the decimal comma (1/4 = 0,25)", std.ResultText == "0,25", std.ResultText);
                w.SwitchTo(AppMode.Date);
                Check("German window title for date calculation", w.TitleText == "Datumsberechnung", w.TitleText);
                dv = (DateView)w.View;
                dv.SetDifference("2024-01-31", "2024-03-01");
                Check("German date difference text", dv.DifferenceText == "1 Monat, 1 Tag", dv.DifferenceText);
                w.SwitchTo(AppMode.Length);
                conv = (ConverterView)w.View;
                conv.Select(4, 7); conv.SetInput("1");
                Check("German converter shows comma and German unit names", conv.OutputText == "3,280839895" && conv.Cat.Units[7].ToString() == "Fu\u00DF", conv.OutputText + " / " + conv.Cat.Units[7]);
                w.SwitchTo(AppMode.Settings);
                sv = (SettingsView)w.View;
                Check("settings page in German with five language buttons", w.TitleText == "Einstellungen" && sv.LanguageButtonCount == 5, w.TitleText + " / " + sv.LanguageButtonCount);
                w.SetLanguage("fr");
                Check("French window title for settings", w.TitleText == "Param\u00E8tres", w.TitleText);
                w.SetLanguage("System");
                Check("automatic language resolves to a supported language", Loc.IsSupported(Loc.Lang) && Settings.Get("Language", "") == "System", Loc.Lang);
                w.SetLanguage("en");
                Check("back to English", Loc.Lang == "en" && w.TitleText == "Settings" && w.Ses.History.Count == langBefore + 1, w.TitleText + " history " + w.Ses.History.Count);
            }
            catch (Exception ex) { Check("ui tests", false, ex.ToString()); }
            finally
            {
                try { File.Delete(Settings.Path); } catch (Exception) { }
                Settings.Path = oldS;
            }
        }
    }
}
