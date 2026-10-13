// Prisma - Date calculation, unit converters and graphing views. C# 5.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace Prisma
{
    static class Fmt
    {
        public static string Plural(int n, string one, string many) { return Loc.Plural(n, one, many); }
    }

    // ------------------------------------------------------------------ Date
    public class DateView : IModeView
    {
        readonly StackPanel root = new StackPanel { Margin = new Thickness(6) };
        readonly ComboBox modeBox = new ComboBox();
        readonly StackPanel diffPanel = new StackPanel(), addPanel = new StackPanel();
        readonly TextBox fromBox = new TextBox(), toBox = new TextBox(), addFromBox = new TextBox();
        readonly TextBox yBox = new TextBox(), mBox = new TextBox(), dBox = new TextBox();
        readonly TextBlock diffOut, diffTotal, addOut, hint;
        readonly Button addBtn, subBtn;
        bool subtract;

        public UIElement Root { get { return root; } }
        public double PreferredWidth { get { return 440; } }
        public double PreferredHeight { get { return 540; } }
        public bool HasHistory { get { return false; } }
        public string DifferenceText { get { return diffOut.Text; } }
        public string TotalText { get { return diffTotal.Text; } }
        public string AddResultText { get { return addOut.Text; } }

        static TextBox Field(string text, double width)
        {
            var t = new TextBox { Text = text, FontSize = 16, Margin = new Thickness(0, 2, 0, 8) };
            if (width > 0) t.Width = width;
            return t;
        }

        public DateView()
        {
            string today = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            root.Children.Add(Ui.Label(Loc.T("Calculation"), "SubText", 13));
            modeBox.Items.Add(Loc.T("Difference between dates")); modeBox.Items.Add(Loc.T("Add or subtract"));
            modeBox.SelectedIndex = 0; modeBox.Margin = new Thickness(0, 2, 0, 12); modeBox.FontSize = 15;
            modeBox.SelectionChanged += delegate { ShowMode(); };
            root.Children.Add(modeBox);
            hint = Ui.Label(Loc.T("Dates as yyyy-mm-dd or dd.mm.yyyy"), "SubText", 12);

            fromBox.Text = today; fromBox.FontSize = 16; fromBox.Margin = new Thickness(0, 2, 0, 8);
            toBox.Text = today; toBox.FontSize = 16; toBox.Margin = new Thickness(0, 2, 0, 8);
            diffPanel.Children.Add(Ui.Label(Loc.T("From"), "SubText", 13)); diffPanel.Children.Add(fromBox);
            diffPanel.Children.Add(Ui.Label(Loc.T("To"), "SubText", 13)); diffPanel.Children.Add(toBox);
            diffOut = Ui.Label("", "Text", 24); diffOut.TextWrapping = TextWrapping.Wrap;
            diffTotal = Ui.Label("", "SubText", 14); diffTotal.TextWrapping = TextWrapping.Wrap; diffTotal.Margin = new Thickness(0, 6, 0, 0);
            var dc = new StackPanel(); dc.Children.Add(diffOut); dc.Children.Add(diffTotal);
            diffPanel.Children.Add(Ui.Card(dc));

            addFromBox.Text = today; addFromBox.FontSize = 16; addFromBox.Margin = new Thickness(0, 2, 0, 8);
            addPanel.Children.Add(Ui.Label(Loc.T("From"), "SubText", 13)); addPanel.Children.Add(addFromBox);
            var pill = new Border { CornerRadius = new CornerRadius(Ui.R(10)), BorderThickness = new Thickness(1), Padding = new Thickness(1), HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 4, 0, 10) };
            pill.SetResourceReference(Border.BackgroundProperty, "FuncBg"); pill.SetResourceReference(Border.BorderBrushProperty, "Border");
            var seg = new StackPanel { Orientation = Orientation.Horizontal };
            addBtn = SegButton(Loc.T("Add"), delegate { subtract = false; Update(); });
            subBtn = SegButton(Loc.T("Subtract"), delegate { subtract = true; Update(); });
            seg.Children.Add(addBtn); seg.Children.Add(subBtn); pill.Child = seg;
            addPanel.Children.Add(pill);
            var parts = new Grid();
            for (int i = 0; i < 3; i++) parts.ColumnDefinitions.Add(new ColumnDefinition());
            string[] names = { Loc.T("Years"), Loc.T("Months"), Loc.T("Days") };
            TextBox[] boxes = { yBox, mBox, dBox };
            for (int i = 0; i < 3; i++)
            {
                var col = new StackPanel { Margin = new Thickness(0, 0, i < 2 ? 8 : 0, 0) };
                boxes[i].Text = "0"; boxes[i].FontSize = 16; boxes[i].Margin = new Thickness(0, 2, 0, 8);
                col.Children.Add(Ui.Label(names[i], "SubText", 13)); col.Children.Add(boxes[i]);
                Grid.SetColumn(col, i); parts.Children.Add(col);
            }
            addPanel.Children.Add(parts);
            addOut = Ui.Label("", "Text", 24); addOut.TextWrapping = TextWrapping.Wrap;
            addPanel.Children.Add(Ui.Card(addOut));

            root.Children.Add(diffPanel); root.Children.Add(addPanel);
            root.Children.Add(hint);
            hint.Margin = new Thickness(0, 10, 0, 0);
            foreach (TextBox t in new[] { fromBox, toBox, addFromBox }) t.MaxLength = 24;
            foreach (TextBox t in new[] { yBox, mBox, dBox }) t.MaxLength = 5;
            foreach (TextBox t in new[] { fromBox, toBox, addFromBox, yBox, mBox, dBox }) t.TextChanged += delegate { Update(); };
            ShowMode(); Update();
        }

        Button SegButton(string cap, Action a)
        {
            var b = new Button { Content = cap, FontSize = 12, Width = 80, Height = 30 };
            b.Style = Ui.Seg;
            b.Click += delegate { a(); };
            return b;
        }

        void ShowMode()
        {
            bool diff = modeBox.SelectedIndex <= 0;
            diffPanel.Visibility = diff ? Visibility.Visible : Visibility.Collapsed;
            addPanel.Visibility = diff ? Visibility.Collapsed : Visibility.Visible;
        }

        public void SetDifference(string from, string to) { fromBox.Text = from; toBox.Text = to; }
        public void SetAdd(string from, string y, string m, string d, bool sub)
        {
            subtract = sub; addFromBox.Text = from; yBox.Text = y; mBox.Text = m; dBox.Text = d; Update();
        }

        static bool Int(string s, out int v) { return int.TryParse((s ?? "").Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out v) && v >= 0 && v < 100000; }

        void Update()
        {
            if (addBtn == null || subBtn == null) return;
            addBtn.SetResourceReference(Control.BackgroundProperty, subtract ? "FuncBg" : "AccentFill");
            addBtn.SetResourceReference(Control.ForegroundProperty, subtract ? "Text" : "AccentText");
            subBtn.SetResourceReference(Control.BackgroundProperty, subtract ? "AccentFill" : "FuncBg");
            subBtn.SetResourceReference(Control.ForegroundProperty, subtract ? "AccentText" : "Text");
            DateTime a, b;
            if (DateCalc.TryParse(fromBox.Text, out a) && DateCalc.TryParse(toBox.Text, out b))
            {
                DateCalc.Diff d = DateCalc.Difference(a, b);
                if (d.TotalDays == 0) { diffOut.Text = Loc.T("Same date"); diffTotal.Text = ""; }
                else
                {
                    var parts = new List<string>();
                    if (d.Years > 0) parts.Add(Fmt.Plural(d.Years, "year", "years"));
                    if (d.Months > 0) parts.Add(Fmt.Plural(d.Months, "month", "months"));
                    if (d.Days > 0) parts.Add(Fmt.Plural(d.Days, "day", "days"));
                    diffOut.Text = string.Join(", ", parts.ToArray());
                    diffTotal.Text = Loc.F("Total: {0}", Fmt.Plural(d.TotalDays, "day", "days") + " (" + Fmt.Plural(d.TotalDays / 7, "week", "weeks") + ", " + Fmt.Plural(d.TotalDays % 7, "day", "days") + ")");
                }
            }
            else { diffOut.Text = Loc.T("Enter two valid dates"); diffTotal.Text = ""; }
            int y, m, dd;
            DateTime f;
            if (DateCalc.TryParse(addFromBox.Text, out f) && Int(yBox.Text, out y) && Int(mBox.Text, out m) && Int(dBox.Text, out dd))
            {
                try { addOut.Text = Loc.DateText(DateCalc.Add(f, y, m, dd, subtract)); }
                catch (ArgumentOutOfRangeException) { addOut.Text = Loc.T("Date out of range"); }
            }
            else addOut.Text = Loc.T("Enter a valid date and whole numbers");
        }

        public bool HandleText(string text) { return false; }
        public bool HandleKey(Key key) { return false; }
        public string CopyText() { return diffPanel.Visibility == Visibility.Visible ? diffOut.Text : addOut.Text; }
        public void Paste(string text) { }
        public void Recall(string value) { }
    }

    // ------------------------------------------------------------- Converter
    public class ConverterView : IModeView
    {
        readonly AppMode mode;
        readonly Category cat;
        readonly Grid root = new Grid { Margin = new Thickness(6) };
        readonly TextBox input = new TextBox(), output = new TextBox();
        readonly ComboBox fromBox = new ComboBox(), toBox = new ComboBox();
        readonly StackPanel list = new StackPanel();

        public UIElement Root { get { return root; } }
        public double PreferredWidth { get { return 440; } }
        public double PreferredHeight { get { return 720; } }
        public bool HasHistory { get { return false; } }
        public Category Cat { get { return cat; } }
        public string OutputText { get { return output.Text; } }

        static readonly Dictionary<AppMode, int[]> Defaults = new Dictionary<AppMode, int[]>
        {
            { AppMode.Currency, new[] { 0, 1 } }, { AppMode.Volume, new[] { 1, 9 } }, { AppMode.Length, new[] { 4, 7 } }, { AppMode.Weight, new[] { 2, 5 } },
            { AppMode.Temperature, new[] { 0, 1 } }, { AppMode.Energy, new[] { 0, 1 } }, { AppMode.Area, new[] { 2, 6 } }, { AppMode.Speed, new[] { 1, 2 } },
            { AppMode.Time, new[] { 3, 4 } }, { AppMode.Power, new[] { 0, 2 } }, { AppMode.Data, new[] { 6, 4 } }, { AppMode.Pressure, new[] { 3, 5 } }, { AppMode.Angle, new[] { 0, 1 } }
        };

        public ConverterView(AppMode m)
        {
            mode = m; cat = Units.Get(m);
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            var top = new StackPanel();
            input.MaxLength = 40; input.Text = "1"; input.FontSize = 26; input.Margin = new Thickness(0, 2, 0, 6);
            output.IsReadOnly = true; output.FontSize = 26; output.Margin = new Thickness(0, 2, 0, 6);
            fromBox.FontSize = 15; toBox.FontSize = 15;
            foreach (Unit u in cat.Units) { fromBox.Items.Add(u); toBox.Items.Add(u); }
            int[] d = Defaults[m];
            fromBox.SelectedIndex = Math.Min(d[0], cat.Units.Length - 1); toBox.SelectedIndex = Math.Min(d[1], cat.Units.Length - 1);
            fromBox.Margin = new Thickness(0, 0, 0, 10); toBox.Margin = new Thickness(0, 0, 0, 6);
            top.Children.Add(input); top.Children.Add(fromBox);
            var swap = Ui.Key("⇅  " + Loc.T("Swap"), "func", Swap, 13); swap.Height = 30; swap.HorizontalAlignment = HorizontalAlignment.Left; swap.Width = 100; swap.Margin = new Thickness(0, 0, 0, 6);
            top.Children.Add(swap);
            top.Children.Add(output); top.Children.Add(toBox);
            if (cat.Sample)
            {
                var note = Ui.Label(Loc.F("Example rates, not live. Edit {0} (CODE=rate per 1 EUR) to use your own.", Units.RatesPath()), "SubText", 12);
                note.TextWrapping = TextWrapping.Wrap; note.Margin = new Thickness(0, 2, 0, 6);
                top.Children.Add(note);
            }
            Grid.SetRow(top, 0); root.Children.Add(top);

            var pad = new UniformGrid { Columns = 3, Margin = new Thickness(0, 4, 0, 0) };
            foreach (string k in new[] { "7", "8", "9", "4", "5", "6", "1", "2", "3", "±", "0", "." })
            {
                string key = k;
                pad.Children.Add(Ui.Key(k, "num", delegate { Append(key); }, 18));
            }
            var pad2 = new UniformGrid { Columns = 2 };
            pad2.Children.Add(Ui.Key("C", "func", Clear, 16)); pad2.Children.Add(Ui.Key("⌫", "func", Back, 16));
            var padHost = new StackPanel(); padHost.Children.Add(pad2); padHost.Children.Add(pad);
            Grid.SetRow(padHost, 1); root.Children.Add(padHost);

            var sv = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(0, 8, 0, 0), Content = list };
            Grid.SetRow(sv, 2); root.Children.Add(sv);

            input.TextChanged += delegate { Recalculate(); };
            fromBox.SelectionChanged += delegate { Recalculate(); };
            toBox.SelectionChanged += delegate { Recalculate(); };
            Recalculate();
        }

        public void SetInput(string text) { input.Text = text; }
        public void Select(int from, int to) { fromBox.SelectedIndex = from; toBox.SelectedIndex = to; }

        void Swap() { int a = fromBox.SelectedIndex; fromBox.SelectedIndex = toBox.SelectedIndex; toBox.SelectedIndex = a; }

        void Append(string k)
        {
            if (k == "±") { input.Text = input.Text.StartsWith("-") ? input.Text.Substring(1) : "-" + input.Text; return; }
            if (k == "." && input.Text.IndexOf('.') >= 0) return;
            input.Text = (input.Text == "0" && k != ".") ? k : input.Text + k;
        }
        void Back() { input.Text = input.Text.Length > 1 ? input.Text.Substring(0, input.Text.Length - 1) : "0"; }
        void Clear() { input.Text = "0"; }

        void Recalculate()
        {
            Unit f = fromBox.SelectedItem as Unit, t = toBox.SelectedItem as Unit;
            list.Children.Clear();
            double v;
            if (f == null || t == null || !Ui.TryNumber(input.Text, out v)) { output.Text = "—"; return; }
            output.Text = Loc.Num(Units.Format(Units.Convert(cat, v, f, t)));
            foreach (Unit u in cat.Units)
            {
                var g = new Grid { Margin = new Thickness(4, 2, 4, 2) };
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                TextBlock n = Ui.Label(u.ToString(), "SubText", 13);
                TextBlock val = Ui.Label(Loc.Num(Units.Format(Units.Convert(cat, v, f, u))) + " " + u.Abbr, "Text", 13);
                Grid.SetColumn(val, 1);
                g.Children.Add(n); g.Children.Add(val);
                list.Children.Add(g);
            }
        }

        public bool HandleText(string t)
        {
            if (string.IsNullOrEmpty(t)) return false;
            char c = t[0];
            if (char.IsDigit(c)) { Append(c.ToString()); return true; }
            if (c == '.' || c == ',') { Append("."); return true; }
            if (c == '-') { Append("±"); return true; }
            return false;
        }

        public bool HandleKey(Key key)
        {
            if (key == Key.Back) Back();
            else if (key == Key.Escape || key == Key.Delete) Clear();
            else return false;
            return true;
        }

        public string CopyText() { return output.Text; }
        public void Paste(string text) { double v; if (text != null && text.Length <= 40 && Ui.TryNumber(text, out v)) input.Text = text.Trim(); }
        public void Recall(string value) { Paste(value); }
    }

    // --------------------------------------------------------------- Graphing
    public class GraphCanvas : FrameworkElement
    {
        public double Cx, Cy, Scale = 50;
        public readonly Func<double, double>[] Funcs = new Func<double, double>[6];
        public Func<EffectiveTheme> Theme = delegate { return EffectiveTheme.Light; };
        public event Action<string> Trace;
        Point? mouse; bool dragging; Point dragStart; double dragCx, dragCy;

        public GraphCanvas() { ClipToBounds = true; }

        static double NiceStep(double min)
        {
            double p = Math.Pow(10, Math.Floor(Math.Log10(min)));
            foreach (double m in new[] { 1.0, 2.0, 5.0, 10.0 }) if (p * m >= min) return p * m;
            return p * 10;
        }

        static double Safe(Func<double, double> f, double x)
        {
            try { return f(x); } catch (Exception) { return double.NaN; }
        }

        FormattedText Text(string s, Brush b)
        {
            return new FormattedText(s, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 11, b, VisualTreeHelper.GetDpi(this).PixelsPerDip);
        }

        static string Num(double v) { return Loc.Num(NumRaw(v)); }

        static string NumRaw(double v)
        {
            if (Math.Abs(v) < 1e-12) return "0";
            return Math.Abs(v) >= 1e6 || Math.Abs(v) < 1e-3 ? v.ToString("0.##E+0", CultureInfo.InvariantCulture) : v.ToString("G6", CultureInfo.InvariantCulture);
        }

        protected override void OnRender(DrawingContext dc)
        {
            double w = ActualWidth, h = ActualHeight;
            if (w < 2 || h < 2) return;
            Brush bg = Ui.Res(this, "GraphBg", Brushes.White), sub = Ui.Res(this, "SubText", Brushes.Gray), grid = Ui.Res(this, "Border", Brushes.LightGray);
            dc.DrawRectangle(bg, null, new Rect(0, 0, w, h));
            var gridPen = new Pen(grid, 1); var axisPen = new Pen(sub, 1.6);
            double step = NiceStep(70.0 / Scale);
            double x0 = Cx - w / 2 / Scale, x1 = Cx + w / 2 / Scale, y0 = Cy - h / 2 / Scale, y1 = Cy + h / 2 / Scale;
            double axisX = (0 - Cx) * Scale + w / 2, axisY = h / 2 - (0 - Cy) * Scale;
            double labelY = Math.Max(2, Math.Min(h - 16, axisY + 3)), labelX = Math.Max(2, Math.Min(w - 40, axisX + 4));
            for (long k = (long)Math.Ceiling(x0 / step); k * step <= x1; k++)
            {
                double px = (k * step - Cx) * Scale + w / 2;
                dc.DrawLine(gridPen, new Point(px, 0), new Point(px, h));
                if (k != 0) dc.DrawText(Text(Num(k * step), sub), new Point(px + 3, labelY));
            }
            for (long k = (long)Math.Ceiling(y0 / step); k * step <= y1; k++)
            {
                double py = h / 2 - (k * step - Cy) * Scale;
                dc.DrawLine(gridPen, new Point(0, py), new Point(w, py));
                if (k != 0) dc.DrawText(Text(Num(k * step), sub), new Point(labelX, py - 14));
            }
            dc.DrawLine(axisPen, new Point(axisX, 0), new Point(axisX, h));
            dc.DrawLine(axisPen, new Point(0, axisY), new Point(w, axisY));

            Color[] cols = ThemeManager.CurveColors(Theme());
            for (int i = 0; i < Funcs.Length; i++)
            {
                Func<double, double> f = Funcs[i];
                if (f == null) continue;
                var brush = new SolidColorBrush(cols[i % cols.Length]); brush.Freeze();
                var geo = new StreamGeometry();
                using (StreamGeometryContext ctx = geo.Open())
                {
                    bool pen = false; double prev = 0;
                    for (int px = 0; px <= (int)w; px++)
                    {
                        double y = Safe(f, Cx + (px - w / 2) / Scale);
                        if (double.IsNaN(y) || double.IsInfinity(y)) { pen = false; continue; }
                        double py = h / 2 - (y - Cy) * Scale;
                        if (Math.Abs(py) > 1e6) { pen = false; continue; }
                        if (!pen || Math.Abs(py - prev) > h * 3) { ctx.BeginFigure(new Point(px, py), false, false); pen = true; }
                        else ctx.LineTo(new Point(px, py), true, true);
                        prev = py;
                    }
                }
                geo.Freeze();
                var lp = new Pen(brush, 2.2) { LineJoin = PenLineJoin.Round }; lp.Freeze();
                dc.DrawGeometry(null, lp, geo);
            }

            if (mouse.HasValue && !dragging)
            {
                double mx = Cx + (mouse.Value.X - w / 2) / Scale;
                string info = null;
                for (int i = 0; i < Funcs.Length; i++)
                {
                    if (Funcs[i] == null) continue;
                    double y = Safe(Funcs[i], mx);
                    if (double.IsNaN(y) || double.IsInfinity(y)) continue;
                    double py = h / 2 - (y - Cy) * Scale;
                    var brush = new SolidColorBrush(cols[i % cols.Length]);
                    dc.DrawEllipse(brush, new Pen(bg, 2), new Point(mouse.Value.X, py), 4.5, 4.5);
                    if (info == null) info = "x = " + Num(mx) + ",  y = " + Num(y);
                }
                if (Trace != null) Trace(info ?? "");
            }
        }

        public void Refresh() { InvalidateVisual(); }

        public void Reset() { Cx = 0; Cy = 0; Scale = 50; InvalidateVisual(); }
        public void ZoomBy(double factor) { Scale = Math.Max(0.5, Math.Min(20000, Scale * factor)); InvalidateVisual(); }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            dragging = true; dragStart = e.GetPosition(this); dragCx = Cx; dragCy = Cy; CaptureMouse(); base.OnMouseLeftButtonDown(e);
        }
        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e) { dragging = false; ReleaseMouseCapture(); InvalidateVisual(); base.OnMouseLeftButtonUp(e); }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            Point p = e.GetPosition(this);
            if (dragging) { Cx = dragCx - (p.X - dragStart.X) / Scale; Cy = dragCy + (p.Y - dragStart.Y) / Scale; }
            else mouse = p;
            InvalidateVisual(); base.OnMouseMove(e);
        }
        protected override void OnMouseLeave(MouseEventArgs e) { mouse = null; if (Trace != null) Trace(""); InvalidateVisual(); base.OnMouseLeave(e); }
        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            Point p = e.GetPosition(this);
            double w = ActualWidth, h = ActualHeight;
            double wx = Cx + (p.X - w / 2) / Scale, wy = Cy - (p.Y - h / 2) / Scale;
            Scale = Math.Max(0.5, Math.Min(20000, Scale * (e.Delta > 0 ? 1.15 : 1 / 1.15)));
            Cx = wx - (p.X - w / 2) / Scale; Cy = wy + (p.Y - h / 2) / Scale;
            InvalidateVisual(); e.Handled = true;
        }

        // Zeros and extrema of one equation inside the visible x range.
        public string Analyze(int index, double width)
        {
            Func<double, double> f = Funcs[index];
            if (f == null) return "";
            double x0 = Cx - width / 2 / Scale, x1 = Cx + width / 2 / Scale;
            const int N = 1600;
            var xs = new double[N + 1]; var ys = new double[N + 1];
            for (int k = 0; k <= N; k++) { xs[k] = x0 + (x1 - x0) * k / N; ys[k] = Safe(f, xs[k]); }
            var zeros = new List<string>(); var maxs = new List<string>(); var mins = new List<string>();
            for (int k = 0; k < N; k++)
            {
                double a = ys[k], b = ys[k + 1];
                if (double.IsNaN(a) || double.IsNaN(b) || double.IsInfinity(a) || double.IsInfinity(b)) continue;
                if (a == 0 || a * b < 0)
                {
                    double lo = xs[k], hi = xs[k + 1];
                    for (int it = 0; it < 50; it++)
                    {
                        double mid = (lo + hi) / 2, fm = Safe(f, mid);
                        if (double.IsNaN(fm)) break;
                        if ((Safe(f, lo) < 0) == (fm < 0)) lo = mid; else hi = mid;
                    }
                    double r = (lo + hi) / 2;
                    if (Math.Abs(Safe(f, r)) < 1e-3 * Math.Max(1, Math.Abs(a) + Math.Abs(b)) && zeros.Count < 8) zeros.Add(Num(Math.Round(r, 6)));
                }
            }
            for (int k = 1; k < N; k++)
            {
                double p = ys[k - 1], c = ys[k], n = ys[k + 1];
                if (double.IsNaN(p) || double.IsNaN(c) || double.IsNaN(n) || double.IsInfinity(c)) continue;
                if (c > p && c >= n && maxs.Count < 6) maxs.Add("(" + Num(Math.Round(xs[k], 4)) + ", " + Num(Math.Round(c, 4)) + ")");
                else if (c < p && c <= n && mins.Count < 6) mins.Add("(" + Num(Math.Round(xs[k], 4)) + ", " + Num(Math.Round(c, 4)) + ")");
            }
            var sb = new StringBuilder();
            sb.Append(Loc.F("Zeros: {0}", zeros.Count == 0 ? Loc.T("none in view") : string.Join(", ", zeros.ToArray())) + "\n");
            sb.Append(Loc.F("Maxima: {0}", maxs.Count == 0 ? Loc.T("none in view") : string.Join("  ", maxs.ToArray())) + "\n");
            sb.Append(Loc.F("Minima: {0}", mins.Count == 0 ? Loc.T("none in view") : string.Join("  ", mins.ToArray())));
            return sb.ToString();
        }
    }

    public class GraphView : IModeView
    {
        readonly Session ses;
        readonly Grid root = new Grid { Margin = new Thickness(4) };
        readonly GraphCanvas canvas = new GraphCanvas();
        readonly TextBox[] boxes = new TextBox[4];
        readonly TextBlock[] errors = new TextBlock[4];
        readonly Border[] swatches = new Border[4];
        readonly TextBlock traceText, analysis;
        readonly bool[] valid = new bool[4];

        public UIElement Root { get { return root; } }
        public double PreferredWidth { get { return 900; } }
        public double PreferredHeight { get { return 620; } }
        public bool HasHistory { get { return false; } }
        public GraphCanvas Canvas { get { return canvas; } }

        public GraphView(Session s)
        {
            ses = s;
            canvas.Theme = delegate { return ses.Theme; };
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(270) });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var left = new StackPanel { Margin = new Thickness(0, 0, 10, 0) };
            left.Children.Add(Ui.Label(Loc.T("Equations  y ="), "SubText", 13));
            for (int i = 0; i < 4; i++)
            {
                int idx = i;
                var row = new Grid { Margin = new Thickness(0, 4, 0, 0) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                swatches[i] = new Border { Width = 12, Height = 12, CornerRadius = new CornerRadius(6), Margin = new Thickness(2, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
                boxes[i] = new TextBox { FontSize = 15, MaxLength = 300 };
                boxes[i].TextChanged += delegate { Compile(idx); };
                Grid.SetColumn(boxes[i], 1);
                row.Children.Add(swatches[i]); row.Children.Add(boxes[i]);
                left.Children.Add(row);
                errors[i] = Ui.Label("", "SubText", 11); errors[i].Margin = new Thickness(22, 1, 0, 0); errors[i].Visibility = Visibility.Collapsed;
                left.Children.Add(errors[i]);
            }
            var btns = new UniformGrid { Columns = 3, Margin = new Thickness(0, 10, 0, 0) };
            btns.Children.Add(Ui.Key("Zoom +", "func", delegate { canvas.ZoomBy(1.3); }, 13));
            btns.Children.Add(Ui.Key("Zoom −", "func", delegate { canvas.ZoomBy(1 / 1.3); }, 13));
            btns.Children.Add(Ui.Key(Loc.T("Reset"), "func", delegate { canvas.Reset(); }, 13));
            left.Children.Add(btns);
            var an = Ui.Key(Loc.T("Analyze first equation"), "accent", Analyze, 13); an.Height = 34;
            left.Children.Add(an);
            analysis = Ui.Label("", "Text", 12); analysis.TextWrapping = TextWrapping.Wrap; analysis.Margin = new Thickness(4, 8, 4, 0);
            left.Children.Add(analysis);
            traceText = Ui.Label(Loc.T(Loc.T("Drag to pan, wheel to zoom, hover to trace.")), "SubText", 12);
            traceText.TextWrapping = TextWrapping.Wrap; traceText.Margin = new Thickness(4, 10, 4, 0);
            left.Children.Add(traceText);
            root.Children.Add(left);

            var host = new Border { CornerRadius = new CornerRadius(Ui.R(12)), BorderThickness = new Thickness(1), ClipToBounds = true, Child = canvas };
            host.SetResourceReference(Border.BorderBrushProperty, "Border");
            Grid.SetColumn(host, 1); root.Children.Add(host);
            canvas.Trace += delegate(string t) { traceText.Text = t.Length > 0 ? t : Loc.T(Loc.T("Drag to pan, wheel to zoom, hover to trace.")); };
            ses.ThemeChanged += OnTheme;
            boxes[0].Text = "sin(x)";
            OnTheme();
        }

        void OnTheme()
        {
            Color[] cols = ThemeManager.CurveColors(ses.Theme);
            for (int i = 0; i < 4; i++) swatches[i].Background = new SolidColorBrush(cols[i % cols.Length]);
            canvas.Refresh();
        }

        void Compile(int i)
        {
            string t = boxes[i].Text.Trim();
            valid[i] = false;
            if (t.Length == 0) { canvas.Funcs[i] = null; errors[i].Visibility = Visibility.Collapsed; canvas.Refresh(); return; }
            try { canvas.Funcs[i] = Expr.Compile(t, AngleMode.Radians); valid[i] = true; errors[i].Visibility = Visibility.Collapsed; }
            catch (CalcException ex) { canvas.Funcs[i] = null; errors[i].Text = ex.Message; errors[i].Visibility = Visibility.Visible; }
            canvas.Refresh();
        }

        public void SetEquation(int i, string text) { boxes[i].Text = text; }

        void Analyze()
        {
            for (int i = 0; i < 4; i++)
            {
                if (canvas.Funcs[i] != null) { analysis.Text = canvas.Analyze(i, Math.Max(100, canvas.ActualWidth > 0 ? canvas.ActualWidth : 600)); return; }
            }
            analysis.Text = Loc.T("Enter an equation first.");
        }

        public string AnalyzeText(int i, double width) { return canvas.Analyze(i, width); }

        public bool HandleText(string t) { return false; }
        public bool HandleKey(Key key) { return false; }
        public string CopyText() { return boxes[0].Text; }
        public void Paste(string text) { }
        public void Recall(string value) { }
    }
}
