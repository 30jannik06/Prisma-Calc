// Prisma - main window: liquid backdrop, navigation, history/memory panel, theming engine. C# 5.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Prisma
{
    public class MainWindow : Window
    {
        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        const double NavWidth = 220, SideWidth = 250;

        public readonly Session Ses = new Session();
        public ThemeMode Mode;
        public EffectiveTheme Current;
        public AppMode Active;
        public string StyleName;
        public string AccentChoice;   // "Theme", "System" or "#RRGGBB"
        public bool Glass, Animate;
        public event Action PrefsChanged;

        readonly Dictionary<AppMode, IModeView> views = new Dictionary<AppMode, IModeView>();
        readonly Dictionary<AppMode, Button> navButtons = new Dictionary<AppMode, Button>();
        ContentControl content;
        TextBlock title;
        Border nav, side;
        StackPanel sideList;
        Button histToggle, navToggle, tabHistory, tabMemory;
        Grid rootGrid, shell;
        public string LanguageChoice;
        public string Corners = "Soft";
        // Window size chosen by the user (remembered across modes and sessions). Until the user resizes the window,
        // each mode opens at its own preferred size.
        bool userSized;
        double userW, userH, lastW, lastH;
        public bool UserSized { get { return userSized; } }
        public string TitleText { get { return title.Text; } }
        readonly Grid blobHost = new Grid { IsHitTestVisible = false, ClipToBounds = true };
        readonly List<KeyValuePair<TranslateTransform, double[]>> blobs = new List<KeyValuePair<TranslateTransform, double[]>>();
        IModeView view;
        bool sideOpen, navOpen = true, memoryTab, blobsRunning;
        DispatcherTimer timer;
        readonly Dictionary<string, Color> shown = new Dictionary<string, Color>();

        public IModeView View { get { return view; } }
        public bool BlobsVisible { get { return blobHost.Visibility == Visibility.Visible; } }
        public bool BlobsAnimating { get { return blobsRunning; } }
        public Color Shown(string key) { return shown[key]; }

        // The window / taskbar icon, embedded in the program as the resource "Prisma.ico" (all sizes in one file).
        public static ImageSource AppIcon()
        {
            try
            {
                using (Stream st = Assembly.GetExecutingAssembly().GetManifestResourceStream("Prisma.ico"))
                {
                    if (st == null) return null;
                    return BitmapFrame.Create(st, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                }
            }
            catch (Exception ex) { Log.Warn("Could not load the icon: " + ex.Message); return null; }
        }

        public static string CaptionKey(AppMode m)
        {
            switch (m)
            {
                case AppMode.Date: return "Date calculation";
                case AppMode.Weight: return "Weight and mass";
                case AppMode.Subnet: return "Subnet calculator";
                case AppMode.IpConvert: return "Address converter";
                case AppMode.Transfer: return "Transfer time";
                default: return m.ToString();
            }
        }

        public static string Caption(AppMode m) { return Loc.T(CaptionKey(m)); }

        public MainWindow(ThemeMode mode, AppMode start)
        {
            Title = "Prisma";
            Icon = AppIcon();
            MinWidth = 340; MinHeight = 420;
            FontFamily = new FontFamily("Segoe UI, Segoe UI Symbol");
            Mode = mode;
            StyleName = ThemeManager.NormalizeStyle(Settings.Get("Style", ThemeManager.DefaultStyle));
            AccentChoice = Settings.Get("Accent", "Theme");
            Glass = Settings.Get("Glass", "1") != "0";
            Animate = Settings.Get("Animate", "1") != "0";
            LanguageChoice = Settings.Get("Language", "System");
            Corners = Settings.Get("Corners", "Soft");
            Ui.CornerScale = Ui.ScaleFor(Corners);
            int savedW, savedH;
            if (int.TryParse(Settings.Get("WindowWidth", ""), NumberStyles.None, CultureInfo.InvariantCulture, out savedW)
                && int.TryParse(Settings.Get("WindowHeight", ""), NumberStyles.None, CultureInfo.InvariantCulture, out savedH))
            { userSized = true; userW = savedW; userH = savedH; }
            Ui.InstallStyles(Resources);
            SetResourceReference(BackgroundProperty, "WinBg");

            rootGrid = new Grid();
            var backdrop = new Rectangle { IsHitTestVisible = false };
            backdrop.SetResourceReference(Shape.FillProperty, "Backdrop");
            rootGrid.Children.Add(backdrop);
            BuildBlobs();
            rootGrid.Children.Add(blobHost);

            BuildShell();
            Content = rootGrid;

            Ses.Changed += BuildSide;
            ApplyTheme(false);
            SwitchTo(start);
            SourceInitialized += delegate { UpdateTitleBar(); HookSizing(); };
            Loaded += delegate { UpdateEffects(); KeepOnScreen(); };
            PreviewTextInput += OnText;
            PreviewKeyDown += OnKey;
        }

        // Builds navigation, header, content host and side panel. Called again when the language changes.
        void BuildShell()
        {
            content = new ContentControl(); nav = new Border(); side = new Border(); sideList = new StackPanel();
            navButtons.Clear();
            Grid oldShell = shell;
            shell = new Grid();
            shell.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            shell.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            shell.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            BuildNav();
            Grid.SetColumn(nav, 0); shell.Children.Add(nav);

            var main = new Grid { Margin = new Thickness(12, 10, 12, 12) };
            main.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            main.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            var head = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            navToggle = Ui.Key("☰", "func", ToggleNav, 18); navToggle.Width = 42; navToggle.Height = 38;
            title = Ui.Label("", "Text", 22); title.FontWeight = FontWeights.SemiBold; title.VerticalAlignment = VerticalAlignment.Center; title.Margin = new Thickness(8, 0, 8, 0);
            Grid.SetColumn(title, 1);
            histToggle = Ui.Key(Loc.T("History"), "func", ToggleSide, 13); histToggle.Height = 34; histToggle.Width = 84;
            Grid.SetColumn(histToggle, 2);
            head.Children.Add(navToggle); head.Children.Add(title); head.Children.Add(histToggle);
            Grid.SetRow(head, 0); main.Children.Add(head);
            Grid.SetRow(content, 1); main.Children.Add(content);
            Grid.SetColumn(main, 1); shell.Children.Add(main);

            side.Width = SideWidth; side.Visibility = Visibility.Collapsed; side.BorderThickness = new Thickness(1, 0, 0, 0);
            side.SetResourceReference(Border.BackgroundProperty, "Panel");
            side.SetResourceReference(Border.BorderBrushProperty, "Border");
            var sideRoot = new Grid { Margin = new Thickness(8) };
            sideRoot.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            sideRoot.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            sideRoot.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var tabs = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 6) };
            tabHistory = TabButton(Loc.T("History"), false); tabMemory = TabButton(Loc.T("Memory"), true);
            tabs.Children.Add(tabHistory); tabs.Children.Add(tabMemory);
            Grid.SetRow(tabs, 0); sideRoot.Children.Add(tabs);
            var sv = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = sideList };
            Grid.SetRow(sv, 1); sideRoot.Children.Add(sv);
            var clear = Ui.Key(Loc.T("Clear"), "func", ClearSide, 13); clear.Height = 34;
            Grid.SetRow(clear, 2); sideRoot.Children.Add(clear);
            side.Child = sideRoot;
            Grid.SetColumn(side, 2); shell.Children.Add(side);
            if (oldShell != null) rootGrid.Children.Remove(oldShell);
            rootGrid.Children.Add(shell);
            nav.Visibility = navOpen ? Visibility.Visible : Visibility.Collapsed;
        }

        // ------------------------------------------------------ liquid backdrop
        void BuildBlobs()
        {
            //       key     size  horizontal                    vertical                    margin                              dx    dy   secX secY
            AddBlob("Blob1", 640, HorizontalAlignment.Left, VerticalAlignment.Top, new Thickness(-180, -240, 0, 0), 120, 90, 23, 29);
            AddBlob("Blob2", 720, HorizontalAlignment.Right, VerticalAlignment.Bottom, new Thickness(0, 0, -240, -280), -130, -100, 31, 25);
            AddBlob("Blob3", 560, HorizontalAlignment.Center, VerticalAlignment.Center, new Thickness(120, 40, 0, 0), -150, 110, 37, 33);
        }

        void AddBlob(string key, double size, HorizontalAlignment ha, VerticalAlignment va, Thickness margin, double dx, double dy, double secX, double secY)
        {
            var e = new Ellipse { Width = size, Height = size, HorizontalAlignment = ha, VerticalAlignment = va, Margin = margin };
            e.SetResourceReference(Shape.FillProperty, key);
            var tt = new TranslateTransform();
            e.RenderTransform = tt;
            blobHost.Children.Add(e);
            blobs.Add(new KeyValuePair<TranslateTransform, double[]>(tt, new[] { dx, dy, secX, secY }));
        }

        void UpdateEffects()
        {
            bool glassStyle = ThemeManager.IsGlassStyle(StyleName) && Current != EffectiveTheme.HighContrast;
            blobHost.Visibility = glassStyle ? Visibility.Visible : Visibility.Collapsed;
            bool run = glassStyle && Glass && Animate;
            if (run == blobsRunning) return;
            blobsRunning = run;
            foreach (KeyValuePair<TranslateTransform, double[]> b in blobs)
            {
                if (run)
                {
                    b.Key.BeginAnimation(TranslateTransform.XProperty, Drift(b.Value[0], b.Value[2]));
                    b.Key.BeginAnimation(TranslateTransform.YProperty, Drift(b.Value[1], b.Value[3]));
                }
                else
                {
                    b.Key.BeginAnimation(TranslateTransform.XProperty, null);
                    b.Key.BeginAnimation(TranslateTransform.YProperty, null);
                }
            }
        }

        static DoubleAnimation Drift(double to, double seconds)
        {
            return new DoubleAnimation(0, to, new Duration(TimeSpan.FromSeconds(seconds)))
            {
                AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };
        }

        // ---------------------------------------------------------- navigation
        void BuildNav()
        {
            nav.Width = NavWidth; nav.BorderThickness = new Thickness(0, 0, 1, 0);
            nav.SetResourceReference(Border.BackgroundProperty, "Panel");
            nav.SetResourceReference(Border.BorderBrushProperty, "Border");
            var g = new Grid();
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            TextBlock brand = Ui.Label("PRISMA", "Brand", 19); brand.FontWeight = FontWeights.Bold; brand.VerticalAlignment = VerticalAlignment.Center; brand.Margin = new Thickness(10, 0, 0, 0);
            var brandRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(18, 16, 0, 8) };
            brandRow.Children.Add(Ui.Logo(32)); brandRow.Children.Add(brand);
            g.Children.Add(brandRow);
            var list = new StackPanel();
            list.Children.Add(Section(Loc.T("CALCULATOR")));
            foreach (AppMode m in new[] { AppMode.Standard, AppMode.Scientific, AppMode.Programmer, AppMode.Graphing, AppMode.Date }) list.Children.Add(NavItem(m));
            list.Children.Add(Section(Loc.T("NETWORK")));
            foreach (AppMode m in new[] { AppMode.Subnet, AppMode.IpConvert, AppMode.Transfer }) list.Children.Add(NavItem(m));
            list.Children.Add(Section(Loc.T("CONVERTER")));
            foreach (AppMode m in new[] { AppMode.Currency, AppMode.Volume, AppMode.Length, AppMode.Weight, AppMode.Temperature, AppMode.Energy, AppMode.Area, AppMode.Speed, AppMode.Time, AppMode.Power, AppMode.Data, AppMode.Pressure, AppMode.Angle })
                list.Children.Add(NavItem(m));
            var sv = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = list };
            Grid.SetRow(sv, 1); g.Children.Add(sv);
            Button settings = NavItem(AppMode.Settings);
            settings.Content = "⚙   " + Loc.T("Settings"); settings.Margin = new Thickness(0, 4, 0, 10);
            Grid.SetRow(settings, 2); g.Children.Add(settings);
            nav.Child = g;
        }

        TextBlock Section(string text)
        {
            TextBlock t = Ui.Label(text, "SubText", 11); t.FontWeight = FontWeights.SemiBold; t.Margin = new Thickness(20, 12, 0, 4);
            return t;
        }

        Button NavItem(AppMode m)
        {
            AppMode mm = m;
            var b = new Button { Content = Caption(m), FontSize = 14, Style = (Style)Resources["Nav"] };
            b.Click += delegate { SwitchTo(mm); };
            navButtons[m] = b;
            return b;
        }

        Button TabButton(string caption, bool memory)
        {
            bool mem = memory;
            var b = new Button { Content = caption, FontSize = 12, Width = 90, Height = 28, Style = Ui.Seg };
            b.Click += delegate { memoryTab = mem; BuildSide(); };
            return b;
        }

        void ToggleNav()
        {
            navOpen = !navOpen;
            nav.Visibility = navOpen ? Visibility.Visible : Visibility.Collapsed;
            if (userSized) userW = Math.Max(300, userW + (navOpen ? NavWidth : -NavWidth));
            FitWindow();
        }

        void ToggleSide()
        {
            sideOpen = !sideOpen;
            side.Visibility = sideOpen && view != null && view.HasHistory ? Visibility.Visible : Visibility.Collapsed;
            if (userSized && view != null && view.HasHistory) userW = Math.Max(300, userW + (sideOpen ? SideWidth : -SideWidth));
            BuildSide();
            FitWindow();
        }

        void ClearSide()
        {
            if (memoryTab) Ses.Memory.Clear(); else Ses.History.Clear();
            Ses.Raise();
        }

        void BuildSide()
        {
            sideList.Children.Clear();
            Mark(tabHistory, !memoryTab); Mark(tabMemory, memoryTab);
            if (!memoryTab)
            {
                if (Ses.History.Count == 0) sideList.Children.Add(Empty(Loc.T("There is no history yet.")));
                foreach (HistoryItem h in Ses.History)
                {
                    HistoryItem item = h;
                    var sp = new StackPanel();
                    TextBlock a = Ui.Label(Loc.Num(h.Expr) + " =", "SubText", 12); a.HorizontalAlignment = HorizontalAlignment.Right; a.TextTrimming = TextTrimming.CharacterEllipsis;
                    TextBlock r = Ui.Label(Loc.Num(h.Result), "Text", 22); r.HorizontalAlignment = HorizontalAlignment.Right; r.TextTrimming = TextTrimming.CharacterEllipsis;
                    sp.Children.Add(a); sp.Children.Add(r);
                    var b = Ui.Key("", "flat", delegate { if (view != null) view.Recall(item.Result); }, 14);
                    b.Content = sp; b.HorizontalContentAlignment = HorizontalAlignment.Stretch; b.Padding = new Thickness(6, 2, 6, 2);
                    sideList.Children.Add(b);
                }
            }
            else
            {
                if (Ses.Memory.Count == 0) sideList.Children.Add(Empty(Loc.T("Nothing is saved in memory.")));
                foreach (double v in Ses.Memory)
                {
                    string text = Expr.Format(v);
                    TextBlock r = Ui.Label(Loc.Num(text), "Text", 22); r.HorizontalAlignment = HorizontalAlignment.Right; r.TextTrimming = TextTrimming.CharacterEllipsis;
                    var b = Ui.Key("", "flat", delegate { if (view != null) view.Recall(text); }, 14);
                    b.Content = r; b.HorizontalContentAlignment = HorizontalAlignment.Stretch;
                    sideList.Children.Add(b);
                }
            }
        }

        TextBlock Empty(string text)
        {
            TextBlock t = Ui.Label(text, "SubText", 13); t.Margin = new Thickness(8, 10, 8, 0); t.TextWrapping = TextWrapping.Wrap;
            return t;
        }

        // Selected buttons get the accent fill, others stay transparent.
        public static void Mark(Button b, bool selected)
        {
            if (selected) b.SetResourceReference(Control.BackgroundProperty, "AccentFill"); else b.Background = Brushes.Transparent;
            b.SetResourceReference(Control.ForegroundProperty, selected ? "AccentText" : "Text");
        }

        // Same for buttons that rest on the function-key fill.
        public static void Mark2(Button b, bool selected)
        {
            if (selected) { b.SetResourceReference(Control.BackgroundProperty, "AccentFill"); b.SetResourceReference(Control.ForegroundProperty, "AccentText"); }
            else { b.SetResourceReference(Control.BackgroundProperty, "FuncBg"); b.SetResourceReference(Control.ForegroundProperty, "FuncText"); }
        }

        IModeView Create(AppMode m)
        {
            switch (m)
            {
                case AppMode.Standard: return new StandardView(Ses);
                case AppMode.Scientific: return new ScientificView(Ses);
                case AppMode.Programmer: return new ProgrammerView();
                case AppMode.Graphing: return new GraphView(Ses);
                case AppMode.Date: return new DateView();
                case AppMode.Subnet: return new SubnetView();
                case AppMode.IpConvert: return new AddressView();
                case AppMode.Transfer: return new TransferView();
                case AppMode.Settings: return new SettingsView(this);
                default: return new ConverterView(m);
            }
        }

        public void SwitchTo(AppMode m)
        {
            IModeView v;
            if (!views.TryGetValue(m, out v)) { v = Create(m); views[m] = v; }
            Active = m; view = v;
            content.Content = v.Root;
            title.Text = Caption(m);
            foreach (KeyValuePair<AppMode, Button> kv in navButtons) Mark(kv.Value, kv.Key == m);
            histToggle.Visibility = v.HasHistory ? Visibility.Visible : Visibility.Collapsed;
            side.Visibility = sideOpen && v.HasHistory ? Visibility.Visible : Visibility.Collapsed;
            BuildSide();
            FitWindow();
            Settings.Set("Mode", m.ToString());
            Log.Info("Mode: " + m);
        }

        // Size the window should have: the user's size if they chose one (but never below what the mode needs),
        // otherwise the mode's preferred size. Always inside the work area.
        public static Size TargetSize(bool userSized, Size user, Size preferred, Size min, Size work)
        {
            double w = userSized ? Math.Max(user.Width, min.Width) : preferred.Width;
            double h = userSized ? Math.Max(user.Height, min.Height) : preferred.Height;
            return new Size(Math.Min(w, work.Width), Math.Min(h, work.Height));
        }

        void FitWindow()
        {
            if (view == null || WindowState != WindowState.Normal) return;
            Rect wa = SystemParameters.WorkArea;
            bool withSide = sideOpen && view.HasHistory;
            double extra = (navOpen ? NavWidth : 0) + (withSide ? SideWidth : 0) + 28;
            var preferred = new Size(extra + view.PreferredWidth, Math.Max(view.PreferredHeight + 70, 720));
            var min = new Size(Math.Min(extra + view.PreferredWidth * 0.8, wa.Width), Math.Min(Math.Max(560, (view.PreferredHeight + 70) * 0.8), wa.Height));
            Size t = TargetSize(userSized, new Size(userW, userH), preferred, min, new Size(wa.Width, wa.Height));
            MinWidth = min.Width; MinHeight = min.Height;
            Width = t.Width; Height = t.Height;
            lastW = t.Width; lastH = t.Height;
            KeepOnScreen();
        }

        // Called when the user finished resizing the window.
        public void RememberUserSize(double w, double h)
        {
            userSized = true;
            userW = Math.Max(300, Math.Min(8000, Math.Round(w)));
            userH = Math.Max(300, Math.Min(8000, Math.Round(h)));
            Settings.Set("WindowWidth", ((int)userW).ToString(CultureInfo.InvariantCulture));
            Settings.Set("WindowHeight", ((int)userH).ToString(CultureInfo.InvariantCulture));
        }

        void HookSizing()
        {
            try
            {
                HwndSource src = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
                if (src != null) src.AddHook(WndProc);
            }
            catch (Exception ex) { Log.Warn("Could not watch window size: " + ex.Message); }
        }

        // WM_EXITSIZEMOVE: the user let go after moving or resizing. Only a real size change counts as a choice.
        IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == 0x0232 && WindowState == WindowState.Normal && (Math.Abs(ActualWidth - lastW) > 2 || Math.Abs(ActualHeight - lastH) > 2))
                RememberUserSize(ActualWidth, ActualHeight);
            return IntPtr.Zero;
        }

        // Moves the window back into the visible work area (above the taskbar) after it grew. Only for windows
        // that are on the primary screen; windows on other monitors are left alone.
        public static Rect ClampRect(Rect window, Rect workArea)
        {
            double cx = window.Left + window.Width / 2, cy = window.Top + window.Height / 2;
            if (cx < workArea.Left || cx > workArea.Right || cy < workArea.Top || cy > workArea.Bottom) return window;
            double w = Math.Min(window.Width, workArea.Width), h = Math.Min(window.Height, workArea.Height);
            double l = Math.Max(workArea.Left, Math.Min(window.Left, workArea.Right - w));
            double t = Math.Max(workArea.Top, Math.Min(window.Top, workArea.Bottom - h));
            return new Rect(l, t, w, h);
        }

        void KeepOnScreen()
        {
            if (WindowState != WindowState.Normal || double.IsNaN(Left) || double.IsNaN(Top) || !IsLoaded) return;
            Rect r = ClampRect(new Rect(Left, Top, Width, Height), SystemParameters.WorkArea);
            Left = r.Left; Top = r.Top; Width = r.Width; Height = r.Height;
        }

        // -------------------------------------------------------------- input
        void OnText(object s, TextCompositionEventArgs e)
        {
            if (view == null || Keyboard.FocusedElement is TextBox) return;
            if (view.HandleText(e.Text)) e.Handled = true;
        }

        void OnKey(object s, KeyEventArgs e)
        {
            if (view == null) return;
            bool typing = Keyboard.FocusedElement is TextBox;
            bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
            if (ctrl && !typing && e.Key == Key.C)
            {
                try { string t = view.CopyText(); if (!string.IsNullOrEmpty(t)) Clipboard.SetText(t); } catch (Exception ex) { Log.Warn("Copy failed: " + ex.Message); }
                e.Handled = true;
            }
            else if (ctrl && !typing && e.Key == Key.V)
            {
                try { if (Clipboard.ContainsText()) view.Paste(Clipboard.GetText()); } catch (Exception ex) { Log.Warn("Paste failed: " + ex.Message); }
                e.Handled = true;
            }
            else if (!typing && view.HandleKey(e.Key)) e.Handled = true;
        }

        // ------------------------------------------------------ look settings
        void Changed()
        {
            ApplyTheme();
            if (PrefsChanged != null) PrefsChanged();
        }

        public void SetLanguage(string choice)
        {
            LanguageChoice = choice; Settings.Set("Language", choice);
            Loc.Apply(choice);
            Log.Info("Language set to " + choice + " (" + Loc.Lang + ")");
            Rebuild();
        }

        // Rebuilds the whole window content (needed when text or corner radii change) and keeps the current mode.
        void Rebuild()
        {
            AppMode keep = Active;
            views.Clear();
            BuildShell();
            SwitchTo(keep);
            Ses.Raise();
            if (PrefsChanged != null) PrefsChanged();
        }

        public void SetCorners(string name)
        {
            if (name != "Square" && name != "Soft" && name != "Round") return;
            Corners = name; Ui.CornerScale = Ui.ScaleFor(name);
            Settings.Set("Corners", name);
            Log.Info("Corners set to " + name);
            Rebuild();
        }

        public void SetMode(ThemeMode m) { Mode = m; Settings.Save(m); Log.Info("Appearance set to " + m); Changed(); }
        public void SetStyle(string name) { StyleName = ThemeManager.NormalizeStyle(name); Settings.Set("Style", StyleName); Log.Info("Style set to " + StyleName); Changed(); }
        public void SetAccent(string choice) { AccentChoice = choice; Settings.Set("Accent", choice); Log.Info("Accent set to " + choice); Changed(); }
        public void SetGlass(bool on) { Glass = on; Settings.Set("Glass", on ? "1" : "0"); Log.Info("Glass " + on); Changed(); }
        public void SetAnimate(bool on) { Animate = on; Settings.Set("Animate", on ? "1" : "0"); Log.Info("Animate " + on); Changed(); }

        public void ResetLook()
        {
            Mode = ThemeMode.Default; StyleName = ThemeManager.DefaultStyle; AccentChoice = "Theme"; Glass = true; Animate = true;
            Settings.Save(Mode); Settings.Set("Style", StyleName); Settings.Set("Accent", AccentChoice); Settings.Set("Glass", "1"); Settings.Set("Animate", "1");
            bool cornersChanged = Corners != "Soft";
            Corners = "Soft"; Ui.CornerScale = 1.0; Settings.Set("Corners", "Soft");
            Log.Info("Look reset to defaults");
            if (cornersChanged) Rebuild();
            Changed();
        }

        Color? AccentOverride()
        {
            if (AccentChoice == "System") return ThemeManager.SystemAccent();
            Color c;
            if (ThemeManager.TryParseHex(AccentChoice, out c)) return c;
            return null;
        }

        public bool Animating { get { return timer != null && timer.IsEnabled; } }

        public void ApplyTheme() { ApplyTheme(IsLoaded); }

        static Color Lerp(Color a, Color b, double k)
        {
            return Color.FromArgb((byte)Math.Round(a.A + (b.A - a.A) * k), (byte)Math.Round(a.R + (b.R - a.R) * k),
                (byte)Math.Round(a.G + (b.G - a.G) * k), (byte)Math.Round(a.B + (b.B - a.B) * k));
        }

        static Brush Solid(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }

        static Brush Vertical(Color top, Color bottom)
        {
            var b = new LinearGradientBrush(top, bottom, new Point(0.5, 0), new Point(0.5, 1)); b.Freeze(); return b;
        }

        void SetBrushes(Dictionary<string, Color> p, bool final)
        {
            foreach (KeyValuePair<string, Color> kv in p)
            {
                Brush br;
                Color c = kv.Value;
                if (kv.Key == "Sheen") br = Vertical(c, Color.FromArgb(0, c.R, c.G, c.B));
                else if (kv.Key.StartsWith("Blob"))
                {
                    var rb = new RadialGradientBrush(c, Color.FromArgb(0, c.R, c.G, c.B)); rb.Freeze(); br = rb;
                }
                else br = Solid(c);
                Resources[kv.Key] = br;
                shown[kv.Key] = c;
            }
            Resources["AccentFill"] = Vertical(shown["AccentBg"], shown["AccentBg2"]);
            var bd = new LinearGradientBrush(shown["BgA"], shown["BgB"], new Point(0, 0), new Point(1, 1)); bd.Freeze();
            Resources["Backdrop"] = bd;
            if (final) UpdateTitleBar();
            Ses.RaiseTheme();
        }

        public void ApplyTheme(bool animate)
        {
            try
            {
                EffectiveTheme t = ThemeManager.Resolve(Mode, ThemeManager.OsUsesLightApps(), ThemeManager.HighContrastOn());
                Dictionary<string, Color> target = ThemeManager.Build(StyleName, t, AccentOverride(), Glass);
                bool fade = animate && shown.Count > 0;
                if (timer != null) timer.Stop();
                var from = new Dictionary<string, Color>(shown);
                Current = t; Ses.Theme = t;
                if (fade)
                {
                    DateTime begin = DateTime.UtcNow;
                    var tm = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
                    timer = tm;
                    tm.Tick += delegate
                    {
                        double k = (DateTime.UtcNow - begin).TotalMilliseconds / 320.0;
                        if (k >= 1) { tm.Stop(); SetBrushes(target, true); return; }
                        k = 1 - Math.Pow(1 - k, 3);
                        var mid = new Dictionary<string, Color>();
                        foreach (KeyValuePair<string, Color> kv in target)
                            mid[kv.Key] = Lerp(from.ContainsKey(kv.Key) ? from[kv.Key] : kv.Value, kv.Value, k);
                        SetBrushes(mid, false);
                    };
                    tm.Start();
                }
                else SetBrushes(target, true);
                UpdateEffects();
                Log.Info("Applied theme: appearance=" + Mode + " style=" + StyleName + " effective=" + t + " glass=" + Glass + " animate=" + Animate + (fade ? " (fade)" : ""));
            }
            catch (Exception ex) { Log.Error("ApplyTheme failed: " + ex); }
        }

        static int ColorRef(Color c) { return c.R | (c.G << 8) | (c.B << 16); }

        void UpdateTitleBar()
        {
            try
            {
                IntPtr h = new WindowInteropHelper(this).Handle;
                if (h == IntPtr.Zero) return;
                int dark = Current == EffectiveTheme.Dark ? 1 : 0;
                if (DwmSetWindowAttribute(h, 20, ref dark, 4) != 0) DwmSetWindowAttribute(h, 19, ref dark, 4);
                bool flat = Current == EffectiveTheme.HighContrast || !shown.ContainsKey("BgA");
                int cap = flat ? -1 : ColorRef(shown["BgA"]);
                int txt = flat ? -1 : ColorRef(shown["Text"]);
                DwmSetWindowAttribute(h, 35, ref cap, 4);   // caption colour (Windows 11)
                DwmSetWindowAttribute(h, 36, ref txt, 4);   // caption text colour
                DwmSetWindowAttribute(h, 34, ref cap, 4);   // window border colour
            }
            catch (Exception ex) { Log.Warn("Title bar update failed: " + ex.Message); }
        }
    }
}
