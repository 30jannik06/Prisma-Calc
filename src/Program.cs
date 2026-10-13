// Prisma - entry point and command line. C# 5.
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;

namespace Prisma
{
    public static class Program
    {
        [DllImport("kernel32.dll")] static extern bool AttachConsole(int pid);
        [DllImport("kernel32.dll")] static extern bool SetDefaultDllDirectories(uint directoryFlags);

        const string Usage =
            "Prisma [--theme Default|Light|Dark] [--mode <name>] [--language System|en|de|fr|es] [--settings <file>] [--log <file>]\r\n" +
            "Prisma --print-theme [--theme <mode>]\r\n" +
            "Prisma --help\r\n" +
            "Modes: Standard Scientific Programmer Graphing Date Currency Volume Length Weight Temperature\r\n" +
            "       Energy Area Speed Time Power Data Pressure Angle\r\n" +
            "Exit codes: 0 ok, 1 failure, 2 usage error.\r\n";

        public class Options
        {
            public ThemeMode? Theme; public AppMode? Mode; public string SettingsPath; public string LogPath;
            public bool PrintTheme, Help; public string Error, Language;
        }

        public static Options Parse(string[] args)
        {
            var o = new Options();
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];
                if (a == "--help" || a == "-h" || a == "/?") o.Help = true;
                else if (a == "--print-theme") o.PrintTheme = true;
                else if (a == "--theme" || a == "--settings" || a == "--log" || a == "--mode" || a == "--language")
                {
                    if (i + 1 >= args.Length) { o.Error = "Missing value after " + a; return o; }
                    string v = args[++i];
                    if (a == "--settings" || a == "--log")
                    {
                        string full, why;
                        if (!SafePath.TryNormalize(v, out full, out why)) { o.Error = "Rejected " + a + " path: " + why + "."; return o; }
                        if (a == "--settings") o.SettingsPath = full; else o.LogPath = full;
                    }
                    else if (a == "--language")
                    {
                        if (!string.Equals(v, "System", StringComparison.OrdinalIgnoreCase) && !Loc.IsSupported(v)) { o.Error = "Unknown language '" + v + "'. Use System, en, de, fr or es."; return o; }
                        o.Language = v;
                    }
                    else if (a == "--mode")
                    {
                        AppMode am;
                        if (!Settings.TryParseApp(v, out am)) { o.Error = "Unknown mode '" + v + "'."; return o; }
                        o.Mode = am;
                    }
                    else
                    {
                        ThemeMode m;
                        if (!Settings.TryParseMode(v, out m)) { o.Error = "Unknown theme '" + v + "'. Use Default, Light or Dark."; return o; }
                        o.Theme = m;
                    }
                }
                else { o.Error = "Unknown option " + a; return o; }
            }
            return o;
        }

        [STAThread]
        public static int Main(string[] args)
        {
            // Only load DLLs from the Windows system folder (no search of the current folder or PATH).
            try { SetDefaultDllDirectories(0x00000800); } catch (Exception) { }
            AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e) { Log.Error("Fatal: " + e.ExceptionObject); };
            AttachConsole(-1);
            Options o = Parse(args);
            if (o.LogPath != null) Log.Path = o.LogPath;
            if (o.SettingsPath != null) Settings.Path = o.SettingsPath;
            if (o.Error != null) { Console.Error.Write(o.Error + "\r\n" + Usage); Log.Warn("Usage error: " + o.Error); return 2; }
            if (o.Help) { Console.Out.Write(Usage); return 0; }
            try
            {
                if (o.Theme.HasValue) Settings.Save(o.Theme.Value);
                if (o.Language != null) Settings.Set("Language", o.Language);
                Loc.Apply(Settings.Get("Language", "System"));
                ThemeMode mode = o.Theme.HasValue ? o.Theme.Value : Settings.Load();
                if (o.PrintTheme)
                {
                    bool light = ThemeManager.OsUsesLightApps(), hc = ThemeManager.HighContrastOn();
                    Console.Out.Write("mode=" + mode + " osLight=" + light + " highContrast=" + hc + " effective=" + ThemeManager.Resolve(mode, light, hc) + "\r\n");
                    return 0;
                }
                AppMode start = o.Mode.HasValue ? o.Mode.Value : Settings.LoadApp();
                Log.Info("Start, theme=" + mode + ", mode=" + start + ", log=" + (Log.Path ?? Log.DefaultPath()));
                var app = new Application();
                app.DispatcherUnhandledException += delegate(object sender, DispatcherUnhandledExceptionEventArgs e) { Log.Error("UI error: " + e.Exception); e.Handled = true; };
                var win = new MainWindow(mode, start);
                UserPreferenceChangedEventHandler onPrefs = delegate(object s, UserPreferenceChangedEventArgs e)
                {
                    if (e.Category == UserPreferenceCategory.General || e.Category == UserPreferenceCategory.Color || e.Category == UserPreferenceCategory.VisualStyle)
                    {
                        Log.Info("OS preference changed: " + e.Category);
                        win.Dispatcher.BeginInvoke(new Action(win.ApplyTheme));
                    }
                };
                SystemEvents.UserPreferenceChanged += onPrefs;
                int rc;
                try { rc = app.Run(win); }
                finally { SystemEvents.UserPreferenceChanged -= onPrefs; }
                Log.Info("Exit " + rc);
                return rc;
            }
            catch (Exception ex)
            {
                Log.Error("Unhandled: " + ex);
                Console.Error.Write("Failed: " + ex.Message + "\r\n");
                return 1;
            }
        }
    }
}
