// Prisma - network calculations (pure maths on addresses, no network access): IPv4/IPv6 parsing and formatting,
// subnets, splitting, address conversion and transfer times. C# 5, no System.Net, no BigInteger.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Prisma
{
    public class IpAddr
    {
        public bool V6;
        public byte[] B;                       // 4 or 16 bytes, big-endian
        public int Bits { get { return V6 ? 128 : 32; } }
    }

    public class SubnetInfo
    {
        public IpAddr Ip;
        public int Prefix;
        public string Network, Broadcast, First, Last, Mask, Wildcard, Total, Usable, Type, Class;
        public List<KeyValuePair<string, string>> Rows = new List<KeyValuePair<string, string>>();
    }

    public static class NetCalc
    {
        public const int MaxInput = 80;

        static bool IsHex(char c) { return (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'); }
        static int HexVal(char c) { return c <= '9' ? c - '0' : (c | 0x20) - 'a' + 10; }

        // ------------------------------------------------------------ parsing
        public static bool TryParseV4(string s, out byte[] b)
        {
            b = null;
            if (s == null) return false;
            string[] p = s.Split('.');
            if (p.Length != 4) return false;
            var r = new byte[4];
            for (int i = 0; i < 4; i++)
            {
                string t = p[i];
                if (t.Length == 0 || t.Length > 3) return false;
                if (t.Length > 1 && t[0] == '0') return false;          // leading zeros are ambiguous (octal?)
                int v = 0;
                foreach (char c in t) { if (c < '0' || c > '9') return false; v = v * 10 + (c - '0'); }
                if (v > 255) return false;
                r[i] = (byte)v;
            }
            b = r;
            return true;
        }

        static bool Groups(string part, bool allowV4Last, List<int> into)
        {
            if (part.Length == 0) return true;
            string[] p = part.Split(':');
            for (int i = 0; i < p.Length; i++)
            {
                string t = p[i];
                if (t.Length == 0) return false;
                if (t.IndexOf('.') >= 0)
                {
                    byte[] v4;
                    if (!allowV4Last || i != p.Length - 1 || !TryParseV4(t, out v4)) return false;
                    into.Add(v4[0] << 8 | v4[1]); into.Add(v4[2] << 8 | v4[3]);
                }
                else
                {
                    if (t.Length > 4) return false;
                    int v = 0;
                    foreach (char c in t) { if (!IsHex(c)) return false; v = v * 16 + HexVal(c); }
                    into.Add(v);
                }
            }
            return true;
        }

        public static bool TryParseV6(string s, out byte[] b)
        {
            b = null;
            if (s == null) return false;
            s = s.Trim();
            if (s.Length >= 2 && s[0] == '[' && s[s.Length - 1] == ']') s = s.Substring(1, s.Length - 2);
            int pct = s.IndexOf('%');
            if (pct >= 0) s = s.Substring(0, pct);                          // a zone id (fe80::1%eth0) is ignored
            if (s.Length < 2 || s.Length > 45) return false;
            foreach (char c in s) if (!(IsHex(c) || c == ':' || c == '.')) return false;
            if (s.IndexOf(":::", StringComparison.Ordinal) >= 0) return false;
            int dc = s.IndexOf("::", StringComparison.Ordinal);
            if (dc >= 0 && s.IndexOf("::", dc + 2, StringComparison.Ordinal) >= 0) return false;
            string head = dc < 0 ? s : s.Substring(0, dc);
            string tail = dc < 0 ? "" : s.Substring(dc + 2);
            var hg = new List<int>(); var tg = new List<int>();
            if (!Groups(head, dc < 0, hg)) return false;
            if (dc >= 0 && !Groups(tail, true, tg)) return false;
            int total = hg.Count + tg.Count;
            if (dc < 0 ? total != 8 : total > 7) return false;
            var g = new int[8];
            for (int i = 0; i < hg.Count; i++) g[i] = hg[i];
            for (int j = 0; j < tg.Count; j++) g[8 - tg.Count + j] = tg[j];
            b = new byte[16];
            for (int k = 0; k < 8; k++) { b[2 * k] = (byte)(g[k] >> 8); b[2 * k + 1] = (byte)(g[k] & 0xFF); }
            return true;
        }

        public static bool TryParseIp(string s, out IpAddr ip)
        {
            ip = null;
            if (s == null) return false;
            s = s.Trim();
            if (s.Length == 0 || s.Length > MaxInput) return false;
            byte[] b;
            if (s.IndexOf(':') >= 0) { if (!TryParseV6(s, out b)) return false; ip = new IpAddr { V6 = true, B = b }; return true; }
            if (!TryParseV4(s, out b)) return false;
            ip = new IpAddr { V6 = false, B = b };
            return true;
        }

        // Accepts "addr/prefix", "addr mask" (IPv4) or just "addr" (host prefix). Returns a localized message on failure.
        public static bool TryParseCidr(string input, out IpAddr ip, out int prefix, out string error)
        {
            ip = null; prefix = 0; error = null;
            if (string.IsNullOrWhiteSpace(input)) { error = Loc.T("Enter an address"); return false; }
            string s = input.Trim();
            if (s.Length > MaxInput) { error = Loc.T("Not a valid address"); return false; }
            string addr = s, pre = null;
            int slash = s.IndexOf('/');
            if (slash >= 0) { addr = s.Substring(0, slash).Trim(); pre = s.Substring(slash + 1).Trim(); }
            else
            {
                int sp = s.IndexOfAny(new[] { ' ', '\t' });
                if (sp >= 0) { addr = s.Substring(0, sp); pre = s.Substring(sp + 1).Trim(); }
            }
            if (!TryParseIp(addr, out ip))
            {
                error = addr.IndexOf(':') >= 0 ? Loc.T("Not a valid IPv6 address") : addr.IndexOf('.') >= 0 ? Loc.T("Not a valid IPv4 address") : Loc.T("Not a valid address");
                ip = null; return false;
            }
            if (pre == null) { prefix = ip.Bits; return true; }
            if (pre.IndexOf('.') >= 0)
            {
                byte[] m;
                if (ip.V6 || !TryParseV4(pre, out m)) { error = Loc.T("The mask is not valid"); ip = null; return false; }
                int p = PrefixFromMask(m);
                if (p < 0) { error = Loc.T("The mask is not contiguous"); ip = null; return false; }
                prefix = p; return true;
            }
            int n;
            if (!TryInt(pre.TrimStart('/'), out n) || n < 0 || n > ip.Bits)
            {
                error = Loc.F("Prefix must be 0 to {0}", ip.Bits); ip = null; return false;
            }
            prefix = n;
            return true;
        }

        public static bool TryInt(string s, out int v)
        {
            v = 0;
            if (string.IsNullOrEmpty(s) || s.Length > 9) return false;
            foreach (char c in s) if (c < '0' || c > '9') return false;
            v = int.Parse(s, CultureInfo.InvariantCulture);
            return true;
        }

        // -1 when the mask is not a run of ones followed by zeros.
        public static int PrefixFromMask(byte[] m)
        {
            int ones = 0; bool zero = false;
            for (int i = 0; i < m.Length * 8; i++)
            {
                bool bit = (m[i / 8] & (0x80 >> (i % 8))) != 0;
                if (bit) { if (zero) return -1; ones++; } else zero = true;
            }
            return ones;
        }

        // --------------------------------------------------------- formatting
        public static string FormatV4(byte[] b) { return b[0] + "." + b[1] + "." + b[2] + "." + b[3]; }

        public static string FormatV6(byte[] b, bool compress)
        {
            var g = new int[8];
            for (int i = 0; i < 8; i++) g[i] = b[2 * i] << 8 | b[2 * i + 1];
            if (!compress)
            {
                var full = new string[8];
                for (int i = 0; i < 8; i++) full[i] = g[i].ToString("x4", CultureInfo.InvariantCulture);
                return string.Join(":", full);
            }
            int bestStart = -1, bestLen = 0;
            for (int i = 0; i < 8; )
            {
                if (g[i] == 0)
                {
                    int j = i; while (j < 8 && g[j] == 0) j++;
                    if (j - i > bestLen) { bestLen = j - i; bestStart = i; }
                    i = j;
                }
                else i++;
            }
            if (bestLen < 2) bestStart = -1;
            Func<int, int, string> join = delegate(int from, int to)
            {
                var parts = new List<string>();
                for (int i = from; i < to; i++) parts.Add(g[i].ToString("x", CultureInfo.InvariantCulture));
                return string.Join(":", parts.ToArray());
            };
            if (bestStart < 0) return join(0, 8);
            return join(0, bestStart) + "::" + join(bestStart + bestLen, 8);
        }

        public static string Format(IpAddr ip) { return ip.V6 ? FormatV6(ip.B, true) : FormatV4(ip.B); }
        static string Format(bool v6, byte[] b) { return v6 ? FormatV6(b, true) : FormatV4(b); }

        // ----------------------------------------------------- byte arithmetic
        public static byte[] MaskBytes(int bits, int prefix)
        {
            var m = new byte[bits / 8];
            for (int i = 0; i < m.Length; i++)
            {
                int rem = prefix - i * 8;
                m[i] = rem >= 8 ? (byte)0xFF : rem <= 0 ? (byte)0 : (byte)(0xFF << (8 - rem));
            }
            return m;
        }

        static byte[] And(byte[] a, byte[] b) { var r = new byte[a.Length]; for (int i = 0; i < r.Length; i++) r[i] = (byte)(a[i] & b[i]); return r; }
        static byte[] Or(byte[] a, byte[] b) { var r = new byte[a.Length]; for (int i = 0; i < r.Length; i++) r[i] = (byte)(a[i] | b[i]); return r; }
        static byte[] Not(byte[] a) { var r = new byte[a.Length]; for (int i = 0; i < r.Length; i++) r[i] = (byte)~a[i]; return r; }

        // a + 2^bit (bit counted from the least significant end); a carry out of the top is dropped.
        static byte[] AddPow2(byte[] a, int bit)
        {
            var r = (byte[])a.Clone();
            int idx = r.Length - 1 - bit / 8, add = 1 << (bit % 8);
            while (idx >= 0 && add != 0) { int v = r[idx] + add; r[idx] = (byte)(v & 0xFF); add = v >> 8; idx--; }
            return r;
        }

        static byte[] Inc(byte[] a) { return AddPow2(a, 0); }

        static byte[] Dec(byte[] a)
        {
            var r = (byte[])a.Clone();
            for (int i = r.Length - 1; i >= 0; i--) { if (r[i] != 0) { r[i]--; return r; } r[i] = 0xFF; }
            return r;
        }

        public static bool PrefixMatch(byte[] addr, byte[] net, int prefix)
        {
            byte[] m = MaskBytes(addr.Length * 8, prefix);
            for (int i = 0; i < addr.Length; i++) if ((addr[i] & m[i]) != (net[i] & m[i])) return false;
            return true;
        }

        // ------------------------------------------------- big numbers as text
        public static string Pow2String(int n)
        {
            var d = new List<int> { 1 };                       // little-endian decimal digits
            for (int i = 0; i < n; i++)
            {
                int carry = 0;
                for (int k = 0; k < d.Count; k++) { int v = d[k] * 2 + carry; d[k] = v % 10; carry = v / 10; }
                if (carry > 0) d.Add(carry);
            }
            var sb = new StringBuilder();
            for (int k = d.Count - 1; k >= 0; k--) sb.Append((char)('0' + d[k]));
            return sb.ToString();
        }

        // 2^n - 2, 2^n - 1 etc. for n <= 128 as decimal text
        static string Pow2Minus(int n, int minus)
        {
            string p = Pow2String(n);
            var d = new List<int>();
            for (int i = p.Length - 1; i >= 0; i--) d.Add(p[i] - '0');
            for (int m = 0; m < minus; m++)
            {
                int k = 0;
                while (k < d.Count && d[k] == 0) { d[k] = 9; k++; }
                if (k < d.Count) d[k]--;
            }
            var sb = new StringBuilder();
            int top = d.Count - 1; while (top > 0 && d[top] == 0) top--;
            for (int k = top; k >= 0; k--) sb.Append((char)('0' + d[k]));
            return sb.ToString();
        }

        public static string ToDecimal(byte[] a)
        {
            var w = (byte[])a.Clone();
            var digits = new StringBuilder();
            while (true)
            {
                bool zero = true; int rem = 0;
                for (int i = 0; i < w.Length; i++)
                {
                    int cur = rem * 256 + w[i];
                    w[i] = (byte)(cur / 10); rem = cur % 10;
                    if (w[i] != 0) zero = false;
                }
                digits.Insert(0, (char)('0' + rem));
                if (zero) break;
            }
            return digits.ToString();
        }

        static string ToHex(byte[] a)
        {
            var sb = new StringBuilder("0x");
            bool started = false;
            foreach (byte x in a) { if (!started && x == 0) continue; sb.Append(x.ToString(started ? "X2" : "X", CultureInfo.InvariantCulture)); started = true; }
            if (!started) sb.Append('0');
            return sb.ToString();
        }

        // Parses a decimal number into 4 bytes (if it fits) or 16 bytes.
        public static bool TryParseNumber(string s, out IpAddr ip)
        {
            ip = null;
            if (string.IsNullOrEmpty(s) || s.Length > 40) return false;
            var r = new byte[16];
            if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                string h = s.Substring(2);
                if (h.Length == 0 || h.Length > 32) return false;
                foreach (char c in h) if (!IsHex(c)) return false;
                for (int i = 0; i < h.Length; i++) SetNibble(r, h.Length - 1 - i, HexVal(h[i]));
            }
            else if (s.StartsWith("0b", StringComparison.OrdinalIgnoreCase))
            {
                string bin = s.Substring(2).Replace("_", "").Replace(" ", "");
                if (bin.Length == 0 || bin.Length > 128) return false;
                foreach (char c in bin) if (c != '0' && c != '1') return false;
                for (int i = 0; i < bin.Length; i++) if (bin[bin.Length - 1 - i] == '1') r[15 - i / 8] |= (byte)(1 << (i % 8));
            }
            else
            {
                foreach (char c in s) if (c < '0' || c > '9') return false;
                foreach (char c in s)
                {
                    int carry = c - '0';
                    for (int i = 15; i >= 0; i--) { int v = r[i] * 10 + carry; r[i] = (byte)(v & 0xFF); carry = v >> 8; }
                    if (carry != 0) return false;               // larger than 2^128 - 1
                }
            }
            bool fitsV4 = true;
            for (int i = 0; i < 12; i++) if (r[i] != 0) fitsV4 = false;
            if (fitsV4) { var v4 = new byte[4]; Array.Copy(r, 12, v4, 0, 4); ip = new IpAddr { V6 = false, B = v4 }; }
            else ip = new IpAddr { V6 = true, B = r };
            return true;
        }

        static void SetNibble(byte[] r, int nibbleFromRight, int value)
        {
            int idx = 15 - nibbleFromRight / 2;
            r[idx] |= (byte)(nibbleFromRight % 2 == 0 ? value : value << 4);
        }

        // -------------------------------------------------- classification
        static bool In4(byte[] b, int a0, int a1, int a2, int a3, int prefix) { return PrefixMatch(b, new[] { (byte)a0, (byte)a1, (byte)a2, (byte)a3 }, prefix); }

        public static string Classify4(byte[] b)
        {
            if (b[0] == 255 && b[1] == 255 && b[2] == 255 && b[3] == 255) return Loc.T("Limited broadcast");
            if (In4(b, 0, 0, 0, 0, 8)) return Loc.T("This network");
            if (In4(b, 10, 0, 0, 0, 8) || In4(b, 172, 16, 0, 0, 12) || In4(b, 192, 168, 0, 0, 16)) return Loc.T("Private (RFC 1918)");
            if (In4(b, 100, 64, 0, 0, 10)) return Loc.T("Shared address space (CGNAT)");
            if (In4(b, 127, 0, 0, 0, 8)) return Loc.T("Loopback");
            if (In4(b, 169, 254, 0, 0, 16)) return Loc.T("Link-local");
            if (In4(b, 192, 0, 0, 0, 24)) return Loc.T("IETF protocol assignments");
            if (In4(b, 192, 0, 2, 0, 24) || In4(b, 198, 51, 100, 0, 24) || In4(b, 203, 0, 113, 0, 24)) return Loc.T("Documentation");
            if (In4(b, 198, 18, 0, 0, 15)) return Loc.T("Benchmarking");
            if (In4(b, 224, 0, 0, 0, 4)) return Loc.T("Multicast");
            if (In4(b, 240, 0, 0, 0, 4)) return Loc.T("Reserved");
            return Loc.T("Public");
        }

        public static string ClassOf4(byte[] b)
        {
            if ((b[0] & 0x80) == 0) return Loc.T("Class A");
            if ((b[0] & 0xC0) == 0x80) return Loc.T("Class B");
            if ((b[0] & 0xE0) == 0xC0) return Loc.T("Class C");
            if ((b[0] & 0xF0) == 0xE0) return Loc.T("Class D (multicast)");
            return Loc.T("Class E (reserved)");
        }

        static bool In6(byte[] b, string net, int prefix) { byte[] n; TryParseV6(net, out n); return PrefixMatch(b, n, prefix); }

        public static string Classify6(byte[] b)
        {
            bool allZero = true, loop = b[15] == 1;
            for (int i = 0; i < 16; i++) { if (b[i] != 0) allZero = false; if (i < 15 && b[i] != 0) loop = false; }
            if (allZero) return Loc.T("Unspecified");
            if (loop) return Loc.T("Loopback");
            if (In6(b, "::ffff:0:0", 96)) return Loc.T("IPv4-mapped");
            if (In6(b, "64:ff9b::", 96)) return Loc.T("NAT64");
            if (In6(b, "2001:db8::", 32)) return Loc.T("Documentation");
            if (In6(b, "2001::", 32)) return Loc.T("Teredo");
            if (In6(b, "2002::", 16)) return Loc.T("6to4");
            if (In6(b, "fe80::", 10)) return Loc.T("Link-local");
            if (In6(b, "fc00::", 7)) return Loc.T("Unique local");
            if (In6(b, "ff00::", 8)) return Loc.T("Multicast");
            if (In6(b, "2000::", 3)) return Loc.T("Global unicast");
            return Loc.T("Reserved");
        }

        static string BinaryDotted(byte[] b)
        {
            var parts = new string[b.Length];
            for (int i = 0; i < b.Length; i++) parts[i] = Convert.ToString(b[i], 2).PadLeft(8, '0');
            return string.Join(".", parts);
        }

        // ------------------------------------------------------------ subnet
        public static SubnetInfo Subnet(IpAddr ip, int prefix)
        {
            int bits = ip.Bits, host = bits - prefix;
            byte[] mask = MaskBytes(bits, prefix), net = And(ip.B, mask), bc = Or(net, Not(mask));
            var si = new SubnetInfo { Ip = ip, Prefix = prefix };
            si.Network = Format(ip.V6, net);
            si.Total = Pow2String(host);
            var rows = si.Rows;
            Action<string, string> add = delegate(string k, string v) { rows.Add(new KeyValuePair<string, string>(k, v)); };
            add("IP version", ip.V6 ? "IPv6" : "IPv4");
            add("Address", Format(ip));
            if (!ip.V6)
            {
                si.Mask = FormatV4(mask); si.Wildcard = FormatV4(Not(mask)); si.Broadcast = FormatV4(bc);
                si.First = host >= 2 ? FormatV4(Inc(net)) : FormatV4(net);
                si.Last = host >= 2 ? FormatV4(Dec(bc)) : FormatV4(bc);
                if (host == 0) si.Last = si.First;
                si.Usable = host >= 2 ? Pow2Minus(host, 2) : host == 1 ? "2" : "1";
                si.Type = Classify4(ip.B); si.Class = ClassOf4(ip.B);
                add("Subnet mask", si.Mask); add("Prefix length", "/" + prefix); add("Wildcard mask", si.Wildcard);
                add("Network address", si.Network); add("Broadcast address", si.Broadcast);
                add("First host", si.First); add("Last host", si.Last);
                add("Total addresses", si.Total); add("Usable hosts", si.Usable);
                add("Address type", si.Type); add("Address class", si.Class);
                add("Binary address", BinaryDotted(ip.B)); add("Binary mask", BinaryDotted(mask));
            }
            else
            {
                si.Mask = FormatV6(mask, true); si.Broadcast = null;
                si.First = Format(true, net); si.Last = Format(true, bc);
                si.Usable = si.Total; si.Type = Classify6(ip.B);
                add("Prefix length", "/" + prefix);
                add("Network address", si.Network); add("First address", si.First); add("Last address", si.Last);
                add("Total addresses", si.Total); add("Address type", si.Type);
                add("Compressed form", FormatV6(ip.B, true)); add("Expanded form", FormatV6(ip.B, false));
                if (In6(ip.B, "::ffff:0:0", 96)) add("IPv4-mapped address", FormatV4(new[] { ip.B[12], ip.B[13], ip.B[14], ip.B[15] }));
            }
            return si;
        }

        public static bool Contains(IpAddr net, int prefix, IpAddr addr)
        {
            return net.V6 == addr.V6 && PrefixMatch(addr.B, net.B, prefix);
        }

        // Splits network/prefix into subnets of newPrefix. Returns up to maxRows rows and the total as decimal text.
        public static List<string> Split(IpAddr ip, int prefix, int newPrefix, int maxRows, out string total)
        {
            int bits = ip.Bits;
            total = Pow2String(newPrefix - prefix);
            byte[] net = And(ip.B, MaskBytes(bits, prefix)), newMask = MaskBytes(bits, newPrefix);
            int diff = newPrefix - prefix;
            ulong count = diff >= 63 ? ulong.MaxValue : 1UL << diff;
            var rows = new List<string>();
            for (ulong i = 0; i < count && rows.Count < maxRows; i++)
            {
                byte[] sub = net;
                for (int k = 0; k < 63; k++) if (((i >> k) & 1UL) != 0) sub = AddPow2(sub, bits - newPrefix + k);
                byte[] last = Or(sub, Not(newMask));
                rows.Add(Format(ip.V6, sub) + "/" + newPrefix + "   " + Format(ip.V6, sub) + " - " + Format(ip.V6, last));
            }
            return rows;
        }

        // Smallest subnet that holds the given number of usable hosts. Returns the prefix, or -1 if impossible.
        public static int PrefixForHosts(bool v6, ulong hosts, out string usable)
        {
            usable = null;
            if (hosts == 0) return -1;
            if (!v6)
            {
                if (hosts > 4294967294UL) return -1;
                for (int p = 30; p >= 0; p--)
                {
                    ulong u = (1UL << (32 - p)) - 2;
                    if (u >= hosts) { usable = u.ToString(CultureInfo.InvariantCulture); return p; }
                }
                return -1;
            }
            int h = 0;
            while (h < 64 && (1UL << h) < hosts) h++;
            if (h >= 64) return -1;
            usable = Pow2String(h);
            return 128 - h;
        }

        // ------------------------------------------------------- conversion
        public static List<KeyValuePair<string, string>> ConvertAddress(IpAddr ip)
        {
            var rows = new List<KeyValuePair<string, string>>();
            Action<string, string> add = delegate(string k, string v) { rows.Add(new KeyValuePair<string, string>(k, v)); };
            add("IP version", ip.V6 ? "IPv6" : "IPv4");
            if (!ip.V6)
            {
                add("Dotted decimal", FormatV4(ip.B));
                add("Decimal number", ToDecimal(ip.B));
                add("Hexadecimal", ToHex(ip.B));
                add("Binary", BinaryDotted(ip.B));
                add("IPv4-mapped IPv6", "::ffff:" + FormatV4(ip.B));
                add("Reverse DNS name", ip.B[3] + "." + ip.B[2] + "." + ip.B[1] + "." + ip.B[0] + ".in-addr.arpa");
                add("Address type", Classify4(ip.B));
            }
            else
            {
                add("Compressed form", FormatV6(ip.B, true));
                add("Expanded form", FormatV6(ip.B, false));
                add("Decimal number", ToDecimal(ip.B));
                add("Hexadecimal", ToHex(ip.B));
                var nib = new StringBuilder();
                for (int i = 15; i >= 0; i--) nib.Append((ip.B[i] & 0x0F).ToString("x", CultureInfo.InvariantCulture)).Append('.').Append((ip.B[i] >> 4).ToString("x", CultureInfo.InvariantCulture)).Append('.');
                add("Reverse DNS name", nib + "ip6.arpa");
                if (In6(ip.B, "::ffff:0:0", 96)) add("IPv4-mapped address", FormatV4(new[] { ip.B[12], ip.B[13], ip.B[14], ip.B[15] }));
                add("Address type", Classify6(ip.B));
            }
            return rows;
        }

        // Anything an address can be written as: dotted IPv4, IPv6, a decimal number, 0x... or 0b...
        public static bool TryParseAny(string input, out IpAddr ip)
        {
            ip = null;
            if (input == null) return false;
            string s = input.Trim();
            if (s.Length == 0 || s.Length > MaxInput) return false;
            if (TryParseIp(s, out ip)) return true;
            return TryParseNumber(s, out ip);
        }
    }

    // ---------------------------------------------------------- transfer time
    public static class TransferCalc
    {
        public static readonly string[] SizeUnits = { "bit", "B", "kB", "MB", "GB", "TB", "KiB", "MiB", "GiB", "TiB" };
        public static readonly double[] SizeBits = { 1, 8, 8e3, 8e6, 8e9, 8e12, 8 * 1024.0, 8 * 1048576.0, 8 * 1073741824.0, 8 * 1099511627776.0 };
        public static readonly string[] SpeedUnits = { "bit/s", "kbit/s", "Mbit/s", "Gbit/s", "Tbit/s", "B/s", "kB/s", "MB/s", "GB/s", "MiB/s" };
        public static readonly double[] SpeedBits = { 1, 1e3, 1e6, 1e9, 1e12, 8, 8e3, 8e6, 8e9, 8 * 1048576.0 };
        public const double MaxSeconds = 3650.0 * 86400.0;

        static bool Num(string s, out double v)
        {
            v = 0;
            if (string.IsNullOrWhiteSpace(s) || s.Length > 30) return false;
            return double.TryParse(s.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out v) && !double.IsNaN(v) && !double.IsInfinity(v);
        }

        // "90", "1:30", "1:30:00", "2h 15m", "1d2h3m4s", "250ms", "1.5h"
        public static bool TryParseDuration(string input, out double seconds)
        {
            seconds = 0;
            if (string.IsNullOrWhiteSpace(input) || input.Length > 40) return false;
            string s = input.Trim().ToLowerInvariant();
            if (s.IndexOf(':') >= 0)
            {
                string[] p = s.Split(':');
                if (p.Length < 2 || p.Length > 3) return false;
                double total = 0;
                for (int i = 0; i < p.Length; i++)
                {
                    double v;
                    if (p[i].Length == 0 || !Num(p[i], out v) || v < 0) return false;
                    if (i < p.Length - 1 && v != Math.Floor(v)) return false;
                    total = total * 60 + v;
                }
                if (total > MaxSeconds) return false;
                seconds = total; return true;
            }
            double bare;
            if (Num(s, out bare)) { if (bare < 0 || bare > MaxSeconds) return false; seconds = bare; return true; }
            double sum = 0; int pos = 0; bool any = false;
            while (pos < s.Length)
            {
                while (pos < s.Length && s[pos] == ' ') pos++;
                int st = pos;
                while (pos < s.Length && ((s[pos] >= '0' && s[pos] <= '9') || s[pos] == '.' || s[pos] == ',')) pos++;
                double v;
                if (pos == st || !Num(s.Substring(st, pos - st), out v) || v < 0) return false;
                while (pos < s.Length && s[pos] == ' ') pos++;
                int us = pos;
                while (pos < s.Length && s[pos] >= 'a' && s[pos] <= 'z') pos++;
                string unit = s.Substring(us, pos - us);
                double mul;
                switch (unit)
                {
                    case "d": mul = 86400; break;
                    case "h": mul = 3600; break;
                    case "m": case "min": mul = 60; break;
                    case "s": case "sec": mul = 1; break;
                    case "ms": mul = 0.001; break;
                    default: return false;
                }
                sum += v * mul; any = true;
                if (sum > MaxSeconds) return false;
            }
            if (!any) return false;
            seconds = sum; return true;
        }

        public static string FormatDuration(double sec)
        {
            if (double.IsNaN(sec) || double.IsInfinity(sec) || sec < 0) return "—";
            if (sec == 0) return "0 s";
            if (sec < 1) return (sec * 1000).ToString("0.###", CultureInfo.InvariantCulture) + " ms";
            if (sec < 60) return sec.ToString("0.##", CultureInfo.InvariantCulture) + " s";
            if (sec >= 365.0 * 86400.0 * 10) return "≈ " + (sec / (365.0 * 86400.0)).ToString("0.#", CultureInfo.InvariantCulture) + " " + Loc.T("years");
            long t = (long)Math.Round(sec);
            long d = t / 86400, h = t % 86400 / 3600, m = t % 3600 / 60, s = t % 60;
            string hms = h.ToString("00", CultureInfo.InvariantCulture) + ":" + m.ToString("00", CultureInfo.InvariantCulture) + ":" + s.ToString("00", CultureInfo.InvariantCulture);
            return d > 0 ? d + " d " + hms : hms;
        }

        public static string FormatNumber(double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) return "—";
            if (v == 0) return "0";
            double a = Math.Abs(v);
            return (a >= 1e12 || a < 1e-4) ? v.ToString("0.###E+0", CultureInfo.InvariantCulture) : v.ToString("0.####", CultureInfo.InvariantCulture);
        }

        // bits / (bits per second * share) -> seconds
        public static double TimeFor(double sizeBits, double speedBitsPerSec, double sharePercent)
        {
            double eff = speedBitsPerSec * sharePercent / 100.0;
            return eff > 0 ? sizeBits / eff : double.PositiveInfinity;
        }
        public static double SpeedFor(double sizeBits, double seconds, double sharePercent)
        {
            double eff = seconds * sharePercent / 100.0;
            return eff > 0 ? sizeBits / eff : double.PositiveInfinity;
        }
        public static double SizeFor(double speedBitsPerSec, double seconds, double sharePercent)
        {
            return speedBitsPerSec * seconds * sharePercent / 100.0;
        }
    }
}
