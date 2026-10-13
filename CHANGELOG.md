# Changelog

All notable changes are listed here. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project uses [Semantic Versioning](https://semver.org/).

## [1.1.2] - 2026-10-13

### Fixed
- **Programmer mode: the `C` key typed nothing but cleared the input** instead of entering the hex digit C, so numbers containing C could not be entered (also with the keyboard). Reported in issue #2 by @maik26-hub. The clear action now has its own internal name; Esc still clears. Two new self-tests cover the button and the keyboard.

## [1.1.1] - 2026-10-13

### Changed
- **The released `Prisma.exe` no longer contains the self-tests.** They moved to a separate test program that loads the real `Prisma.exe` as a library. The shipped file is smaller (454 KB instead of 538 KB) and no longer contains strings such as `WebClient`, `DownloadString`, `urlmon` or `wininet` that the security tests used as search terms and that string-based antivirus heuristics can misread. A new self-test keeps it that way.
- The `--selftest` command-line option is gone; run `test.ps1` instead.

### Fixed
- Antivirus programs that flagged `Prisma.exe` as suspicious because of those strings should no longer do so (not all engines can be checked; see SECURITY.md).

## [1.1.0] - 2026-10-13

### Added
- **Network tools** (new NETWORK section in the sidebar; pure calculations, no network access):
  - Subnet calculator for IPv4 and IPv6: network, broadcast, host range, mask, wildcard, exact address counts, address types and classes, binary and short/long forms, "is this address inside?", splitting into subnets, host planning.
  - Address converter: dotted decimal, decimal number, hexadecimal, binary, IPv4-mapped IPv6, IPv6 short/long form, reverse-DNS name.
  - Transfer time calculator: solve for time, speed or size, with the usable share of the line.
- 92 more self-tests (377 in total at that point), including fuzzing of the address and duration parsers.
- Translations for all new texts (German, French, Spanish).
- A network screenshot in the README.

## [1.0.0] - 2026-10-13

First release.

### Added
- Calculator modes: Standard, Scientific, Programmer, Graphing, Date calculation.
- Converters: Currency (example rates, user-editable), Volume, Length, Weight and mass, Temperature, Energy, Area, Speed, Time, Power, Data, Pressure, Angle.
- History, memory, keyboard input, copy and paste.
- Theming: appearance (System / Light / Dark with live follow of Windows and High Contrast), six liquid-glass styles plus a flat Classic style, accent colour, corner roundness, glass and animated background switches.
- Languages: English, German, French, Spanish (automatic or chosen), decimal comma where appropriate.
- The window keeps the size the user chose across categories and restarts.
- Logo and icon (prism splitting light), file properties, About card.
- Hardening: input limits, validated settings and rates files, log without forgeable entries, rejection of network/device paths, restricted DLL search, ASLR/DEP, `asInvoker` manifest.
- 285 self-tests (including fuzzing and checks on the built program), `SHA256SUMS.txt` and `verify.ps1`.

### Known limits
- The program is not code-signed.
- Translations were not reviewed by native speakers.
- Live currency rates, an always-on-top compact view and graph export are not included.
