// Prisma - calculation engines (no UI): expression parser, standard, programmer, date, units. C# 5.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Prisma
{
    public enum AngleMode { Degrees, Radians, Gradians }

    public class CalcException : Exception
    {
        public CalcException(string message) : base(message) { }
    }

    // ------------------------------------------------------ expression parser
    // Compiles text such as "2(3+x)^2 + sin(30)" into a delegate. x is the only variable.
    public static class Expr
    {
        public const int MaxLength = 1000;   // longest accepted expression
        public const int MaxDepth = 100;     // deepest nesting of parentheses and function calls

        public static Func<double, double> Compile(string text, AngleMode am)
        {
            if (text == null) throw new CalcException(Loc.T("Empty expression"));
            if (text.Length > MaxLength) throw new CalcException(Loc.T("Expression too long"));
            string t = text.Replace('×', '*').Replace('÷', '/').Replace('−', '-').Replace("π", "pi").Replace("√", "sqrt").Replace(',', '.');
            int open = 0;
            foreach (char c in t) { if (c == '(') open++; else if (c == ')') open--; }
            if (open < 0) throw new CalcException(Loc.T("Unbalanced parentheses"));
            t = t + new string(')', open);
            if (t.Trim().Length == 0) throw new CalcException(Loc.T("Empty expression"));
            var p = new ExprParser(t, am);
            return p.ParseAll();
        }

        public static double Eval(string text, AngleMode am)
        {
            double r = Compile(text, am)(0);
            if (double.IsNaN(r)) throw new CalcException(Loc.T("Undefined result"));
            if (double.IsInfinity(r)) throw new CalcException(Loc.T("Result is not finite"));
            return r;
        }

        public static string Format(double v)
        {
            if (Math.Abs(v) < 1e-14) return "0";
            string s = v.ToString("G15", CultureInfo.InvariantCulture);
            return s.Replace("E+", "e+").Replace("E-", "e-");
        }
    }

    class ExprParser
    {
        readonly string s;
        int i;
        readonly AngleMode am;
        readonly double f; // radians per angle unit
        int depth;

        public ExprParser(string text, AngleMode mode)
        {
            s = text; am = mode;
            f = mode == AngleMode.Degrees ? Math.PI / 180.0 : mode == AngleMode.Gradians ? Math.PI / 200.0 : 1.0;
        }

        public Func<double, double> ParseAll()
        {
            Func<double, double> r = ParseSum();
            Skip();
            if (i < s.Length) throw new CalcException(Loc.F("Unexpected '{0}'", s[i]));
            return r;
        }

        void Skip() { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }
        bool Peek(char c) { Skip(); return i < s.Length && s[i] == c; }

        bool PeekWord(string w)
        {
            Skip();
            if (string.Compare(s, i, w, 0, w.Length, StringComparison.OrdinalIgnoreCase) != 0) return false;
            int e = i + w.Length;
            return e >= s.Length || !char.IsLetter(s[e]);
        }

        Func<double, double> ParseSum()
        {
            Func<double, double> left = ParseTerm();
            while (true)
            {
                if (Peek('+')) { i++; Func<double, double> a = left, b = ParseTerm(); left = x => a(x) + b(x); }
                else if (Peek('-')) { i++; Func<double, double> a = left, b = ParseTerm(); left = x => a(x) - b(x); }
                else return left;
            }
        }

        bool StartsPrimary()
        {
            Skip();
            if (i >= s.Length) return false;
            char c = s[i];
            return char.IsDigit(c) || c == '.' || c == '(' || char.IsLetter(c);
        }

        Func<double, double> ParseTerm()
        {
            Func<double, double> left = ParseUnary();
            while (true)
            {
                if (Peek('*')) { i++; Func<double, double> a = left, b = ParseUnary(); left = x => a(x) * b(x); }
                else if (Peek('/')) { i++; Func<double, double> a = left, b = ParseUnary(); left = x => a(x) / b(x); }
                else if (PeekWord("mod")) { i += 3; Func<double, double> a = left, b = ParseUnary(); left = x => { double d = b(x); return d == 0 ? double.NaN : a(x) % d; }; }
                else if (StartsPrimary()) { Func<double, double> a = left, b = ParsePower(); left = x => a(x) * b(x); }
                else return left;
            }
        }

        int unary;

        Func<double, double> ParseUnary()
        {
            if (++unary > 200) throw new CalcException(Loc.T("Expression too complex"));
            try { return ParseUnaryCore(); } finally { unary--; }
        }

        Func<double, double> ParseUnaryCore()
        {
            if (Peek('-')) { i++; Func<double, double> a = ParseUnary(); return x => -a(x); }
            if (Peek('+')) { i++; return ParseUnary(); }
            return ParsePower();
        }

        Func<double, double> ParsePower()
        {
            Func<double, double> b = ParsePostfix();
            if (Peek('^'))
            {
                i++;
                Func<double, double> e = ParseUnary();
                return x => Math.Pow(b(x), e(x));
            }
            return b;
        }

        Func<double, double> ParsePostfix()
        {
            Func<double, double> p = ParsePrimary();
            while (Peek('!'))
            {
                i++;
                Func<double, double> a = p;
                p = x =>
                {
                    double v = a(x);
                    if (v < 0 || v > 170 || Math.Abs(v - Math.Round(v)) > 1e-12) return double.NaN;
                    double r = 1;
                    for (int k = 2; k <= (int)Math.Round(v); k++) r *= k;
                    return r;
                };
            }
            return p;
        }

        Func<double, double> ParsePrimary()
        {
            Skip();
            if (i >= s.Length) throw new CalcException(Loc.T("Incomplete expression"));
            char c = s[i];
            if (char.IsDigit(c) || c == '.') return ParseNumber();
            if (c == '(')
            {
                i++;
                if (++depth > Expr.MaxDepth) throw new CalcException(Loc.T("Expression too complex"));
                Func<double, double> inner = ParseSum();
                if (!Peek(')')) throw new CalcException(Loc.T("Missing )"));
                i++;
                depth--;
                return inner;
            }
            if (char.IsLetter(c))
            {
                int st = i;
                while (i < s.Length && (char.IsLetter(s[i]) || char.IsDigit(s[i]))) i++;
                string name = s.Substring(st, i - st).ToLowerInvariant();
                if (name == "x") return x => x;
                if (name == "pi") return x => Math.PI;
                if (name == "e") return x => Math.E;
                if (!Peek('(')) throw new CalcException(Loc.F("Unknown name '{0}'", name));
                i++;
                if (++depth > Expr.MaxDepth) throw new CalcException(Loc.T("Expression too complex"));
                Func<double, double> arg = ParseSum();
                if (!Peek(')')) throw new CalcException(Loc.T("Missing )"));
                i++;
                depth--;
                return Function(name, arg);
            }
            throw new CalcException(Loc.F("Unexpected '{0}'", c));
        }

        Func<double, double> ParseNumber()
        {
            int st = i;
            while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '.')) i++;
            if (i < s.Length && (s[i] == 'e' || s[i] == 'E'))
            {
                int j = i + 1;
                if (j < s.Length && (s[j] == '+' || s[j] == '-')) j++;
                if (j < s.Length && char.IsDigit(s[j])) { i = j; while (i < s.Length && char.IsDigit(s[i])) i++; }
            }
            double v;
            if (!double.TryParse(s.Substring(st, i - st), NumberStyles.Float, CultureInfo.InvariantCulture, out v))
                throw new CalcException(Loc.F("Bad number '{0}'", s.Substring(st, i - st)));
            return x => v;
        }

        static double Clean(double r) { return Math.Abs(r) < 1e-14 ? 0.0 : Math.Round(r, 13); }

        Func<double, double> Function(string name, Func<double, double> a)
        {
            switch (name)
            {
                case "sin": return x => Clean(Math.Sin(a(x) * f));
                case "cos": return x => Clean(Math.Cos(a(x) * f));
                case "tan": return x => { double r = a(x) * f; return Math.Abs(Math.Cos(r)) < 1e-12 ? double.NaN : Clean(Math.Tan(r)); };
                case "asin": return x => Clean(Math.Asin(a(x)) / f);
                case "acos": return x => Clean(Math.Acos(a(x)) / f);
                case "atan": return x => Clean(Math.Atan(a(x)) / f);
                case "sinh": return x => Math.Sinh(a(x));
                case "cosh": return x => Math.Cosh(a(x));
                case "tanh": return x => Math.Tanh(a(x));
                case "asinh": return x => { double v = a(x); return Math.Log(v + Math.Sqrt(v * v + 1)); };
                case "acosh": return x => { double v = a(x); return v < 1 ? double.NaN : Math.Log(v + Math.Sqrt(v * v - 1)); };
                case "atanh": return x => { double v = a(x); return Math.Abs(v) >= 1 ? double.NaN : 0.5 * Math.Log((1 + v) / (1 - v)); };
                case "ln": return x => { double v = a(x); return v <= 0 ? double.NaN : Math.Log(v); };
                case "log": return x => { double v = a(x); return v <= 0 ? double.NaN : Math.Log10(v); };
                case "log2": return x => { double v = a(x); return v <= 0 ? double.NaN : Math.Log(v, 2); };
                case "sqrt": return x => { double v = a(x); return v < 0 ? double.NaN : Math.Sqrt(v); };
                case "cbrt": return x => { double v = a(x); return v < 0 ? -Math.Pow(-v, 1.0 / 3.0) : Math.Pow(v, 1.0 / 3.0); };
                case "abs": return x => Math.Abs(a(x));
                case "exp": return x => Math.Exp(a(x));
                case "floor": return x => Math.Floor(a(x));
                case "ceil": return x => Math.Ceiling(a(x));
                case "round": return x => Math.Round(a(x), MidpointRounding.AwayFromZero);
                case "sign": return x => Math.Sign(a(x));
                default: throw new CalcException(Loc.F("Unknown function '{0}'", name));
            }
        }
    }

    // --------------------------------------------------------- standard engine
    public class CalcEngine
    {
        public string Entry = "0";
        public string Expr = "";
        public bool Error;
        public string HistExpr, HistResult; // set when a calculation finished; the view takes and clears them
        decimal acc;
        char op;
        bool fresh, afterOp;

        static string Fmt(decimal d) { return d.ToString("0.#############################", CultureInfo.InvariantCulture); }
        static decimal Parse(string s) { return decimal.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture); }
        public decimal Value { get { return Error ? 0m : Parse(Entry); } }

        public void Clear() { Entry = "0"; Expr = ""; Error = false; acc = 0; op = '\0'; fresh = false; afterOp = false; }
        public void ClearEntry() { if (Error) { Clear(); return; } Entry = "0"; fresh = false; }

        public void SetValue(decimal v) { Error = false; Entry = Fmt(v); fresh = true; afterOp = false; }

        public void Digit(char c)
        {
            if (Error) Clear();
            if (fresh) { Entry = "0"; fresh = false; if (op == '\0') Expr = ""; }
            afterOp = false;
            if (Entry == "0") Entry = c.ToString();
            else if (Entry == "-0") Entry = "-" + c;
            else if (Entry.Replace("-", "").Replace(".", "").Length < 16) Entry += c;
        }

        public void Dot()
        {
            if (Error) Clear();
            if (fresh) { Entry = "0"; fresh = false; if (op == '\0') Expr = ""; }
            afterOp = false;
            if (Entry.IndexOf('.') < 0) Entry += ".";
        }

        public void Negate()
        {
            if (Error || Entry == "0") return;
            Entry = Entry.StartsWith("-") ? Entry.Substring(1) : "-" + Entry;
        }

        public void Back()
        {
            if (Error) { Clear(); return; }
            if (fresh) return;
            Entry = Entry.Substring(0, Entry.Length - 1);
            if (Entry.Length == 0 || Entry == "-") Entry = "0";
        }

        void Fail(string message) { Error = true; Entry = message; Expr = ""; op = '\0'; fresh = true; afterOp = false; }

        bool Apply(decimal a, decimal b, char o, out decimal r)
        {
            r = 0;
            try
            {
                switch (o)
                {
                    case '+': r = a + b; break;
                    case '-': r = a - b; break;
                    case '*': r = a * b; break;
                    case '/':
                        if (b == 0) { Fail(Loc.T("Division by zero")); return false; }
                        r = a / b; break;
                }
                return true;
            }
            catch (OverflowException) { Fail(Loc.T("Result too large")); return false; }
        }

        public static string Symbol(char o)
        {
            switch (o) { case '*': return "×"; case '/': return "÷"; case '-': return "−"; default: return "+"; }
        }

        public void Operator(char o)
        {
            if (Error) return;
            decimal cur = Parse(Entry);
            if (op != '\0' && afterOp) { op = o; Expr = Fmt(acc) + " " + Symbol(o); return; }
            if (op != '\0')
            {
                decimal r;
                if (!Apply(acc, cur, op, out r)) return;
                acc = r;
            }
            else acc = cur;
            op = o;
            Expr = Fmt(acc) + " " + Symbol(o);
            Entry = Fmt(acc);
            fresh = true; afterOp = true;
        }

        public void Equals()
        {
            if (Error) return;
            afterOp = false;
            if (op == '\0') { Expr = ""; fresh = true; return; }
            decimal cur = Parse(Entry), r;
            string left = Fmt(acc) + " " + Symbol(op) + " " + Fmt(cur);
            if (!Apply(acc, cur, op, out r)) return;
            Expr = left + " ="; Entry = Fmt(r); op = '\0'; fresh = true;
            HistExpr = left; HistResult = Entry;
        }

        void Unary(string label, Func<decimal, decimal> f)
        {
            if (Error) return;
            decimal cur = Parse(Entry);
            try
            {
                decimal r = f(cur);
                HistExpr = label.Replace("#", Fmt(cur)); HistResult = Fmt(r);
                Entry = Fmt(r); fresh = true; afterOp = false;
            }
            catch (CalcException ex) { Fail(ex.Message); }
            catch (OverflowException) { Fail(Loc.T("Result too large")); }
        }

        public void Sqrt()
        {
            Unary("√(#)", delegate(decimal d)
            {
                if (d < 0) throw new CalcException(Loc.T("Invalid input"));
                return (decimal)Math.Sqrt((double)d);
            });
        }
        public void Square() { Unary("sqr(#)", d => d * d); }
        public void Reciprocal()
        {
            Unary("1/(#)", delegate(decimal d)
            {
                if (d == 0) throw new CalcException(Loc.T("Division by zero"));
                return 1m / d;
            });
        }

        public void Percent()
        {
            if (Error) return;
            decimal cur = Parse(Entry);
            try
            {
                decimal r = op == '\0' ? 0m : (op == '+' || op == '-') ? acc * cur / 100m : cur / 100m;
                Entry = Fmt(r); fresh = true; afterOp = false;
            }
            catch (OverflowException) { Fail(Loc.T("Result too large")); }
        }
    }

    // ------------------------------------------------------- programmer engine
    public class ProgEngine
    {
        public int Bits = 64;
        public int Base = 10;
        public bool Error;
        public string ErrorText = "";
        long value, acc;
        string op, typed;
        bool fresh, afterOp;

        public long Value { get { return value; } }

        ulong Mask { get { return Bits == 64 ? ulong.MaxValue : (1UL << Bits) - 1UL; } }

        public long Normalize(long v)
        {
            if (Bits == 64) return v;
            ulong u = (ulong)v & Mask;
            if ((u & (1UL << (Bits - 1))) != 0) u |= ~Mask;
            return (long)u;
        }

        public ulong Unsigned { get { return (ulong)value & Mask; } }

        static string ToBase(ulong v, int b)
        {
            if (v == 0) return "0";
            const string digits = "0123456789ABCDEF";
            var sb = new StringBuilder();
            while (v > 0) { sb.Insert(0, digits[(int)(v % (ulong)b)]); v /= (ulong)b; }
            return sb.ToString();
        }

        public string FormatIn(int b)
        {
            if (b == 10) return value.ToString(CultureInfo.InvariantCulture);
            string s = ToBase(Unsigned, b);
            if (b == 2 || b == 16)
            {
                int g = 4;
                s = s.PadLeft((s.Length + g - 1) / g * g, '0').TrimStart('0');
                if (s.Length == 0) s = "0";
                var sb = new StringBuilder();
                for (int k = 0; k < s.Length; k++) { if (k > 0 && (s.Length - k) % g == 0) sb.Append(' '); sb.Append(s[k]); }
                s = sb.ToString();
            }
            return s;
        }

        public string Display { get { return Error ? ErrorText : FormatIn(Base); } }

        public bool DigitAllowed(char c)
        {
            int d = c >= '0' && c <= '9' ? c - '0' : c >= 'A' && c <= 'F' ? c - 'A' + 10 : 99;
            return d < Base;
        }

        public void Clear() { value = 0; acc = 0; op = null; typed = null; fresh = false; afterOp = false; Error = false; }
        public void ClearEntry() { if (Error) { Clear(); return; } value = 0; typed = null; fresh = false; }

        void Fail(string m) { Error = true; ErrorText = m; op = null; fresh = true; afterOp = false; }

        bool TryParseTyped(string t, out long v)
        {
            v = 0;
            bool neg = t.StartsWith("-");
            string digits = neg ? t.Substring(1) : t;
            ulong u = 0;
            foreach (char ch in digits)
            {
                int d = ch >= '0' && ch <= '9' ? ch - '0' : ch - 'A' + 10;
                if (u > (ulong.MaxValue - (ulong)d) / (ulong)Base) return false;
                u = u * (ulong)Base + (ulong)d;
            }
            if (Base == 10)
            {
                ulong max = Bits == 64 ? (ulong)long.MaxValue : (1UL << (Bits - 1)) - 1UL;
                if (u > max + (neg ? 1UL : 0UL)) return false;
                v = neg ? (long)(0UL - u) : (long)u;
                v = Normalize(v);
            }
            else
            {
                if (u > Mask) return false;
                v = Normalize((long)u);
            }
            return true;
        }

        public void Digit(char c)
        {
            if (Error) Clear();
            if (!DigitAllowed(c)) return;
            if (fresh) { typed = null; fresh = false; }
            afterOp = false;
            string cur = typed ?? "";
            string t = (cur == "0" ? "" : cur) + c;
            long v;
            if (!TryParseTyped(t, out v)) return;
            typed = t == "" ? "0" : t; value = v;
        }

        public void Back()
        {
            if (Error) { Clear(); return; }
            if (fresh) return;
            string t = typed;
            if (t == null)
            {
                string d = Base == 10 ? value.ToString(CultureInfo.InvariantCulture) : ToBase(Unsigned, Base);
                t = d;
            }
            t = t.Length > 0 ? t.Substring(0, t.Length - 1) : t;
            if (t.Length == 0 || t == "-") { typed = null; value = 0; return; }
            long v;
            if (TryParseTyped(t, out v)) { typed = t; value = v; }
        }

        public void SetBase(int b) { Base = b; typed = null; fresh = true; }
        public void SetBits(int n) { Bits = n; value = Normalize(value); acc = Normalize(acc); typed = null; }

        long Apply(long a, long b, string o)
        {
            switch (o)
            {
                case "+": return Normalize(unchecked(a + b));
                case "-": return Normalize(unchecked(a - b));
                case "*": return Normalize(unchecked(a * b));
                case "/":
                    if (b == 0) { Fail(Loc.T("Division by zero")); return 0; }
                    if (a == long.MinValue && b == -1) return Normalize(a);
                    return Normalize(a / b);
                case "mod":
                    if (b == 0) { Fail(Loc.T("Division by zero")); return 0; }
                    if (b == -1) return 0;
                    return Normalize(a % b);
                case "AND": return Normalize(a & b);
                case "OR": return Normalize(a | b);
                case "XOR": return Normalize(a ^ b);
                case "NAND": return Normalize(~(a & b));
                case "NOR": return Normalize(~(a | b));
                case "<<": return (b < 0 || b >= Bits) ? 0 : Normalize(a << (int)b);
                case ">>": return (b < 0 || b >= 64) ? (a < 0 ? -1 : 0) : Normalize(a >> (int)b);
            }
            return a;
        }

        public void Operator(string o)
        {
            if (Error) return;
            if (op != null && afterOp) { op = o; return; }
            if (op != null) { long r = Apply(acc, value, op); if (Error) return; acc = r; }
            else acc = value;
            op = o; value = acc; typed = null; fresh = true; afterOp = true;
        }

        public void Equals()
        {
            if (Error) return;
            afterOp = false;
            if (op == null) { fresh = true; return; }
            long r = Apply(acc, value, op);
            if (Error) return;
            value = r; op = null; typed = null; fresh = true;
        }

        public void Not() { if (Error) return; value = Normalize(~value); typed = null; fresh = true; afterOp = false; }
        public void Negate() { if (Error) return; value = Normalize(unchecked(-value)); typed = null; fresh = true; afterOp = false; }
        public void RotateLeft() { if (Error) return; ulong u = Unsigned; value = Normalize((long)(((u << 1) | (u >> (Bits - 1))) & Mask)); typed = null; fresh = true; afterOp = false; }
        public void RotateRight() { if (Error) return; ulong u = Unsigned; value = Normalize((long)(((u >> 1) | (u << (Bits - 1))) & Mask)); typed = null; fresh = true; afterOp = false; }
    }

    // ------------------------------------------------------------- dates
    public static class DateCalc
    {
        public class Diff { public int Years, Months, Days, TotalDays; }

        public static Diff Difference(DateTime a, DateTime b)
        {
            if (a > b) { DateTime t = a; a = b; b = t; }
            var d = new Diff();
            int y = b.Year - a.Year;
            if (a.AddYears(y) > b) y--;
            DateTime t1 = a.AddYears(y);
            int m = (b.Year - t1.Year) * 12 + b.Month - t1.Month;
            if (t1.AddMonths(m) > b) m--;
            DateTime t2 = t1.AddMonths(m);
            d.Years = y; d.Months = m; d.Days = (int)(b - t2).TotalDays; d.TotalDays = (int)(b - a).TotalDays;
            return d;
        }

        public static DateTime Add(DateTime d, int years, int months, int days, bool subtract)
        {
            int s = subtract ? -1 : 1;
            return d.AddYears(s * years).AddMonths(s * months).AddDays(s * days);
        }

        public static bool TryParse(string text, out DateTime d)
        {
            string[] formats = Loc.Lang == "en"
                ? new[] { "yyyy-MM-dd", "dd.MM.yyyy", "d.M.yyyy", "MM/dd/yyyy", "yyyy/MM/dd" }
                : new[] { "yyyy-MM-dd", "dd.MM.yyyy", "d.M.yyyy", "dd/MM/yyyy", "d/M/yyyy", "yyyy/MM/dd" };
            if (DateTime.TryParseExact((text ?? "").Trim(), formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out d)) return true;
            return DateTime.TryParse((text ?? "").Trim(), CultureInfo.CurrentCulture, DateTimeStyles.None, out d);
        }
    }

    // ------------------------------------------------------------- units
    public class Unit
    {
        public string Name, Abbr; public double Factor;
        public Unit(string name, string abbr, double factor) { Name = name; Abbr = abbr; Factor = factor; }
        public override string ToString() { return Loc.T(Name); }
    }

    public class Category
    {
        public string Name; public Unit[] Units; public bool Temperature; public bool Sample;
    }

    public static class Units
    {
        static Unit U(string n, string a, double f) { return new Unit(n, a, f); }

        public static Category Get(AppMode m)
        {
            var c = new Category { Name = m.ToString() };
            switch (m)
            {
                case AppMode.Volume: c.Units = new[] { U("Milliliter", "ml", 0.001), U("Liter", "l", 1), U("Cubic meter", "m³", 1000), U("Teaspoon (US)", "tsp", 0.00492892159), U("Tablespoon (US)", "tbsp", 0.0147867648), U("Fluid ounce (US)", "fl oz", 0.0295735296), U("Cup (US)", "cup", 0.236588237), U("Pint (US)", "pt", 0.473176473), U("Quart (US)", "qt", 0.946352946), U("Gallon (US)", "gal", 3.78541178), U("Cubic foot", "ft³", 28.3168466), U("Cubic inch", "in³", 0.016387064) }; break;
                case AppMode.Length: c.Units = new[] { U("Nanometer", "nm", 1e-9), U("Micrometer", "µm", 1e-6), U("Millimeter", "mm", 0.001), U("Centimeter", "cm", 0.01), U("Meter", "m", 1), U("Kilometer", "km", 1000), U("Inch", "in", 0.0254), U("Foot", "ft", 0.3048), U("Yard", "yd", 0.9144), U("Mile", "mi", 1609.344), U("Nautical mile", "nmi", 1852) }; break;
                case AppMode.Weight: c.Units = new[] { U("Milligram", "mg", 1e-6), U("Gram", "g", 0.001), U("Kilogram", "kg", 1), U("Tonne", "t", 1000), U("Ounce", "oz", 0.028349523125), U("Pound", "lb", 0.45359237), U("Stone", "st", 6.35029318), U("Short ton (US)", "tn", 907.18474) }; break;
                case AppMode.Temperature: c.Temperature = true; c.Units = new[] { U("Celsius", "°C", 1), U("Fahrenheit", "°F", 1), U("Kelvin", "K", 1) }; break;
                case AppMode.Energy: c.Units = new[] { U("Joule", "J", 1), U("Kilojoule", "kJ", 1000), U("Calorie (thermal)", "cal", 4.184), U("Kilocalorie", "kcal", 4184), U("Watt-hour", "Wh", 3600), U("Kilowatt-hour", "kWh", 3.6e6), U("Electronvolt", "eV", 1.602176634e-19), U("British thermal unit", "BTU", 1055.056), U("Foot-pound", "ft·lb", 1.3558179483) }; break;
                case AppMode.Area: c.Units = new[] { U("Square millimeter", "mm²", 1e-6), U("Square centimeter", "cm²", 1e-4), U("Square meter", "m²", 1), U("Hectare", "ha", 1e4), U("Square kilometer", "km²", 1e6), U("Square inch", "in²", 0.00064516), U("Square foot", "ft²", 0.09290304), U("Square yard", "yd²", 0.83612736), U("Acre", "ac", 4046.8564224), U("Square mile", "mi²", 2589988.110336) }; break;
                case AppMode.Speed: c.Units = new[] { U("Meter per second", "m/s", 1), U("Kilometer per hour", "km/h", 1.0 / 3.6), U("Mile per hour", "mph", 0.44704), U("Knot", "kn", 1852.0 / 3600.0), U("Foot per second", "ft/s", 0.3048), U("Mach", "Ma", 343) }; break;
                case AppMode.Time: c.Units = new[] { U("Microsecond", "µs", 1e-6), U("Millisecond", "ms", 1e-3), U("Second", "s", 1), U("Minute", "min", 60), U("Hour", "h", 3600), U("Day", "d", 86400), U("Week", "wk", 604800), U("Year (365 days)", "yr", 31536000) }; break;
                case AppMode.Power: c.Units = new[] { U("Watt", "W", 1), U("Kilowatt", "kW", 1000), U("Horsepower (mechanical)", "hp", 745.699872), U("Foot-pound per minute", "ft·lb/min", 0.0225969658), U("BTU per minute", "BTU/min", 17.5842642) }; break;
                case AppMode.Data: c.Units = new[] { U("Bit", "bit", 1), U("Byte", "B", 8), U("Kilobyte", "kB", 8e3), U("Kibibyte", "KiB", 8192), U("Megabyte", "MB", 8e6), U("Mebibyte", "MiB", 8388608), U("Gigabyte", "GB", 8e9), U("Gibibyte", "GiB", 8589934592.0), U("Terabyte", "TB", 8e12), U("Tebibyte", "TiB", 8796093022208.0) }; break;
                case AppMode.Pressure: c.Units = new[] { U("Pascal", "Pa", 1), U("Kilopascal", "kPa", 1000), U("Bar", "bar", 1e5), U("Atmosphere", "atm", 101325), U("Torr", "Torr", 133.322368), U("Pound per square inch", "psi", 6894.757293) }; break;
                case AppMode.Angle: c.Units = new[] { U("Degree", "°", 1), U("Radian", "rad", 180.0 / Math.PI), U("Gradian", "grad", 0.9) }; break;
                case AppMode.Currency:
                    c.Sample = true;
                    var rates = LoadRates();
                    var list = new List<Unit>();
                    foreach (KeyValuePair<string, double> kv in rates) list.Add(U(kv.Key, kv.Key, 1.0 / kv.Value));
                    c.Units = list.ToArray();
                    break;
                default: throw new ArgumentException("Not a converter: " + m);
            }
            return c;
        }

        public static string RatesPathOverride;
        public static string RatesPath() { return RatesPathOverride ?? Path.Combine(Log.DefaultDir(), "rates.ini"); }
        public const long MaxRatesFileBytes = 16 * 1024;

        // Example rates relative to 1 EUR. They are NOT live. Edit rates.ini (CODE=rate per EUR) to use your own.
        public static List<KeyValuePair<string, double>> LoadRates()
        {
            var d = new List<KeyValuePair<string, double>>
            {
                new KeyValuePair<string, double>("EUR", 1.0), new KeyValuePair<string, double>("USD", 1.08), new KeyValuePair<string, double>("GBP", 0.85),
                new KeyValuePair<string, double>("CHF", 0.96), new KeyValuePair<string, double>("JPY", 160.0), new KeyValuePair<string, double>("CAD", 1.47),
                new KeyValuePair<string, double>("AUD", 1.65), new KeyValuePair<string, double>("CNY", 7.8), new KeyValuePair<string, double>("SEK", 11.4),
                new KeyValuePair<string, double>("PLN", 4.3)
            };
            try
            {
                string p = RatesPath();
                var fi = new FileInfo(p);
                if (fi.Exists && fi.Length <= MaxRatesFileBytes)
                {
                    var custom = new List<KeyValuePair<string, double>>();
                    var seen = new HashSet<string>();
                    foreach (string line in File.ReadAllLines(p))
                    {
                        if (custom.Count >= 60) break;
                        int i = line.IndexOf('=');
                        double r;
                        if (i <= 0) continue;
                        string code = line.Substring(0, i).Trim().ToUpperInvariant();
                        bool codeOk = code.Length >= 2 && code.Length <= 5;
                        foreach (char ch in code) if (ch < 'A' || ch > 'Z') codeOk = false;
                        if (codeOk && seen.Add(code)
                            && double.TryParse(line.Substring(i + 1).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out r)
                            && r >= 1e-6 && r <= 1e9 && !double.IsNaN(r) && !double.IsInfinity(r))
                            custom.Add(new KeyValuePair<string, double>(code, r));
                    }
                    if (custom.Count >= 2) return custom;
                }
                else if (fi.Exists) Log.Warn("rates.ini is too large and was ignored");
            }
            catch (Exception ex) { Log.Warn("Could not read rates.ini: " + ex.Message); }
            return d;
        }

        static double ToCelsius(string abbr, double v) { return abbr == "°F" ? (v - 32.0) * 5.0 / 9.0 : abbr == "K" ? v - 273.15 : v; }
        static double FromCelsius(string abbr, double c) { return abbr == "°F" ? c * 9.0 / 5.0 + 32.0 : abbr == "K" ? c + 273.15 : c; }

        public static double Convert(Category c, double v, Unit from, Unit to)
        {
            if (c.Temperature) return FromCelsius(to.Abbr, ToCelsius(from.Abbr, v));
            return v * from.Factor / to.Factor;
        }

        public static string Format(double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) return "\u2014";
            if (v == 0) return "0";
            double a = Math.Abs(v);
            string s = (a >= 1e12 || a < 1e-6) ? v.ToString("0.#####E+0", CultureInfo.InvariantCulture) : v.ToString("G10", CultureInfo.InvariantCulture);
            return s;
        }
    }
}
