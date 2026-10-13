// Prisma - settings page: appearance, theme style, accent colour, effects, language. C# 5.
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Prisma
{
    public class SettingsView : IModeView
    {
        static readonly string[] AccentPresets = { "0A84FF", "30D158", "FF9F0A", "FF375F", "BF5AF2", "64D2FF", "FFD60A", "FF453A" };

        readonly MainWindow win;
        readonly ScrollViewer root = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        readonly Dictionary<ThemeMode, Button> modeButtons = new Dictionary<ThemeMode, Button>();
        readonly Dictionary<string, Border> cardRings = new Dictionary<string, Border>();
        readonly Dictionary<string, Border> cardPreviews = new Dictionary<string, Border>();
        readonly Dictionary<string, Border> accentRings = new Dictionary<string, Border>();
        readonly Dictionary<string, Button> accentTextButtons = new Dictionary<string, Button>();
        readonly Dictionary<string, Button> languageButtons = new Dictionary<string, Button>();
        readonly Dictionary<string, Button> cornerButtons = new Dictionary<string, Button>();
        Button glassOn, glassOff, animOn, animOff;
        TextBlock animHint;

        public UIElement Root { get { return root; } }
        public double PreferredWidth { get { return 640; } }
        public double PreferredHeight { get { return 700; } }
        public bool HasHistory { get { return false; } }
        public int StyleCardCount { get { return cardRings.Count; } }
        public int AccentSwatchCount { get { return accentRings.Count; } }
        public int LanguageButtonCount { get { return languageButtons.Count; } }
        public int CornerButtonCount { get { return cornerButtons.Count; } }

        public SettingsView(MainWindow w)
        {
            win = w;
            var page = new StackPanel { Margin = new Thickness(2, 0, 14, 16) };
            root.Content = page;

            page.Children.Add(Head(Loc.T("Language")));
            page.Children.Add(Hint(Loc.T("Changes the language of the app and the number format (decimal comma).")));
            var langs = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) };
            langs.Children.Add(LanguageButton("System", Loc.T("Automatic (Windows)")));
            for (int i = 0; i < Loc.Codes.Length; i++) langs.Children.Add(LanguageButton(Loc.Codes[i], Loc.Native[i]));
            page.Children.Add(langs);

            page.Children.Add(Head(Loc.T("Appearance")));
            page.Children.Add(Hint(Loc.T("System follows the Windows light/dark setting and changes with it.")));
            var pill = Pill();
            var seg = new StackPanel { Orientation = Orientation.Horizontal };
            AddMode(seg, ThemeMode.Default, Loc.T("System")); AddMode(seg, ThemeMode.Light, Loc.T("Light")); AddMode(seg, ThemeMode.Dark, Loc.T("Dark"));
            pill.Child = seg;
            page.Children.Add(pill);

            page.Children.Add(Head(Loc.T("Theme style")));
            var wrap = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) };
            foreach (string s in ThemeManager.Styles) wrap.Children.Add(StyleCard(s));
            page.Children.Add(wrap);

            page.Children.Add(Head(Loc.T("Accent colour")));
            page.Children.Add(Hint(Loc.T("Colours the equals key, selections and highlights.")));
            var acc = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) };
            acc.Children.Add(AccentText("Theme", Loc.T("Theme default")));
            acc.Children.Add(AccentText("System", Loc.T("Windows accent")));
            foreach (string hex in AccentPresets) acc.Children.Add(AccentSwatch("#" + hex));
            page.Children.Add(acc);

            page.Children.Add(Head(Loc.T("Corners")));
            page.Children.Add(Hint(Loc.T("Roundness of keys, panels and fields.")));
            var cpill = Pill();
            var cseg = new StackPanel { Orientation = Orientation.Horizontal };
            foreach (string cn in new[] { "Square", "Soft", "Round" })
            {
                string name = cn;
                var cb = new Button { Content = Loc.T(cn), FontSize = 13, MinWidth = 92, Height = 34, Padding = new Thickness(10, 0, 10, 0), Style = Ui.Seg };
                cb.Click += delegate { win.SetCorners(name); };
                cornerButtons[cn] = cb;
                cseg.Children.Add(cb);
            }
            cpill.Child = cseg;
            page.Children.Add(cpill);

            page.Children.Add(Head(Loc.T("Effects")));
            page.Children.Add(Toggle(Loc.T("Liquid glass surfaces"), Loc.T("Translucent panels and keys with a light edge."), out glassOn, out glassOff, win.SetGlass));
            page.Children.Add(Toggle(Loc.T("Animated background"), Loc.T("Colour blobs drift slowly behind the glass. Turn off to save power."), out animOn, out animOff, win.SetAnimate));
            animHint = Hint("");
            page.Children.Add(animHint);

            var reset = Ui.Key(Loc.T("Restore default look"), "func", win.ResetLook, 13);
            reset.Height = 36; reset.MinWidth = 200; reset.Padding = new Thickness(12, 0, 12, 0); reset.HorizontalAlignment = HorizontalAlignment.Left; reset.Margin = new Thickness(0, 18, 0, 0);
            page.Children.Add(reset);
            page.Children.Add(Hint(Loc.F("Settings are stored in {0}.", System.IO.Path.GetDirectoryName(Settings.Path ?? Settings.DefaultPath()))));

            page.Children.Add(Head(Loc.T("About")));
            var about = new Grid();
            about.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            about.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var logo = Ui.Logo(64); logo.Margin = new Thickness(0, 0, 14, 0); logo.VerticalAlignment = VerticalAlignment.Top;
            var info = new StackPanel();
            TextBlock appName = Ui.Label("Prisma", "Text", 20); appName.FontWeight = FontWeights.SemiBold;
            Version v = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            info.Children.Add(appName);
            info.Children.Add(Ui.Label(Loc.F("Version {0}", v.Major + "." + v.Minor + "." + v.Build), "SubText", 13));
            TextBlock blurb = Ui.Label(Loc.T("A calculator with liquid glass themes. Open source under the MIT licence."), "SubText", 12);
            blurb.TextWrapping = TextWrapping.Wrap; blurb.Margin = new Thickness(0, 6, 0, 0);
            info.Children.Add(blurb);
            Grid.SetColumn(info, 1);
            about.Children.Add(logo); about.Children.Add(info);
            var aboutCard = Ui.Card(about); aboutCard.Margin = new Thickness(0, 6, 0, 0);
            page.Children.Add(aboutCard);

            win.PrefsChanged += Refresh;
            win.Ses.ThemeChanged += RefreshPreviews;
            Refresh();
        }

        // ----------------------------------------------------------- builders
        static TextBlock Head(string text)
        {
            TextBlock t = Ui.Label(text, "Text", 16); t.FontWeight = FontWeights.SemiBold; t.Margin = new Thickness(2, 22, 0, 2);
            return t;
        }

        static TextBlock Hint(string text)
        {
            TextBlock t = Ui.Label(text, "SubText", 12); t.TextWrapping = TextWrapping.Wrap; t.Margin = new Thickness(2, 0, 0, 4);
            return t;
        }

        static Border Pill()
        {
            var pill = new Border { CornerRadius = new CornerRadius(Ui.R(11)), BorderThickness = new Thickness(1), Padding = new Thickness(1), HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 6, 0, 2) };
            pill.SetResourceReference(Border.BackgroundProperty, "FuncBg");
            pill.SetResourceReference(Border.BorderBrushProperty, "Border");
            return pill;
        }

        FrameworkElement LanguageButton(string choice, string caption)
        {
            string c = choice;
            var b = Ui.Key(caption, "func", delegate { win.SetLanguage(c); }, 13);
            b.Height = 36; b.MinWidth = 100; b.Padding = new Thickness(12, 0, 12, 0); b.Margin = new Thickness(0, 4, 6, 4);
            languageButtons[choice] = b;
            return b;
        }

        void AddMode(Panel seg, ThemeMode m, string caption)
        {
            ThemeMode mode = m;
            var b = new Button { Content = caption, FontSize = 13, MinWidth = 92, Height = 34, Padding = new Thickness(10, 0, 10, 0), Style = Ui.Seg };
            b.Click += delegate { win.SetMode(mode); };
            modeButtons[m] = b;
            seg.Children.Add(b);
        }

        FrameworkElement StyleCard(string style)
        {
            string name = style;
            var preview = new Border { Height = 50, CornerRadius = new CornerRadius(Ui.R(10)) };
            var dot = new Border { Width = 14, Height = 14, CornerRadius = new CornerRadius(7), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 8, 8) };
            var inner = new Grid(); inner.Children.Add(dot);
            preview.Child = inner;
            preview.Tag = dot;
            var label = Ui.Label(Loc.T(name), "Text", 13); label.HorizontalAlignment = HorizontalAlignment.Center; label.Margin = new Thickness(0, 6, 0, 0);
            label.TextTrimming = TextTrimming.CharacterEllipsis;
            var stack = new StackPanel(); stack.Children.Add(preview); stack.Children.Add(label);
            var ring = new Border { CornerRadius = new CornerRadius(Ui.R(12)), BorderThickness = new Thickness(2), Padding = new Thickness(6), Width = 112, Child = stack };
            var btn = Ui.Key("", "flat", delegate { win.SetStyle(name); }, 13);
            btn.Content = ring; btn.Margin = new Thickness(0, 4, 8, 4);
            cardRings[name] = ring; cardPreviews[name] = preview;
            return btn;
        }

        FrameworkElement AccentText(string choice, string caption)
        {
            string c = choice;
            var b = Ui.Key(caption, "func", delegate { win.SetAccent(c); }, 12);
            b.Height = 36; b.MinWidth = 110; b.Padding = new Thickness(10, 0, 10, 0); b.Margin = new Thickness(0, 4, 6, 4);
            accentTextButtons[choice] = b;
            return b;
        }

        FrameworkElement AccentSwatch(string hex)
        {
            string h = hex;
            Color col = ThemeManager.C(hex.TrimStart('#'));
            var dot = new Border { Width = 26, Height = 26, CornerRadius = new CornerRadius(13), Background = new SolidColorBrush(col) };
            var ring = new Border { Width = 36, Height = 36, CornerRadius = new CornerRadius(18), BorderThickness = new Thickness(2), Padding = new Thickness(3), Child = dot };
            var b = Ui.Key("", "flat", delegate { win.SetAccent(h); }, 12);
            b.Content = ring; b.Margin = new Thickness(0, 4, 0, 4);
            accentRings[hex] = ring;
            return b;
        }

        FrameworkElement Toggle(string label, string hint, out Button on, out Button off, Action<bool> set)
        {
            var row = new Grid { Margin = new Thickness(0, 8, 0, 0) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var text = new StackPanel();
            text.Children.Add(Ui.Label(label, "Text", 14));
            TextBlock h = Ui.Label(hint, "SubText", 12); h.TextWrapping = TextWrapping.Wrap; h.Margin = new Thickness(0, 2, 12, 0);
            text.Children.Add(h);
            var pill = Pill(); pill.Margin = new Thickness(0); pill.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(pill, 1);
            var seg = new StackPanel { Orientation = Orientation.Horizontal };
            on = new Button { Content = Loc.T("On"), FontSize = 12, MinWidth = 64, Height = 30, Padding = new Thickness(8, 0, 8, 0), Style = Ui.Seg };
            off = new Button { Content = Loc.T("Off"), FontSize = 12, MinWidth = 64, Height = 30, Padding = new Thickness(8, 0, 8, 0), Style = Ui.Seg };
            on.Click += delegate { set(true); }; off.Click += delegate { set(false); };
            seg.Children.Add(on); seg.Children.Add(off); pill.Child = seg;
            row.Children.Add(text); row.Children.Add(pill);
            return row;
        }

        // ------------------------------------------------------------ refresh
        void Refresh()
        {
            foreach (KeyValuePair<ThemeMode, Button> kv in modeButtons) MainWindow.Mark(kv.Value, kv.Key == win.Mode);
            foreach (KeyValuePair<string, Border> kv in cardRings)
            {
                if (kv.Key == win.StyleName) kv.Value.SetResourceReference(Border.BorderBrushProperty, "Brand"); else kv.Value.BorderBrush = Brushes.Transparent;
            }
            foreach (KeyValuePair<string, Border> kv in accentRings)
            {
                if (kv.Key.Equals(win.AccentChoice, StringComparison.OrdinalIgnoreCase)) kv.Value.SetResourceReference(Border.BorderBrushProperty, "Text"); else kv.Value.BorderBrush = Brushes.Transparent;
            }
            foreach (KeyValuePair<string, Button> kv in accentTextButtons) MainWindow.Mark2(kv.Value, kv.Key == win.AccentChoice);
            foreach (KeyValuePair<string, Button> kv in languageButtons) MainWindow.Mark2(kv.Value, string.Equals(kv.Key, win.LanguageChoice, StringComparison.OrdinalIgnoreCase));
            foreach (KeyValuePair<string, Button> kv in cornerButtons) MainWindow.Mark(kv.Value, kv.Key == win.Corners);
            MainWindow.Mark(glassOn, win.Glass); MainWindow.Mark(glassOff, !win.Glass);
            MainWindow.Mark(animOn, win.Animate); MainWindow.Mark(animOff, !win.Animate);
            animHint.Text = (!win.Glass && win.Animate) ? Loc.T("The animation is only visible while glass surfaces are on.") : "";
            RefreshPreviews();
        }

        void RefreshPreviews()
        {
            bool dark = win.Current == EffectiveTheme.Dark;
            foreach (KeyValuePair<string, Border> kv in cardPreviews)
            {
                Color[] c = ThemeManager.Preview(kv.Key, dark);
                kv.Value.Background = new LinearGradientBrush(c[0], c[1], new Point(0, 0), new Point(1, 1));
                ((Border)kv.Value.Tag).Background = new SolidColorBrush(c[2]);
            }
        }

        public bool HandleText(string text) { return false; }
        public bool HandleKey(Key key) { return false; }
        public string CopyText() { return ""; }
        public void Paste(string text) { }
        public void Recall(string value) { }
    }
}
