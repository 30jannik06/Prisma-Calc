// Prisma - tests for the network tools: addresses, subnets, conversions, transfer times, fuzzing, and the three views. C# 5.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Prisma
{
    public static class NetTests
    {
        static Action<string, bool, string> check;

        static void Eq(string name, string actual, string expected) { check(name, actual == expected, "got '" + actual + "', expected '" + expected + "'"); }

        static SubnetInfo Sub(string s)
        {
            IpAddr ip; int p; string err;
            if (!NetCalc.TryParseCidr(s, out ip, out p, out err)) throw new InvalidOperationException("cannot parse '" + s + "': " + err);
            return NetCalc.Subnet(ip, p);
        }

        static bool Ok4(string s) { byte[] b; return NetCalc.TryParseV4(s, out b); }
        static bool Ok6(string s) { byte[] b; return NetCalc.TryParseV6(s, out b); }
        static string V6(string s, bool compress) { byte[] b; NetCalc.TryParseV6(s, out b); return NetCalc.FormatV6(b, compress); }
        static string TypeOf(string s) { IpAddr ip; NetCalc.TryParseIp(s, out ip); return ip.V6 ? NetCalc.Classify6(ip.B) : NetCalc.Classify4(ip.B); }
        static string ClassOf(string s) { IpAddr ip; NetCalc.TryParseIp(s, out ip); return NetCalc.ClassOf4(ip.B); }

        public static void Run(Action<string, bool, string> c)
        {
            check = c;
            string oldLang = Loc.Lang; Loc.Lang = "en";
            try
            {
                V4Tests(); V6Tests(); SubnetTests(); SplitTests(); ConvertTests(); TransferTests(); Fuzz();
            }
            finally { Loc.Lang = oldLang; }
        }

        static void V4Tests()
        {
            check("ipv4: valid addresses parse", Ok4("192.168.1.1") && Ok4("0.0.0.0") && Ok4("255.255.255.255") && Ok4("10.0.0.1"), "");
            check("ipv4: invalid addresses are rejected", !Ok4("256.1.1.1") && !Ok4("1.2.3") && !Ok4("1.2.3.4.5") && !Ok4("01.2.3.4") && !Ok4("a.b.c.d") && !Ok4("") && !Ok4("1..2.3") && !Ok4("1.2.3.-4") && !Ok4(" 1.2.3.4") && !Ok4("1.2.3.4 "), "");
            Eq("ipv4: 192.168.1.10/24 network", Sub("192.168.1.10/24").Network, "192.168.1.0");
            SubnetInfo s = Sub("192.168.1.10/24");
            check("ipv4: /24 has broadcast, first/last host, mask, wildcard, counts, type and class",
                s.Broadcast == "192.168.1.255" && s.First == "192.168.1.1" && s.Last == "192.168.1.254" && s.Mask == "255.255.255.0" && s.Wildcard == "0.0.0.255"
                && s.Total == "256" && s.Usable == "254" && s.Type == "Private (RFC 1918)" && s.Class == "Class C", s.Broadcast + " " + s.First + " " + s.Last + " " + s.Mask + " " + s.Wildcard + " " + s.Total + " " + s.Usable + " " + s.Type + " " + s.Class);
            s = Sub("10.0.0.5/30");
            check("ipv4: /30 has 2 usable hosts", s.Network == "10.0.0.4" && s.Broadcast == "10.0.0.7" && s.First == "10.0.0.5" && s.Last == "10.0.0.6" && s.Usable == "2", s.Network + " " + s.First + " " + s.Last);
            s = Sub("10.0.0.1/31");
            check("ipv4: /31 (point to point) uses both addresses", s.Network == "10.0.0.0" && s.First == "10.0.0.0" && s.Last == "10.0.0.1" && s.Usable == "2" && s.Total == "2", s.First + " " + s.Last + " " + s.Usable);
            s = Sub("10.0.0.1/32");
            check("ipv4: /32 is a single host", s.Network == "10.0.0.1" && s.First == "10.0.0.1" && s.Last == "10.0.0.1" && s.Usable == "1" && s.Total == "1", s.First + " " + s.Last + " " + s.Usable);
            s = Sub("0.0.0.0/0");
            check("ipv4: /0 covers 2^32 addresses", s.Total == "4294967296" && s.Usable == "4294967294" && s.First == "0.0.0.1" && s.Last == "255.255.255.254" && s.Broadcast == "255.255.255.255", s.Total + " " + s.Usable);
            s = Sub("172.20.5.9 255.255.240.0");
            check("ipv4: an address with a dotted mask works (/20)", s.Prefix == 20 && s.Network == "172.20.0.0" && s.Broadcast == "172.20.15.255", s.Prefix + " " + s.Network);
            s = Sub("192.168.5.7");
            check("ipv4: an address without prefix is a /32 host", s.Prefix == 32 && s.Total == "1", s.Prefix + "");
            IpAddr ip; int p; string err;
            check("ipv4: a mask that is not contiguous is rejected", !NetCalc.TryParseCidr("10.0.0.5 255.0.255.0", out ip, out p, out err) && err == "The mask is not contiguous", err);
            check("ipv4: prefix out of range is rejected", !NetCalc.TryParseCidr("10.0.0.5/33", out ip, out p, out err) && err == "Prefix must be 0 to 32" && !NetCalc.TryParseCidr("10.0.0.5/-1", out ip, out p, out err) && !NetCalc.TryParseCidr("10.0.0.5/abc", out ip, out p, out err), err);
            check("ipv4: empty and bad input give messages", !NetCalc.TryParseCidr("", out ip, out p, out err) && err == "Enter an address" && !NetCalc.TryParseCidr("300.1.1.1/8", out ip, out p, out err) && err == "Not a valid IPv4 address" && !NetCalc.TryParseCidr("hello", out ip, out p, out err) && err == "Not a valid address", err);
            check("ipv4: mask to prefix", NetCalc.PrefixFromMask(new byte[] { 255, 255, 255, 0 }) == 24 && NetCalc.PrefixFromMask(new byte[] { 0, 0, 0, 0 }) == 0 && NetCalc.PrefixFromMask(new byte[] { 255, 255, 255, 255 }) == 32 && NetCalc.PrefixFromMask(new byte[] { 255, 254, 0, 0 }) == 15 && NetCalc.PrefixFromMask(new byte[] { 255, 0, 255, 0 }) == -1, "");
            string[,] types = {
                { "10.1.2.3", "Private (RFC 1918)" }, { "172.16.0.1", "Private (RFC 1918)" }, { "172.31.255.255", "Private (RFC 1918)" }, { "172.32.0.1", "Public" }, { "192.168.0.1", "Private (RFC 1918)" },
                { "100.64.0.1", "Shared address space (CGNAT)" }, { "127.0.0.1", "Loopback" }, { "169.254.1.1", "Link-local" }, { "224.0.0.1", "Multicast" }, { "240.0.0.1", "Reserved" },
                { "8.8.8.8", "Public" }, { "255.255.255.255", "Limited broadcast" }, { "192.0.2.5", "Documentation" }, { "198.18.0.1", "Benchmarking" }, { "0.1.2.3", "This network" } };
            var badTypes = new List<string>();
            for (int i = 0; i < types.GetLength(0); i++) if (TypeOf(types[i, 0]) != types[i, 1]) badTypes.Add(types[i, 0] + "=" + TypeOf(types[i, 0]));
            check("ipv4: address types", badTypes.Count == 0, string.Join("; ", badTypes.ToArray()));
            check("ipv4: address classes A to E", ClassOf("10.0.0.1") == "Class A" && ClassOf("130.5.5.5") == "Class B" && ClassOf("200.1.1.1") == "Class C" && ClassOf("230.1.1.1") == "Class D (multicast)" && ClassOf("250.1.1.1") == "Class E (reserved)", "");
            IpAddr net, a1, a2, a6;
            NetCalc.TryParseIp("192.168.1.0", out net); NetCalc.TryParseIp("192.168.1.200", out a1); NetCalc.TryParseIp("192.168.2.1", out a2); NetCalc.TryParseIp("2001:db8::1", out a6);
            check("ipv4: contains", NetCalc.Contains(net, 24, a1) && !NetCalc.Contains(net, 24, a2) && !NetCalc.Contains(net, 24, a6) && NetCalc.Contains(net, 16, a2), "");
        }

        static void V6Tests()
        {
            string[] good = { "::", "::1", "2001:db8::1", "2001:0db8:0000:0000:0000:ff00:0042:8329", "::ffff:192.0.2.128", "fe80::1%eth0", "[2001:db8::1]", "1:2:3:4:5:6:7:8", "1:2:3:4:5:6:7::", "::2:3:4:5:6:7:8", "FFFF::", "1::" };
            string[] bad = { "1::2::3", ":::", "12345::", "g::1", "1:2:3:4:5:6:7", "1:2:3:4:5:6:7:8:9", ":1::2", "1.2.3.4::", "::1.2.3", "1:2:3:4:5:6:7:8::", "::1:2:3:4:5:6:7:8", "1::2:", "", "2001:db8:::1", "1:2:3:4:5:6:1.2.3.4:5" };
            var failGood = new List<string>(); foreach (string g in good) if (!Ok6(g)) failGood.Add(g);
            var failBad = new List<string>(); foreach (string b in bad) if (Ok6(b)) failBad.Add(b);
            check("ipv6: valid forms parse", failGood.Count == 0, string.Join(" | ", failGood.ToArray()));
            check("ipv6: invalid forms are rejected", failBad.Count == 0, string.Join(" | ", failBad.ToArray()));
            Eq("ipv6: RFC 5952 compression (longest run)", V6("2001:0db8:0000:0000:0000:ff00:0042:8329", true), "2001:db8::ff00:42:8329");
            Eq("ipv6: the longest run of zeros is compressed", V6("1:0:0:2:0:0:0:3", true), "1:0:0:2::3");
            Eq("ipv6: on a tie the first run is compressed", V6("1:0:0:2:0:0:3:4", true), "1::2:0:0:3:4");
            Eq("ipv6: all zeros", V6("0:0:0:0:0:0:0:0", true), "::");
            Eq("ipv6: loopback", V6("0:0:0:0:0:0:0:1", true), "::1");
            Eq("ipv6: a single zero group is not compressed", V6("1:0:2:0:3:0:4:0", true), "1:0:2:0:3:0:4:0");
            Eq("ipv6: expanded form", V6("::1", false), "0000:0000:0000:0000:0000:0000:0000:0001");
            Eq("ipv6: embedded IPv4 is read", V6("::ffff:192.0.2.128", true), "::ffff:c000:280");
            Eq("ipv6: letters are written in lower case", V6("2001:DB8::ABCD", true), "2001:db8::abcd");
            SubnetInfo s = Sub("2001:db8::1/64");
            check("ipv6: /64 network, first, last, total, type", s.Network == "2001:db8::" && s.First == "2001:db8::" && s.Last == "2001:db8::ffff:ffff:ffff:ffff" && s.Total == "18446744073709551616" && s.Type == "Documentation", s.Network + " " + s.Last + " " + s.Total);
            s = Sub("2001:db8:abcd:12::1/48");
            Eq("ipv6: /48 network", s.Network, "2001:db8:abcd::");
            Eq("ipv6: /128 is one address", Sub("::1/128").Total, "1");
            Eq("ipv6: /0 has 2^128 addresses", Sub("::/0").Total, "340282366920938463463374607431768211456");
            Eq("ipv6: /32 has 2^96 addresses", Sub("2001:db8::/32").Total, "79228162514264337593543950336");
            Eq("ipv6: prefix > 128 is rejected message", ErrOf("::1/129"), "Prefix must be 0 to 128");
            Eq("ipv6: bad address message", ErrOf("1::2::3/64"), "Not a valid IPv6 address");
            string[,] types = {
                { "::1", "Loopback" }, { "::", "Unspecified" }, { "fe80::1", "Link-local" }, { "fc00::1", "Unique local" }, { "fd12:3456::1", "Unique local" }, { "ff02::1", "Multicast" },
                { "2001:db8::1", "Documentation" }, { "2606:4700::1111", "Global unicast" }, { "::ffff:1.2.3.4", "IPv4-mapped" }, { "64:ff9b::1", "NAT64" }, { "2002::1", "6to4" }, { "2001::1", "Teredo" } };
            var badTypes = new List<string>();
            for (int i = 0; i < types.GetLength(0); i++) if (TypeOf(types[i, 0]) != types[i, 1]) badTypes.Add(types[i, 0] + "=" + TypeOf(types[i, 0]));
            check("ipv6: address types", badTypes.Count == 0, string.Join("; ", badTypes.ToArray()));
            check("ipv6: an IPv4-mapped address shows the IPv4 part", HasRow(Sub("::ffff:192.0.2.1/128").Rows, "IPv4-mapped address", "192.0.2.1"), "");
        }

        static string ErrOf(string s) { IpAddr ip; int p; string err; NetCalc.TryParseCidr(s, out ip, out p, out err); return err; }

        static bool HasRow(List<KeyValuePair<string, string>> rows, string key, string value)
        {
            foreach (KeyValuePair<string, string> kv in rows) if (kv.Key == key && kv.Value == value) return true;
            return false;
        }

        static void SubnetTests()
        {
            string u;
            check("hosts: 50 hosts need /26", NetCalc.PrefixForHosts(false, 50, out u) == 26 && u == "62", u);
            check("hosts: 62 hosts fit in /26, 63 need /25", NetCalc.PrefixForHosts(false, 62, out u) == 26 && NetCalc.PrefixForHosts(false, 63, out u) == 25, "");
            check("hosts: 1 and 2 hosts use /30", NetCalc.PrefixForHosts(false, 1, out u) == 30 && NetCalc.PrefixForHosts(false, 2, out u) == 30, "");
            check("hosts: 254 hosts need /24, 255 need /23", NetCalc.PrefixForHosts(false, 254, out u) == 24 && NetCalc.PrefixForHosts(false, 255, out u) == 23, "");
            check("hosts: zero and too many are impossible", NetCalc.PrefixForHosts(false, 0, out u) == -1 && NetCalc.PrefixForHosts(false, 4294967295UL, out u) == -1 && NetCalc.PrefixForHosts(false, 4294967294UL, out u) == 0, "");
            check("hosts: ipv6", NetCalc.PrefixForHosts(true, 1, out u) == 128 && NetCalc.PrefixForHosts(true, 2, out u) == 127 && NetCalc.PrefixForHosts(true, 65536, out u) == 112 && u == "65536", u);
        }

        static void SplitTests()
        {
            IpAddr ip; int p; string err, total;
            NetCalc.TryParseCidr("192.168.1.77/24", out ip, out p, out err);
            List<string> rows = NetCalc.Split(ip, p, 26, 64, out total);
            check("split: /24 into four /26", total == "4" && rows.Count == 4 && rows[0].StartsWith("192.168.1.0/26") && rows[3] == "192.168.1.192/26   192.168.1.192 - 192.168.1.255", total + " " + rows.Count + " " + rows[rows.Count - 1]);
            rows = NetCalc.Split(ip, p, 25, 64, out total);
            check("split: /24 into two /25", total == "2" && rows.Count == 2 && rows[1].StartsWith("192.168.1.128/25"), total);
            NetCalc.TryParseCidr("10.0.0.0/16", out ip, out p, out err);
            rows = NetCalc.Split(ip, p, 24, 64, out total);
            check("split: /16 into /24 lists only the first 64 of 256", total == "256" && rows.Count == 64 && rows[63].StartsWith("10.0.63.0/24"), total + " " + rows.Count);
            NetCalc.TryParseCidr("2001:db8::/32", out ip, out p, out err);
            rows = NetCalc.Split(ip, p, 48, 64, out total);
            check("split: ipv6 /32 into /48", total == "65536" && rows.Count == 64 && rows[1].StartsWith("2001:db8:1::/48") && rows[0].StartsWith("2001:db8::/48"), total + " " + rows[1]);
            rows = NetCalc.Split(ip, p, 128, 5, out total);
            check("split: a huge split reports the total and only a few rows", total == "79228162514264337593543950336" && rows.Count == 5, total);
        }

        static void ConvertTests()
        {
            IpAddr ip;
            check("convert: a decimal number is read as IPv4", NetCalc.TryParseAny("3232235777", out ip) && !ip.V6 && NetCalc.Format(ip) == "192.168.1.1", "");
            check("convert: 0x hex is read as IPv4", NetCalc.TryParseAny("0xC0A80101", out ip) && !ip.V6 && NetCalc.Format(ip) == "192.168.1.1", "");
            check("convert: 0b binary is read as IPv4", NetCalc.TryParseAny("0b11000000101010000000000100000001", out ip) && NetCalc.Format(ip) == "192.168.1.1", "");
            check("convert: a number above 2^32 is IPv6", NetCalc.TryParseAny("4294967296", out ip) && ip.V6 && NetCalc.Format(ip) == "::1:0:0", ip.V6 + "");
            check("convert: the largest IPv6 number is accepted, one more is not", NetCalc.TryParseAny("340282366920938463463374607431768211455", out ip) && NetCalc.Format(ip) == "ffff:ffff:ffff:ffff:ffff:ffff:ffff:ffff" && !NetCalc.TryParseAny("340282366920938463463374607431768211456", out ip), "");
            check("convert: garbage is rejected", !NetCalc.TryParseAny("hello", out ip) && !NetCalc.TryParseAny("0xZZ", out ip) && !NetCalc.TryParseAny("0b102", out ip) && !NetCalc.TryParseAny("-5", out ip) && !NetCalc.TryParseAny("0x", out ip) && !NetCalc.TryParseAny(new string('9', 80), out ip), "");
            NetCalc.TryParseAny("192.168.1.1", out ip);
            List<KeyValuePair<string, string>> r = NetCalc.ConvertAddress(ip);
            check("convert: ipv4 forms", HasRow(r, "Decimal number", "3232235777") && HasRow(r, "Hexadecimal", "0xC0A80101") && HasRow(r, "Binary", "11000000.10101000.00000001.00000001")
                && HasRow(r, "IPv4-mapped IPv6", "::ffff:192.168.1.1") && HasRow(r, "Reverse DNS name", "1.1.168.192.in-addr.arpa"), "");
            NetCalc.TryParseAny("2001:db8::1", out ip);
            r = NetCalc.ConvertAddress(ip);
            check("convert: ipv6 forms", HasRow(r, "Decimal number", "42540766411282592856903984951653826561") && HasRow(r, "Compressed form", "2001:db8::1")
                && HasRow(r, "Expanded form", "2001:0db8:0000:0000:0000:0000:0000:0001") && HasRow(r, "Hexadecimal", "0x20010DB8000000000000000000000001")
                && HasRow(r, "Reverse DNS name", "1.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.8.b.d.0.1.0.0.2.ip6.arpa"), "");
            NetCalc.TryParseAny("0.0.0.0", out ip);
            check("convert: zero", HasRow(NetCalc.ConvertAddress(ip), "Hexadecimal", "0x0") && HasRow(NetCalc.ConvertAddress(ip), "Decimal number", "0"), "");
        }

        static string Dur(double s) { return TransferCalc.FormatDuration(s); }
        static double P(string s) { double v; return TransferCalc.TryParseDuration(s, out v) ? v : -1; }

        static void TransferTests()
        {
            check("time: parses seconds, m:ss, h:mm:ss", P("90") == 90 && P("1:30") == 90 && P("1:30:00") == 5400 && P("0:00:05") == 5, P("1:30") + " " + P("1:30:00"));
            check("time: parses unit forms", P("2h 15m") == 8100 && P("1d2h3m4s") == 93784 && Math.Abs(P("250ms") - 0.25) < 1e-12 && P("1.5h") == 5400 && P("90 s") == 90, P("2h 15m") + " " + P("1d2h3m4s"));
            check("time: rejects nonsense", P("abc") < 0 && P("1:") < 0 && P("::") < 0 && P("5x") < 0 && P("-5") < 0 && P("1:2:3:4") < 0 && P("") < 0 && P(new string('1', 60)) < 0 && P("h") < 0 && P("1d 2") < 0 && P("99999999d") < 0, "");
            Eq("time: 80 seconds", Dur(80), "00:01:20");
            Eq("time: one hour one minute one second", Dur(3661), "01:01:01");
            Eq("time: over a day", Dur(90061), "1 d 01:01:01");
            Eq("time: milliseconds", Dur(0.25), "250 ms");
            Eq("time: seconds with decimals", Dur(12.5), "12.5 s");
            check("time: invalid values show a dash", Dur(-1) == "—" && Dur(double.NaN) == "—" && Dur(double.PositiveInfinity) == "—", "");
            check("transfer: 1 GB at 100 Mbit/s is 80 s", Math.Abs(TransferCalc.TimeFor(8e9, 1e8, 100) - 80) < 1e-9, "");
            check("transfer: at 50% of the line it takes twice as long", Math.Abs(TransferCalc.TimeFor(8e9, 1e8, 50) - 160) < 1e-9, "");
            check("transfer: speed and size can be solved from the others", Math.Abs(TransferCalc.SpeedFor(8e9, 80, 100) - 1e8) < 1e-3 && Math.Abs(TransferCalc.SizeFor(1e8, 80, 100) - 8e9) < 1e-3, "");
            check("transfer: a speed of zero gives infinity, not an exception", double.IsInfinity(TransferCalc.TimeFor(8e9, 0, 100)), "");
            check("transfer: unit tables line up", TransferCalc.SizeUnits.Length == TransferCalc.SizeBits.Length && TransferCalc.SpeedUnits.Length == TransferCalc.SpeedBits.Length, "");
            Eq("transfer: number format", TransferCalc.FormatNumber(1234.56789), "1234.5679");
        }

        static void Fuzz()
        {
            var rnd = new Random(31415);
            string alpha = "0123456789abcdefABCDEF:.:/ %[]xXb-_gz";
            int bad = 0; string first = null;
            for (int i = 0; i < 25000; i++)
            {
                int len = rnd.Next(0, 50);
                var sb = new StringBuilder();
                for (int k = 0; k < len; k++) sb.Append(alpha[rnd.Next(alpha.Length)]);
                string s = sb.ToString();
                try
                {
                    IpAddr ip; int p; string err;
                    if (NetCalc.TryParseCidr(s, out ip, out p, out err))
                    {
                        SubnetInfo si = NetCalc.Subnet(ip, p);
                        string total;
                        if (p < ip.Bits) NetCalc.Split(ip, p, Math.Min(ip.Bits, p + rnd.Next(1, 12)), 16, out total);
                        NetCalc.ConvertAddress(ip);
                    }
                    if (NetCalc.TryParseAny(s, out ip)) NetCalc.ConvertAddress(ip);
                    double d; TransferCalc.TryParseDuration(s, out d);
                }
                catch (Exception ex) { bad++; if (first == null) first = s + " -> " + ex.GetType().Name; }
            }
            check("fuzz: 25000 random address/duration strings never throw", bad == 0, bad + " exceptions, first: " + first);

            // random valid addresses: format -> parse round trip, and subnet invariants
            int broken = 0; string firstBroken = null;
            for (int i = 0; i < 6000; i++)
            {
                bool v6 = rnd.Next(2) == 0;
                var b = new byte[v6 ? 16 : 4]; rnd.NextBytes(b);
                if (v6 && rnd.Next(3) == 0) for (int z = 2; z < 10; z++) b[z] = 0;     // make zero runs likely
                var ip = new IpAddr { V6 = v6, B = b };
                string text = NetCalc.Format(ip);
                IpAddr back; int prefix = rnd.Next(0, ip.Bits + 1);
                SubnetInfo si = NetCalc.Subnet(ip, prefix);
                IpAddr first1 = null, last1 = null, net = null;
                bool ok = NetCalc.TryParseIp(text, out back) && NetCalc.Format(back) == text
                    && NetCalc.TryParseIp(si.First, out first1) && NetCalc.TryParseIp(si.Last, out last1) && NetCalc.TryParseIp(si.Network, out net)
                    && NetCalc.Contains(net, prefix, ip) && NetCalc.Contains(net, prefix, first1) && NetCalc.Contains(net, prefix, last1);
                if (ok && v6) ok = NetCalc.TryParseIp(NetCalc.FormatV6(b, false), out back) && NetCalc.Format(back) == text;
                if (ok && prefix < ip.Bits && prefix >= ip.Bits - 12)
                {
                    int np = Math.Min(ip.Bits, prefix + rnd.Next(1, 5)); string total;
                    foreach (string row in NetCalc.Split(net, prefix, np, 16, out total))
                    {
                        string[] parts = row.Split(new[] { "   " }, StringSplitOptions.None)[1].Split(new[] { " - " }, StringSplitOptions.None);
                        IpAddr f, l;
                        if (!NetCalc.TryParseIp(parts[0], out f) || !NetCalc.TryParseIp(parts[1], out l) || !NetCalc.Contains(net, prefix, f) || !NetCalc.Contains(net, prefix, l)) ok = false;
                    }
                }
                if (!ok) { broken++; if (firstBroken == null) firstBroken = text + "/" + prefix; }
            }
            check("fuzz: 6000 random addresses round-trip and every subnet stays inside its parent", broken == 0, broken + " broken, first: " + firstBroken);

            int durBad = 0;
            for (int i = 0; i < 3000; i++)
            {
                double sec = Math.Pow(10, rnd.NextDouble() * 9 - 3);
                string text = TransferCalc.FormatDuration(sec);
                if (text.Length == 0 || text == "—") durBad++;
            }
            check("fuzz: formatting 3000 random durations always gives a text", durBad == 0, durBad + " empty");
        }

        // ------------------------------------------------------------------- UI
        public static void RunUi(Action<string, bool, string> c, MainWindow w)
        {
            check = c;
            string oldLang = Loc.Lang; Loc.Lang = "en";
            try
            {
                w.SwitchTo(AppMode.Subnet);
                var sv = (SubnetView)w.View;
                sv.SetInput("192.168.1.10/24"); sv.SetCheck("192.168.1.200"); sv.SetNewPrefix("26"); sv.SetHosts("50");
                check("subnet view: shows the network, broadcast and usable hosts", sv.Value("Network address") == "192.168.1.0" && sv.Value("Broadcast address") == "192.168.1.255" && sv.Value("Usable hosts") == "254" && sv.InfoRowCount >= 12, sv.Value("Network address"));
                check("subnet view: address check, split and host planning", sv.CheckText == "Inside this network" && sv.SplitRowCount == 4 && sv.SplitText == "4 subnets" && sv.HostText == "Smallest subnet: /26 (62 usable)", sv.CheckText + " | " + sv.SplitText + " | " + sv.HostText);
                sv.SetCheck("10.0.0.1");
                check("subnet view: an address outside is reported", sv.CheckText == "Outside this network", sv.CheckText);
                sv.SetCheck("2001:db8::1");
                check("subnet view: another IP version is reported", sv.CheckText == "Different IP version", sv.CheckText);
                sv.SetInput("300.1.1.1/24");
                check("subnet view: bad input shows a message and no results", sv.StatusText == "Not a valid IPv4 address" && sv.InfoRowCount == 0, sv.StatusText);
                sv.SetInput("2001:db8::1/64"); sv.SetNewPrefix("66");
                check("subnet view: ipv6", sv.Value("Network address") == "2001:db8::" && sv.Value("Total addresses") == "18446744073709551616" && sv.SplitText == "4 subnets", sv.Value("Network address") + " " + sv.SplitText);
                sv.SetNewPrefix("200");
                check("subnet view: a split prefix out of range is explained", sv.SplitText == "Enter a prefix between /65 and /128", sv.SplitText);

                w.SwitchTo(AppMode.IpConvert);
                var av = (AddressView)w.View;
                av.SetInput("3232235777");
                check("address view: converts a decimal number", av.Value("Dotted decimal") == "192.168.1.1" && av.Value("Hexadecimal") == "0xC0A80101", av.Value("Dotted decimal"));
                av.SetInput("2001:db8::1");
                check("address view: converts an IPv6 address", av.Value("Expanded form") == "2001:0db8:0000:0000:0000:0000:0000:0001", av.Value("Expanded form"));
                av.SetInput("nonsense");
                check("address view: bad input shows a message", av.StatusText == "Not a valid address" && av.Value("Hexadecimal") == null, av.StatusText);

                w.SwitchTo(AppMode.Transfer);
                var tv = (TransferView)w.View;
                tv.SetSolve(0);
                tv.SetInputs("1", 4, "100", 2, "100", "");
                check("transfer view: 1 GB at 100 Mbit/s takes 80 s", tv.TimeText == "00:01:20", tv.TimeText);
                tv.SetInputs("1", 4, "100", 2, "50", "");
                check("transfer view: at 50% it takes 160 s", tv.TimeText == "00:02:40", tv.TimeText);
                tv.SetSolve(1); tv.SetInputs("1", 4, "", 2, "100", "80");
                check("transfer view: solving for speed gives 100 Mbit/s", tv.SpeedText == "100", tv.SpeedText);
                tv.SetSolve(2); tv.SetInputs("", 4, "100", 2, "100", "80");
                check("transfer view: solving for size gives 1 GB", tv.SizeText == "1", tv.SizeText);
                tv.SetSolve(0); tv.SetInputs("1", 4, "0", 2, "100", "");
                check("transfer view: a speed of 0 is explained", tv.StatusText == "Enter a speed", tv.StatusText);
                tv.SetInputs("1", 4, "100", 2, "150", "");
                check("transfer view: a share above 100 is explained", tv.StatusText == "Share must be between 1 and 100", tv.StatusText);

                Loc.Lang = "de";
                w.SwitchTo(AppMode.Subnet);
                var sv2 = new SubnetView();
                sv2.SetInput("10.0.0.5/30");
                check("subnet view in German: labels and types are translated", sv2.Value("Network address") == "10.0.0.4" && sv2.Value("Address type") == "Privat (RFC 1918)" && sv2.HostText.StartsWith("Kleinstes Subnetz"), sv2.Value("Address type") + " | " + sv2.HostText);
            }
            finally { Loc.Lang = oldLang; }
        }
    }
}
