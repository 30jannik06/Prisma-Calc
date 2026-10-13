// Prisma - network tool views: subnet calculator, address converter, transfer time. C# 5.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Prisma
{
    // A list of "label  value" rows; clicking a row copies its value.
    class ResultsPanel : StackPanel
    {
        public readonly List<KeyValuePair<string, string>> Data = new List<KeyValuePair<string, string>>();

        public void Clear() { Children.Clear(); Data.Clear(); }

        public void Show(List<KeyValuePair<string, string>> rows)
        {
            Clear();
            foreach (KeyValuePair<string, string> kv in rows) { Data.Add(kv); Children.Add(MakeRow(Loc.T(kv.Key), kv.Value)); }
        }

        public void ShowPlain(List<string> lines)
        {
            Clear();
            foreach (string l in lines) { Data.Add(new KeyValuePair<string, string>("", l)); Children.Add(MakeRow(null, l)); }
        }

        static UIElement MakeRow(string label, string value)
        {
            var g = new Grid { Margin = new Thickness(0, 3, 0, 3), Background = Brushes.Transparent, Cursor = Cursors.Hand, ToolTip = Loc.T("Click to copy") };
            string val = value;
            if (label != null)
            {
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                TextBlock l = Ui.Label(label, "SubText", 12); l.VerticalAlignment = VerticalAlignment.Top; l.Margin = new Thickness(0, 2, 8, 0);
                g.Children.Add(l);
            }
            else g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            TextBlock v = Ui.Label(value, "Text", 13);
            v.FontFamily = new FontFamily("Cascadia Mono, Consolas, Segoe UI"); v.TextWrapping = TextWrapping.Wrap;
            if (label != null) Grid.SetColumn(v, 1);
            g.Children.Add(v);
            g.MouseLeftButtonUp += delegate { try { Clipboard.SetText(val); } catch (Exception ex) { Log.Warn("Copy failed: " + ex.Message); } };
            return g;
        }

        public string AsText()
        {
            var sb = new StringBuilder();
            foreach (KeyValuePair<string, string> kv in Data) sb.Append(kv.Key.Length > 0 ? Loc.T(kv.Key) + ": " + kv.Value : kv.Value).Append("\r\n");
            return sb.ToString().TrimEnd();
        }
    }

    static class NetKit
    {
        public static TextBlock Heading(string text)
        {
            TextBlock t = Ui.Label(text, "Text", 15); t.FontWeight = FontWeights.SemiBold; t.Margin = new Thickness(0, 0, 0, 6);
            return t;
        }

        public static TextBlock Note(string text, string brush)
        {
            TextBlock t = Ui.Label(text, brush, 12); t.TextWrapping = TextWrapping.Wrap; t.Margin = new Thickness(2, 4, 0, 0);
            return t;
        }

        public static TextBox Input(string text, int maxLength)
        {
            return new TextBox { Text = text, MaxLength = maxLength, FontSize = 16, FontFamily = new FontFamily("Cascadia Mono, Consolas, Segoe UI"), Margin = new Thickness(0, 2, 0, 2) };
        }

        public static void SetBrush(TextBlock t, string key) { t.SetResourceReference(TextBlock.ForegroundProperty, key); }

        public static Border Section(string title, params UIElement[] content)
        {
            var sp = new StackPanel();
            sp.Children.Add(Heading(title));
            foreach (UIElement e in content) sp.Children.Add(e);
            Border b = Ui.Card(sp); b.Margin = new Thickness(0, 0, 0, 10);
            return b;
        }
    }

    // ------------------------------------------------------- subnet calculator
    public class SubnetView : IModeView
    {
        readonly ScrollViewer root = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        readonly TextBox input, checkBox, newPrefixBox, hostsBox;
        readonly TextBlock status, checkOut, splitInfo, hostOut;
        readonly ResultsPanel info = new ResultsPanel(), splitRows = new ResultsPanel();
        IpAddr curIp; int curPrefix; bool valid;

        public UIElement Root { get { return root; } }
        public double PreferredWidth { get { return 620; } }
        public double PreferredHeight { get { return 760; } }
        public bool HasHistory { get { return false; } }
        public int InfoRowCount { get { return info.Data.Count; } }

        public SubnetView()
        {
            var page = new StackPanel { Margin = new Thickness(0, 0, 10, 8) };
            root.Content = page;

            input = NetKit.Input("192.168.1.10/24", NetCalc.MaxInput);
            status = NetKit.Note("", "SubText");
            var hint = NetKit.Note(Loc.T("Examples: 192.168.1.10/24, 10.0.0.5 255.255.255.0, 2001:db8::1/64"), "SubText");
            page.Children.Add(NetKit.Section(Loc.T("Address / prefix"), input, hint, status, info));

            checkBox = NetKit.Input("192.168.1.200", NetCalc.MaxInput);
            checkOut = NetKit.Note("", "Text");
            page.Children.Add(NetKit.Section(Loc.T("Check an address"), checkBox, checkOut));

            newPrefixBox = NetKit.Input("26", 4);
            splitInfo = NetKit.Note("", "SubText");
            page.Children.Add(NetKit.Section(Loc.T("Split into subnets"), NetKit.Note(Loc.T("New prefix length"), "SubText"), newPrefixBox, splitInfo, splitRows));

            hostsBox = NetKit.Input("50", 12);
            hostOut = NetKit.Note("", "Text");
            page.Children.Add(NetKit.Section(Loc.T("Hosts needed"), hostsBox, hostOut));

            foreach (TextBox t in new[] { input, checkBox, newPrefixBox, hostsBox }) t.TextChanged += delegate { Recalc(); };
            Recalc();
        }

        public void SetInput(string text) { input.Text = text; }
        public void SetCheck(string text) { checkBox.Text = text; }
        public void SetNewPrefix(string text) { newPrefixBox.Text = text; }
        public void SetHosts(string text) { hostsBox.Text = text; }
        public string StatusText { get { return status.Text; } }
        public string CheckText { get { return checkOut.Text; } }
        public string SplitText { get { return splitInfo.Text; } }
        public int SplitRowCount { get { return splitRows.Data.Count; } }
        public string HostText { get { return hostOut.Text; } }
        public string Value(string labelKey)
        {
            foreach (KeyValuePair<string, string> kv in info.Data) if (kv.Key == labelKey) return kv.Value;
            return null;
        }

        void Recalc()
        {
            IpAddr ip; int p; string err;
            if (!NetCalc.TryParseCidr(input.Text, out ip, out p, out err))
            {
                valid = false; status.Text = err; NetKit.SetBrush(status, "SubText"); info.Clear();
            }
            else
            {
                valid = true; curIp = ip; curPrefix = p; status.Text = "";
                info.Show(NetCalc.Subnet(ip, p).Rows);
            }
            RecalcCheck(); RecalcSplit(); RecalcHosts();
        }

        void RecalcCheck()
        {
            checkOut.Text = "";
            if (!valid || checkBox.Text.Trim().Length == 0) return;
            IpAddr a;
            if (!NetCalc.TryParseIp(checkBox.Text, out a)) { checkOut.Text = Loc.T("Not a valid address"); NetKit.SetBrush(checkOut, "SubText"); return; }
            if (a.V6 != curIp.V6) { checkOut.Text = Loc.T("Different IP version"); NetKit.SetBrush(checkOut, "SubText"); return; }
            bool inside = NetCalc.Contains(curIp, curPrefix, a);
            checkOut.Text = inside ? Loc.T("Inside this network") : Loc.T("Outside this network");
            NetKit.SetBrush(checkOut, inside ? "Brand" : "Text");
        }

        void RecalcSplit()
        {
            splitInfo.Text = ""; splitRows.Clear();
            if (!valid) return;
            string t = newPrefixBox.Text.Trim().TrimStart('/');
            if (t.Length == 0) return;
            int np;
            if (!NetCalc.TryInt(t, out np) || np <= curPrefix || np > curIp.Bits)
            {
                splitInfo.Text = Loc.F("Enter a prefix between /{0} and /{1}", curPrefix + 1, curIp.Bits); return;
            }
            string total;
            List<string> rows = NetCalc.Split(curIp, curPrefix, np, 64, out total);
            bool more = total.Length > 3 || int.Parse(total, CultureInfo.InvariantCulture) > rows.Count;
            splitInfo.Text = Loc.F("{0} subnets", total) + (more ? " - " + Loc.F("showing the first {0}", rows.Count) : "");
            splitRows.ShowPlain(rows);
        }

        void RecalcHosts()
        {
            hostOut.Text = "";
            string t = hostsBox.Text.Trim();
            if (t.Length == 0) return;
            ulong n;
            if (t.Length > 12 || !ulong.TryParse(t, NumberStyles.None, CultureInfo.InvariantCulture, out n) || n == 0) { hostOut.Text = Loc.T("Enter a number of hosts"); return; }
            string usable;
            int p = NetCalc.PrefixForHosts(valid && curIp.V6, n, out usable);
            hostOut.Text = p < 0 ? Loc.T("Too many hosts") : Loc.F("Smallest subnet: /{0} ({1} usable)", p, usable);
        }

        public bool HandleText(string text) { return false; }
        public bool HandleKey(Key key) { return false; }
        public string CopyText() { return info.AsText(); }
        public void Paste(string text) { if (text != null && text.Length <= NetCalc.MaxInput) input.Text = text.Trim(); }
        public void Recall(string value) { }
    }

    // ------------------------------------------------------- address converter
    public class AddressView : IModeView
    {
        readonly StackPanel root = new StackPanel { Margin = new Thickness(0, 0, 10, 8) };
        readonly TextBox input;
        readonly TextBlock status;
        readonly ResultsPanel rows = new ResultsPanel();

        public UIElement Root { get { return root; } }
        public double PreferredWidth { get { return 620; } }
        public double PreferredHeight { get { return 640; } }
        public bool HasHistory { get { return false; } }

        public AddressView()
        {
            input = NetKit.Input("192.168.1.1", NetCalc.MaxInput);
            status = NetKit.Note("", "SubText");
            var hint = NetKit.Note(Loc.T("Examples: 192.168.1.1, 3232235777, 0xC0A80101, 2001:db8::1"), "SubText");
            root.Children.Add(NetKit.Section(Loc.T("Address or number"), input, hint, status, rows));
            input.TextChanged += delegate { Recalc(); };
            Recalc();
        }

        public void SetInput(string text) { input.Text = text; }
        public string StatusText { get { return status.Text; } }
        public string Value(string labelKey)
        {
            foreach (KeyValuePair<string, string> kv in rows.Data) if (kv.Key == labelKey) return kv.Value;
            return null;
        }

        void Recalc()
        {
            IpAddr ip;
            if (input.Text.Trim().Length == 0) { status.Text = Loc.T("Enter an address"); rows.Clear(); return; }
            if (!NetCalc.TryParseAny(input.Text, out ip)) { status.Text = Loc.T("Not a valid address"); rows.Clear(); return; }
            status.Text = "";
            rows.Show(NetCalc.ConvertAddress(ip));
        }

        public bool HandleText(string text) { return false; }
        public bool HandleKey(Key key) { return false; }
        public string CopyText() { return rows.AsText(); }
        public void Paste(string text) { if (text != null && text.Length <= NetCalc.MaxInput) input.Text = text.Trim(); }
        public void Recall(string value) { }
    }

    // ------------------------------------------------------------ transfer time
    public class TransferView : IModeView
    {
        readonly StackPanel root = new StackPanel { Margin = new Thickness(0, 0, 10, 8) };
        readonly TextBox sizeBox, speedBox, shareBox, timeBox;
        readonly ComboBox sizeUnit = new ComboBox(), speedUnit = new ComboBox();
        readonly TextBlock status, detail;
        readonly Button[] solveButtons = new Button[3];
        int solve;               // 0 = time, 1 = speed, 2 = size
        bool updating;

        public UIElement Root { get { return root; } }
        public double PreferredWidth { get { return 560; } }
        public double PreferredHeight { get { return 640; } }
        public bool HasHistory { get { return false; } }
        public string StatusText { get { return status.Text; } }
        public string TimeText { get { return timeBox.Text; } }
        public string SpeedText { get { return speedBox.Text; } }
        public string SizeText { get { return sizeBox.Text; } }

        public TransferView()
        {
            foreach (string u in TransferCalc.SizeUnits) sizeUnit.Items.Add(u);
            foreach (string u in TransferCalc.SpeedUnits) speedUnit.Items.Add(u);
            sizeUnit.SelectedIndex = 4; speedUnit.SelectedIndex = 2;
            sizeUnit.FontSize = 14; speedUnit.FontSize = 14;
            sizeBox = NetKit.Input("1", 24); speedBox = NetKit.Input("100", 24); shareBox = NetKit.Input("100", 8); timeBox = NetKit.Input("", 40);

            var pill = new Border { CornerRadius = new CornerRadius(Ui.R(11)), BorderThickness = new Thickness(1), Padding = new Thickness(1), HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 4, 0, 10) };
            pill.SetResourceReference(Border.BackgroundProperty, "FuncBg"); pill.SetResourceReference(Border.BorderBrushProperty, "Border");
            var seg = new StackPanel { Orientation = Orientation.Horizontal };
            string[] names = { Loc.T("Time"), Loc.T("Speed"), Loc.T("Size") };
            for (int i = 0; i < 3; i++)
            {
                int idx = i;
                var b = new Button { Content = names[i], FontSize = 13, MinWidth = 90, Height = 32, Padding = new Thickness(10, 0, 10, 0), Style = Ui.Seg };
                b.Click += delegate { SetSolve(idx); };
                solveButtons[i] = b; seg.Children.Add(b);
            }
            pill.Child = seg;
            root.Children.Add(NetKit.Section(Loc.T("Solve for"), pill));

            status = NetKit.Note("", "SubText");
            detail = NetKit.Note("", "SubText");
            var fields = new StackPanel();
            fields.Children.Add(Row(Loc.T("Size"), sizeBox, sizeUnit));
            fields.Children.Add(Row(Loc.T("Speed"), speedBox, speedUnit));
            fields.Children.Add(Row(Loc.T("Share of the line speed (%)"), shareBox, null));
            fields.Children.Add(Row(Loc.T("Time"), timeBox, null));
            fields.Children.Add(NetKit.Note(Loc.T("Time examples: 90, 1:30:00, 2h 15m"), "SubText"));
            fields.Children.Add(status); fields.Children.Add(detail);
            root.Children.Add(NetKit.Section(Loc.T("Transfer"), fields));

            foreach (TextBox t in new[] { sizeBox, speedBox, shareBox, timeBox }) t.TextChanged += delegate { Recalc(); };
            sizeUnit.SelectionChanged += delegate { Recalc(); };
            speedUnit.SelectionChanged += delegate { Recalc(); };
            timeBox.Text = "";
            SetSolve(0);
        }

        static UIElement Row(string label, TextBox box, ComboBox unit)
        {
            var g = new Grid { Margin = new Thickness(0, 4, 0, 4) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = unit == null ? new GridLength(0) : new GridLength(120) });
            TextBlock l = Ui.Label(label, "SubText", 12); l.VerticalAlignment = VerticalAlignment.Center; l.TextWrapping = TextWrapping.Wrap;
            Grid.SetColumn(box, 1); g.Children.Add(l); g.Children.Add(box);
            if (unit != null) { unit.Margin = new Thickness(8, 2, 0, 2); Grid.SetColumn(unit, 2); g.Children.Add(unit); }
            return g;
        }

        public void SetSolve(int what)
        {
            solve = what;
            for (int i = 0; i < 3; i++) MainWindow.Mark(solveButtons[i], i == what);
            timeBox.IsReadOnly = what == 0; speedBox.IsReadOnly = what == 1; sizeBox.IsReadOnly = what == 2;
            timeBox.Opacity = what == 0 ? 0.85 : 1; speedBox.Opacity = what == 1 ? 0.85 : 1; sizeBox.Opacity = what == 2 ? 0.85 : 1;
            updating = true;
            if (what == 0) timeBox.Text = ""; else if (timeBox.Text.Length == 0) timeBox.Text = "1:00:00";
            updating = false;
            Recalc();
        }

        public void SetInputs(string size, int sizeUnitIndex, string speed, int speedUnitIndex, string share, string time)
        {
            updating = true;
            sizeBox.Text = size; sizeUnit.SelectedIndex = sizeUnitIndex; speedBox.Text = speed; speedUnit.SelectedIndex = speedUnitIndex;
            shareBox.Text = share; timeBox.Text = time;
            updating = false;
            Recalc();
        }

        static bool TryNum(string s, out double v)
        {
            v = 0;
            if (string.IsNullOrWhiteSpace(s) || s.Length > 30) return false;
            return double.TryParse(s.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out v) && !double.IsNaN(v) && !double.IsInfinity(v) && v >= 0;
        }

        void Recalc()
        {
            if (updating) return;
            status.Text = ""; detail.Text = "";
            double share, size = 0, speed = 0, secs = 0;
            if (!TryNum(shareBox.Text, out share) || share <= 0 || share > 100) { status.Text = Loc.T("Share must be between 1 and 100"); return; }
            double sizeBits = 0, speedBits = 0;
            bool needSize = solve != 2, needSpeed = solve != 1, needTime = solve != 0;
            if (needSize) { if (!TryNum(sizeBox.Text, out size)) { status.Text = Loc.T("Enter a size"); return; } sizeBits = size * TransferCalc.SizeBits[sizeUnit.SelectedIndex]; }
            if (needSpeed) { if (!TryNum(speedBox.Text, out speed) || speed <= 0) { status.Text = Loc.T("Enter a speed"); return; } speedBits = speed * TransferCalc.SpeedBits[speedUnit.SelectedIndex]; }
            if (needTime) { if (!TransferCalc.TryParseDuration(timeBox.Text, out secs) || secs <= 0) { status.Text = Loc.T("Enter a duration like 90, 1:30:00 or 2h 15m"); return; } }
            updating = true;
            try
            {
                if (solve == 0)
                {
                    double t = TransferCalc.TimeFor(sizeBits, speedBits, share);
                    timeBox.Text = TransferCalc.FormatDuration(t);
                    if (!double.IsInfinity(t) && t >= 60) detail.Text = Loc.F("{0} seconds in total", TransferCalc.FormatNumber(t));
                }
                else if (solve == 1)
                {
                    double s = TransferCalc.SpeedFor(sizeBits, secs, share) / TransferCalc.SpeedBits[speedUnit.SelectedIndex];
                    speedBox.Text = TransferCalc.FormatNumber(s);
                }
                else
                {
                    double bits = TransferCalc.SizeFor(speedBits, secs, share);
                    sizeBox.Text = TransferCalc.FormatNumber(bits / TransferCalc.SizeBits[sizeUnit.SelectedIndex]);
                }
            }
            finally { updating = false; }
        }

        public bool HandleText(string text) { return false; }
        public bool HandleKey(Key key) { return false; }

        public string CopyText()
        {
            return solve == 0 ? timeBox.Text : solve == 1 ? speedBox.Text + " " + speedUnit.SelectedItem : sizeBox.Text + " " + sizeUnit.SelectedItem;
        }

        public void Paste(string text) { }
        public void Recall(string value) { }
    }
}
