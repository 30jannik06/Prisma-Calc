# Design notes

Decisions that shaped Prisma, why they were made, and what to change if you disagree.

## Features and behaviour
| Decision | Why | If you disagree |
|---|---|---|
| Modes: Standard, Scientific, Programmer, Graphing, Date calculation, 13 converters, history, memory, copy/paste | A complete everyday calculator. Not included: compact always-on-top view, graph export, live currency rates | Open an issue; see CONTRIBUTING.md |
| Standard mode uses immediate execution (2+3x4 = 20); Scientific mode uses normal precedence (= 14) | Matches a desk calculator in Standard and an expression calculator in Scientific | Change the Standard engine in `CalcEngine` |
| Numbers are shown with the display language's decimal separator (comma in de/fr/es); engines always use a dot; typing either works | Natural display without parsing ambiguity | Change `Loc.Num` |
| Currency rates are fixed example values (EUR base) editable through `rates.ini`; nothing is downloaded | Fetching live rates needs network access, which the program deliberately does not have | Add an opt-in refresh, with its own security review |
| Unit factors are the standard definitions (international yard and pound, US customary volumes, thermal calorie 4.184 J, year = 365 days, Mach = 343 m/s) | Widely used defaults | Edit `Units.Get` |
| Graph equations use radians | The usual graphing default | Add an angle switch |
| Programmer-mode division truncates toward zero; `>>` is an arithmetic shift | Common integer semantics | Edit `ProgEngine.Apply` |
| Exit codes: 0 ok, 1 failure or failed self-test, 2 usage error | Conventional | Edit `Program.Main` |

## Look and feel
| Decision | Why | If you disagree |
|---|---|---|
| "Liquid" look = animated gradient backdrop with soft colour blobs, translucent glass surfaces with a light edge and sheen, springy key presses. It is not a blur of the desktop behind the window | WPF cannot blur other windows without undocumented APIs | A Mica/acrylic backdrop needs extra interop and Windows 11 |
| Six glass styles (Aurora, Sunset, Ocean, Forest, Rose, Graphite) plus a flat Classic style; each style has a light and a dark variant | Variety without a theme editor | Edit the tables at the top of `ThemeManager` in `src/Core.cs`; the self-tests check contrast |
| Default for new users: Aurora, glass and animation on | Shows the look at its best | Change `ThemeManager.DefaultStyle`. The animation can be switched off in Settings (saves power) |
| The theme fade is a 320 ms timer that swaps in new frozen brushes | WPF freezes brushes that templates use, so animating the brush objects throws | Not needed |
| Corner radius = 22% of the element's height, clamped to 2-12 px, times a scale (Square 0.25, Soft 1.0, Round 1.7) | Short buttons must not turn into pills | `Ui.Radius` in `src/Ui.cs` |
| Windows 11 title-bar colours (DWM attributes 34/35/36) are best-effort; Windows 10 keeps the system title bar | The attributes only exist on Windows 11 | None |
| The manifest sets `asInvoker` and the supported Windows versions but no DPI mode | WPF already scales for the system DPI; a stricter mode is not a security measure | Add `PerMonitorV2` to `src/app.manifest` |
| Window size: the size the user sets (a move/resize that changes the size by more than 2 px, detected through WM_EXITSIZEMOVE) is kept across categories and restarts. A mode that needs more room (minimum 80% of its preferred size) enlarges the window only while it is shown. Opening the sidebar or history panel changes the width by the panel width. Growth is clamped to the primary work area | Respect the user's choice but never clip a mode | `MainWindow.TargetSize`, `FitWindow` |
| Logo: a prism splitting a white beam into colours, designed for this project. Icon sizes below 256 px are classic bitmaps, only 256 px is PNG | Matches the name; older Windows APIs cannot read small PNG icon entries | Replace `assets/prisma-logo.svg`, update `tools/make-icon.ps1` and `Ui.Logo`, rerun the script |

## Network tools
| Decision | Why | If you disagree |
|---|---|---|
| Subnet, address and transfer tools are pure calculations; Prisma does not look anything up (no DNS, no ping, no whois). The reverse-DNS name is only computed as text | Keeps "no network access" true | A lookup would need network code and its own security review |
| Addresses are parsed and formatted by Prisma's own code in `src/Net.cs` instead of `System.Net.IPAddress`, and IPv6 counts use decimal-string arithmetic instead of `BigInteger` | The self-tests assert that the program references no `System.Net` types and only the .NET Framework and WPF assemblies | Use the framework types if you accept a weaker "no networking API" check |
| IPv4 parsing is strict: exactly four decimal parts, no leading zeros (`010` is ambiguous between decimal and octal), no shorthand like `1.2.3` | Avoids silent misreadings | `NetCalc.TryParseV4` |
| IPv6 follows RFC 4291/5952: `::` once, optional embedded IPv4, longest zero run compressed (leftmost on ties), lower case. `[...]` and a `%zone` are accepted and the zone is ignored | Standard text form | `NetCalc.TryParseV6`, `FormatV6` |
| /31 uses both addresses (point-to-point, RFC 3021) and /32 is one host; IPv6 has no broadcast and all addresses of a prefix count as usable | Matches current practice | `NetCalc.Subnet` |
| Address types cover the common special ranges (private, shared/CGNAT, loopback, link-local, documentation, benchmarking, multicast, reserved; IPv6: unique local, global unicast, NAT64, 6to4, Teredo, IPv4-mapped); the list is not the full IANA registry | Enough for everyday use | Extend `Classify4` / `Classify6` |
| "Hosts needed" picks the smallest subnet whose usable hosts (network and broadcast excluded) are at least N; one or two hosts give /30 | Classic LAN planning | `NetCalc.PrefixForHosts` |
| Split lists at most 64 subnets and always shows the exact total | Keeps the screen and memory bounded | `NetCalc.Split` |
| Transfer units: kB/MB/GB/TB are decimal (1000), KiB/MiB/GiB/TiB are binary (1024); speeds in bits or bytes per second; "share of the line" scales the speed (for protocol overhead) | The usual conventions | `TransferCalc` |
| All inputs are length-limited (80 characters for addresses, 40 for durations) and every parser returns a message instead of throwing; fuzz tests cover them | Same input policy as the rest of the program | Constants in `NetCalc` / `TransferCalc` |

## Languages
| Decision | Why | If you disagree |
|---|---|---|
| English, German, French, Spanish; "Automatic" follows the Windows display language and falls back to English | A useful starting set | Add a column to `Loc.cs` and a code to `Loc.Codes` |
| Translations were written without native-speaker review | No review was available | Correct entries in `src/Loc.cs` (one line each) |
| Dates in fr/es read and show day/month/year; in English a slash date means month/day/year; `yyyy-mm-dd` works everywhere | Follow local convention | `DateCalc.TryParse` |
| Unit abbreviations and names equal in all four languages (Celsius, Kelvin, Pascal, ...) are not translated | No benefit | None |

## Build and security
| Decision | Why | If you disagree |
|---|---|---|
| Built with the C# 5 compiler (`csc.exe`) that ships with Windows; needs .NET Framework 4.6.2 or newer | Nothing to install, no dependencies, reproducible anywhere on Windows | Move to the .NET SDK and a newer C# if you accept a toolchain dependency |
| No network access, no process launching, no third-party code | Small attack surface; checked by the self-tests on the built program | Discuss in an issue first |
| Limits: expressions 1000 characters, 100 nesting levels, 200 chained prefix operators; settings and rates files 16 KB; log rolls at 1 MB | A long run of `(` crashes any recursive parser with a stack overflow; the limits are far above normal use | Constants in `Expr`, `Settings`, `Units`, `Log` |
| `--log` and `--settings` refuse network (UNC) and device paths | Keeps "no network" true for file output | `SafePath` |
| The program is not code-signed | No certificate | Sign `Prisma.exe` after building; releases carry SHA-256 checksums and (for public repositories) a build provenance attestation |
