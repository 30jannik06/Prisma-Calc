// Prisma - calculator views: Standard, Scientific, Programmer. C# 5.
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
    static class DisplayKit
    {
        public static StackPanel Make(out TextBlock small, out TextBlock big)
        {
            var sp = new StackPanel { Margin = new Thickness(6, 4, 6, 8) };
            small = Ui.Label("", "SubText", 14);
            small.HorizontalAlignment = HorizontalAlignment.Right;
            small.TextTrimming = TextTrimming.CharacterEllipsis;
            small.MinHeight = 20;
            big = Ui.Label("0", "Text", 44);
            big.HorizontalAlignment = HorizontalAlignment.Right;
            big.FontWeight = FontWeights.Light;
            big.TextTrimming = TextTrimming.CharacterEllipsis;
            sp.Children.Add(small); sp.Children.Add(big);
            return sp;
        }

        public static void Fit(TextBlock big, string text)
        {
            big.Text = Loc.Num(text);
            big.FontSize = text.Length > 18 ? 22 : text.Length > 14 ? 28 : text.Length > 10 ? 36 : 44;
        }

        public static UniformGrid MemoryRow(Action<string> press)
        {
            var g = new UniformGrid { Columns = 5, Margin = new Thickness(0, 0, 0, 2) };
            foreach (string k in new[] { "MC", "MR", "M+", "M−", "MS" })
            {
                string key = k == "M−" ? "M-" : k;
                g.Children.Add(Ui.Key(k, "flat", delegate { press(key); }, 13));
            }
            return g;
        }

        // Shared memory logic. value = current number; returns a number for MR or null.
        public static double? Memory(Session ses, string key, double value)
        {
            switch (key)
            {
                case "MC": ses.Memory.Clear(); break;
                case "MS": ses.Memory.Insert(0, value); if (ses.Memory.Count > 100) ses.Memory.RemoveAt(ses.Memory.Count - 1); break;
                case "M+": if (ses.Memory.Count == 0) ses.Memory.Add(value); else ses.Memory[0] += value; break;
                case "M-": if (ses.Memory.Count == 0) ses.Memory.Add(-value); else ses.Memory[0] -= value; break;
                case "MR": ses.Raise(); return ses.Memory.Count > 0 ? (double?)ses.Memory[0] : null;
            }
            ses.Raise();
            return null;
        }
    }

    // -------------------------------------------------------------- Standard
    public class StandardView : IModeView
    {
        readonly Session ses;
        readonly CalcEngine e = new CalcEngine();
        readonly TextBlock expr, result;
        readonly Grid root = new Grid();

        public UIElement Root { get { return root; } }
        public double PreferredWidth { get { return 430; } }
        public double PreferredHeight { get { return 600; } }
        public bool HasHistory { get { return true; } }

        public StandardView(Session s)
        {
            ses = s;
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            StackPanel disp = DisplayKit.Make(out expr, out result);
            Grid.SetRow(disp, 0); root.Children.Add(disp);
            UniformGrid mem = DisplayKit.MemoryRow(Mem);
            Grid.SetRow(mem, 1); root.Children.Add(mem);

            string[,] keys = {
                { "%", "pct", "func" }, { "CE", "CE", "func" }, { "C", "C", "func" }, { "⌫", "BK", "func" },
                { "1/x", "inv", "func" }, { "x²", "sqr", "func" }, { "√", "sqrt", "func" }, { "÷", "/", "func" },
                { "7", "7", "num" }, { "8", "8", "num" }, { "9", "9", "num" }, { "×", "*", "func" },
                { "4", "4", "num" }, { "5", "5", "num" }, { "6", "6", "num" }, { "−", "-", "func" },
                { "1", "1", "num" }, { "2", "2", "num" }, { "3", "3", "num" }, { "+", "+", "func" },
                { "±", "neg", "func" }, { "0", "0", "num" }, { ".", ".", "num" }, { "=", "=", "accent" } };
            var pad = new UniformGrid { Columns = 4 };
            for (int i = 0; i < keys.GetLength(0); i++)
            {
                string key = keys[i, 1];
                pad.Children.Add(Ui.Key(keys[i, 0], keys[i, 2], delegate { Press(key); }, 20));
            }
            Grid.SetRow(pad, 2); root.Children.Add(pad);
            Refresh();
        }

        void Mem(string key)
        {
            double? r = DisplayKit.Memory(ses, key, (double)e.Value);
            if (r.HasValue) { try { e.SetValue((decimal)r.Value); } catch (OverflowException) { } }
            Refresh();
        }

        public void Press(string k)
        {
            switch (k)
            {
                case "CE": e.ClearEntry(); break;
                case "C": e.Clear(); break;
                case "BK": e.Back(); break;
                case "neg": e.Negate(); break;
                case ".": e.Dot(); break;
                case "=": e.Equals(); break;
                case "pct": e.Percent(); break;
                case "inv": e.Reciprocal(); break;
                case "sqr": e.Square(); break;
                case "sqrt": e.Sqrt(); break;
                case "+": case "-": case "*": case "/": e.Operator(k[0]); break;
                default: if (k.Length == 1 && char.IsDigit(k[0])) e.Digit(k[0]); break;
            }
            Refresh();
        }

        void Refresh()
        {
            if (e.HistExpr != null) { ses.AddHistory(e.HistExpr, e.HistResult); e.HistExpr = null; e.HistResult = null; }
            DisplayKit.Fit(result, e.Entry);
            expr.Text = Loc.Num(e.Expr);
        }

        public string ResultText { get { return result.Text; } }
        public string ExprText { get { return expr.Text; } }

        public bool HandleText(string t)
        {
            if (string.IsNullOrEmpty(t)) return false;
            char c = t[0];
            if (char.IsDigit(c) || c == '+' || c == '-' || c == '*' || c == '/') Press(c.ToString());
            else if (c == '.' || c == ',') Press(".");
            else if (c == '=') Press("=");
            else if (c == '%') Press("pct");
            else return false;
            return true;
        }

        public bool HandleKey(Key key)
        {
            if (key == Key.Enter) Press("=");
            else if (key == Key.Escape) Press("C");
            else if (key == Key.Back) Press("BK");
            else if (key == Key.Delete) Press("CE");
            else return false;
            return true;
        }

        public string CopyText() { return e.Entry; }

        public void Paste(string text)
        {
            double v;
            if (text == null || text.Length > 64 || !Ui.TryNumber(text, out v)) return;
            try { e.SetValue((decimal)v); } catch (OverflowException) { return; }
            Refresh();
        }

        public void Recall(string value) { Paste(value); }
    }

    // ------------------------------------------------------------ Scientific
    public class ScientificView : IModeView
    {
        readonly Session ses;
        readonly Grid root = new Grid();
        readonly TextBlock small, big;
        readonly Button angleBtn;
        AngleMode am = AngleMode.Degrees;
        string expr = "", lastResult = "0", prevExpr = "", errorText;
        bool done;

        public UIElement Root { get { return root; } }
        public double PreferredWidth { get { return 460; } }
        public double PreferredHeight { get { return 780; } }
        public bool HasHistory { get { return true; } }
        public string ResultText { get { return big.Text; } }

        public ScientificView(Session s)
        {
            ses = s;
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            StackPanel disp = DisplayKit.Make(out small, out big);
            Grid.SetRow(disp, 0); root.Children.Add(disp);
            UniformGrid mem = DisplayKit.MemoryRow(Mem);
            Grid.SetRow(mem, 1); root.Children.Add(mem);

            angleBtn = Ui.Key("DEG", "func", CycleAngle, 15);
            var pad = new UniformGrid { Columns = 5 };
            pad.Children.Add(angleBtn);
            pad.Children.Add(K("π", "num", delegate { InsertConst("pi"); }));
            pad.Children.Add(K("e", "num", delegate { InsertConst("e"); }));
            pad.Children.Add(K("C", "func", Clear));
            pad.Children.Add(K("⌫", "func", Back));
            Fn(pad, "sin", "sin("); Fn(pad, "cos", "cos("); Fn(pad, "tan", "tan(");
            pad.Children.Add(K("(", "func", delegate { Insert("("); })); pad.Children.Add(K(")", "func", delegate { Insert(")"); }));
            Fn(pad, "asin", "asin("); Fn(pad, "acos", "acos("); Fn(pad, "atan", "atan(");
            pad.Children.Add(K("x²", "func", delegate { Insert("^2"); })); pad.Children.Add(K("√", "func", delegate { Insert("sqrt("); }));
            Fn(pad, "sinh", "sinh("); Fn(pad, "cosh", "cosh("); Fn(pad, "tanh", "tanh(");
            pad.Children.Add(K("xʸ", "func", delegate { Insert("^"); })); pad.Children.Add(K("n!", "func", delegate { Insert("!"); }));
            Fn(pad, "ln", "ln("); Fn(pad, "log", "log(");
            pad.Children.Add(K("eˣ", "func", delegate { Insert("exp("); })); pad.Children.Add(K("10ˣ", "func", delegate { Insert("10^"); }));
            pad.Children.Add(K("mod", "func", delegate { Insert(" mod "); }));
            Digits(pad, "7", "8", "9"); pad.Children.Add(K("÷", "func", delegate { Insert("/"); }));
            pad.Children.Add(K("1/x", "func", delegate { Wrap("1/(", ")"); }));
            Digits(pad, "4", "5", "6"); pad.Children.Add(K("×", "func", delegate { Insert("*"); }));
            pad.Children.Add(K("|x|", "func", delegate { Wrap("abs(", ")"); }));
            Digits(pad, "1", "2", "3"); pad.Children.Add(K("−", "func", delegate { Insert("-"); }));
            pad.Children.Add(K("∛", "func", delegate { Insert("cbrt("); }));
            pad.Children.Add(K("±", "func", Negate));
            Digits(pad, "0"); pad.Children.Add(K(".", "num", delegate { Insert("."); }));
            pad.Children.Add(K("=", "accent", Evaluate)); pad.Children.Add(K("+", "func", delegate { Insert("+"); }));
            Grid.SetRow(pad, 2); root.Children.Add(pad);
            Refresh();
        }

        Button K(string cap, string role, Action a) { return Ui.Key(cap, role, a, 17); }
        void Fn(UniformGrid pad, string cap, string tok) { pad.Children.Add(K(cap, "func", delegate { Insert(tok); })); }
        void Digits(UniformGrid pad, params string[] ds)
        {
            foreach (string d in ds) { string dd = d; pad.Children.Add(K(dd, "num", delegate { Insert(dd); })); }
        }

        void CycleAngle()
        {
            am = am == AngleMode.Degrees ? AngleMode.Radians : am == AngleMode.Radians ? AngleMode.Gradians : AngleMode.Degrees;
            angleBtn.Content = am == AngleMode.Degrees ? "DEG" : am == AngleMode.Radians ? "RAD" : "GRAD";
            Refresh();
        }

        public AngleMode Angle { get { return am; } }

        static bool StartsWithOperator(string tok)
        {
            string t = tok.TrimStart();
            return t.Length > 0 && ("+-*/^!".IndexOf(t[0]) >= 0 || t.StartsWith("mod"));
        }

        void Reset() { expr = ""; done = false; errorText = null; }

        public void Insert(string tok)
        {
            if (errorText != null) Reset();
            if (!done && expr.Length + tok.Length > Expr.MaxLength) return;
            if (done)
            {
                expr = StartsWithOperator(tok) ? lastResult : "";
                done = false;
            }
            expr += tok;
            Refresh();
        }

        void InsertConst(string name)
        {
            bool mult = !done && expr.Length > 0 && (char.IsDigit(expr[expr.Length - 1]) || expr[expr.Length - 1] == ')');
            Insert(mult ? "*" + name : name);
        }

        void Wrap(string pre, string post)
        {
            if (errorText != null) Reset();
            if (done) { expr = lastResult; done = false; }
            if (expr.Length == 0) return;
            expr = pre + expr + post;
            Refresh();
        }

        void Negate()
        {
            if (errorText != null) Reset();
            if (done) { expr = lastResult; done = false; }
            if (expr.Length == 0) return;
            expr = expr.StartsWith("-(") && expr.EndsWith(")") ? expr.Substring(2, expr.Length - 3) : "-(" + expr + ")";
            Refresh();
        }

        void Clear() { Reset(); prevExpr = ""; Refresh(); }

        void Back()
        {
            if (done || errorText != null) { Clear(); return; }
            if (expr.EndsWith(" mod ")) expr = expr.Substring(0, expr.Length - 5);
            else if (expr.EndsWith("("))
            {
                int k = expr.Length - 1;
                while (k > 0 && char.IsLetter(expr[k - 1])) k--;
                expr = expr.Substring(0, k == expr.Length - 1 ? k : k);
            }
            else if (expr.Length > 0) expr = expr.Substring(0, expr.Length - 1);
            Refresh();
        }

        public void Evaluate()
        {
            if (errorText != null || expr.Length == 0) return;
            try
            {
                double r = Expr.Eval(expr, am);
                lastResult = Expr.Format(r);
                prevExpr = expr + " =";
                ses.AddHistory(expr, lastResult);
                done = true;
            }
            catch (CalcException ex) { errorText = ex.Message; prevExpr = ""; }
            Refresh();
        }

        double CurrentValue()
        {
            try { return done ? Expr.Eval(lastResult, am) : Expr.Eval(expr, am); } catch (CalcException) { return 0; }
        }

        void Mem(string key)
        {
            double? r = DisplayKit.Memory(ses, key, CurrentValue());
            if (r.HasValue) { Reset(); lastResult = Expr.Format(r.Value); done = true; prevExpr = ""; }
            Refresh();
        }

        void Refresh()
        {
            if (errorText != null) { DisplayKit.Fit(big, errorText); small.Text = ""; return; }
            if (done) { DisplayKit.Fit(big, lastResult); small.Text = Loc.Num(prevExpr); return; }
            DisplayKit.Fit(big, expr.Length == 0 ? "0" : expr);
            string preview = "";
            if (expr.Length > 0)
            {
                try { preview = "= " + Expr.Format(Expr.Eval(expr, am)); } catch (CalcException) { }
            }
            small.Text = Loc.Num(preview);
        }

        public string ExprText { get { return expr; } }

        public bool HandleText(string t)
        {
            if (string.IsNullOrEmpty(t)) return false;
            char c = t[0];
            if (char.IsDigit(c) || "+-*/^()!".IndexOf(c) >= 0) Insert(c.ToString());
            else if (c == '.' || c == ',') Insert(".");
            else if (c == '=') Evaluate();
            else if (c == '%') Insert(" mod ");
            else return false;
            return true;
        }

        public bool HandleKey(Key key)
        {
            if (key == Key.Enter) Evaluate();
            else if (key == Key.Escape) Clear();
            else if (key == Key.Back) Back();
            else if (key == Key.Delete) Clear();
            else return false;
            return true;
        }

        public string CopyText() { return errorText != null ? "" : done ? lastResult : expr; }

        public void Paste(string text)
        {
            double v;
            if (text == null || text.Length > 64 || !Ui.TryNumber(text, out v)) return;
            Insert(Expr.Format(v));
        }

        public void Recall(string value) { Reset(); expr = value; Refresh(); }
    }

    // ------------------------------------------------------------ Programmer
    public class ProgrammerView : IModeView
    {
        readonly ProgEngine p = new ProgEngine();
        readonly Grid root = new Grid();
        readonly Dictionary<int, TextBlock> values = new Dictionary<int, TextBlock>();
        readonly Dictionary<int, TextBlock> labels = new Dictionary<int, TextBlock>();
        readonly Dictionary<string, Button> digitButtons = new Dictionary<string, Button>();
        readonly Button wordBtn;

        public UIElement Root { get { return root; } }
        public double PreferredWidth { get { return 470; } }
        public double PreferredHeight { get { return 690; } }
        public bool HasHistory { get { return false; } }
        public ProgEngine Engine { get { return p; } }

        public ProgrammerView()
        {
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var rows = new StackPanel { Margin = new Thickness(0, 0, 0, 6) };
            foreach (int b in new[] { 16, 10, 8, 2 })
            {
                int bb = b;
                var grid = new Grid { Margin = new Thickness(6, 3, 6, 3) };
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(48) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                TextBlock lab = Ui.Label(b == 16 ? "HEX" : b == 10 ? "DEC" : b == 8 ? "OCT" : "BIN", "SubText", 13);
                lab.VerticalAlignment = VerticalAlignment.Center; lab.FontWeight = FontWeights.SemiBold;
                TextBlock val = Ui.Label("0", "Text", b == 10 ? 30 : 20);
                val.HorizontalAlignment = HorizontalAlignment.Right; val.TextTrimming = TextTrimming.CharacterEllipsis;
                Grid.SetColumn(val, 1);
                grid.Children.Add(lab); grid.Children.Add(val);
                var btn = Ui.Key("", "flat", delegate { p.SetBase(bb); Refresh(); }, 14);
                btn.Content = grid; btn.HorizontalContentAlignment = HorizontalAlignment.Stretch;
                rows.Children.Add(btn);
                labels[b] = lab; values[b] = val;
            }
            Grid.SetRow(rows, 0); root.Children.Add(rows);

            var bar = new Grid { Margin = new Thickness(6, 0, 6, 4) };
            wordBtn = Ui.Key("QWORD", "func", CycleWord, 13);
            wordBtn.Width = 90; wordBtn.Height = 30; wordBtn.HorizontalAlignment = HorizontalAlignment.Left;
            bar.Children.Add(wordBtn);
            Grid.SetRow(bar, 1); root.Children.Add(bar);

            string[,] keys = {
                { "A", "A", "num" }, { "AND", "AND", "func" }, { "OR", "OR", "func" }, { "XOR", "XOR", "func" }, { "NOT", "NOT", "func" },
                { "B", "B", "num" }, { "NAND", "NAND", "func" }, { "NOR", "NOR", "func" }, { "<<", "<<", "func" }, { ">>", ">>", "func" },
                { "C", "C", "num" }, { "RoL", "RoL", "func" }, { "RoR", "RoR", "func" }, { "CE", "CE", "func" }, { "⌫", "BK", "func" },
                { "D", "D", "num" }, { "7", "7", "num" }, { "8", "8", "num" }, { "9", "9", "num" }, { "÷", "/", "func" },
                { "E", "E", "num" }, { "4", "4", "num" }, { "5", "5", "num" }, { "6", "6", "num" }, { "×", "*", "func" },
                { "F", "F", "num" }, { "1", "1", "num" }, { "2", "2", "num" }, { "3", "3", "num" }, { "−", "-", "func" },
                { "Mod", "mod", "func" }, { "0", "0", "num" }, { "±", "neg", "func" }, { "=", "=", "accent" }, { "+", "+", "func" } };
            var pad = new UniformGrid { Columns = 5 };
            for (int i = 0; i < keys.GetLength(0); i++)
            {
                string key = keys[i, 1];
                Button b = Ui.Key(keys[i, 0], keys[i, 2], delegate { Press(key); }, 16);
                if (key.Length == 1 && (char.IsDigit(key[0]) || (key[0] >= 'A' && key[0] <= 'F'))) digitButtons[key] = b;
                pad.Children.Add(b);
            }
            Grid.SetRow(pad, 2); root.Children.Add(pad);
            Refresh();
        }

        void CycleWord()
        {
            p.SetBits(p.Bits == 64 ? 32 : p.Bits == 32 ? 16 : p.Bits == 16 ? 8 : 64);
            Refresh();
        }

        public void Press(string k)
        {
            switch (k)
            {
                case "CLR": p.Clear(); break;   // not "C": that is the hex digit
                case "CE": p.ClearEntry(); break;
                case "BK": p.Back(); break;
                case "=": p.Equals(); break;
                case "neg": p.Negate(); break;
                case "NOT": p.Not(); break;
                case "RoL": p.RotateLeft(); break;
                case "RoR": p.RotateRight(); break;
                case "+": case "-": case "*": case "/": case "mod": case "AND": case "OR": case "XOR": case "NAND": case "NOR": case "<<": case ">>":
                    p.Operator(k); break;
                default: if (k.Length == 1) p.Digit(k[0]); break;
            }
            Refresh();
        }

        void Refresh()
        {
            foreach (int b in new[] { 16, 10, 8, 2 })
            {
                values[b].Text = p.Error ? (b == p.Base ? p.ErrorText : "") : p.FormatIn(b);
                labels[b].SetResourceReference(TextBlock.ForegroundProperty, b == p.Base ? "Brand" : "SubText");
            }
            wordBtn.Content = p.Bits == 64 ? "QWORD" : p.Bits == 32 ? "DWORD" : p.Bits == 16 ? "WORD" : "BYTE";
            foreach (KeyValuePair<string, Button> kv in digitButtons) kv.Value.IsEnabled = p.DigitAllowed(kv.Key[0]);
        }

        public string ValueIn(int b) { return values[b].Text; }

        public bool HandleText(string t)
        {
            if (string.IsNullOrEmpty(t)) return false;
            char c = char.ToUpperInvariant(t[0]);
            if (char.IsDigit(c) || (c >= 'A' && c <= 'F')) { Press(c.ToString()); return true; }
            switch (c)
            {
                case '+': case '-': case '*': case '/': Press(c.ToString()); return true;
                case '%': Press("mod"); return true;
                case '&': Press("AND"); return true;
                case '|': Press("OR"); return true;
                case '^': Press("XOR"); return true;
                case '~': Press("NOT"); return true;
                case '=': Press("="); return true;
            }
            return false;
        }

        public bool HandleKey(Key key)
        {
            if (key == Key.Enter) Press("=");
            else if (key == Key.Escape) Press("CLR");
            else if (key == Key.Back) Press("BK");
            else if (key == Key.Delete) Press("CE");
            else return false;
            return true;
        }

        public string CopyText() { return p.Error ? "" : p.FormatIn(p.Base).Replace(" ", ""); }

        public void Paste(string text)
        {
            if (text == null || text.Length > 80) return;
            string t = text.Trim().Replace(" ", "").ToUpperInvariant();
            bool neg = t.StartsWith("-");
            if (neg) t = t.Substring(1);
            if (t.Length == 0) return;
            p.Clear();
            foreach (char c in t) { if (!p.DigitAllowed(c)) { p.Clear(); Refresh(); return; } p.Digit(c); }
            if (neg) p.Negate();
            Refresh();
        }

        public void Recall(string value) { Paste(value); }
    }
}
