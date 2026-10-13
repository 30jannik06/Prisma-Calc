# Changelog

All notable changes are listed here. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project uses [Semantic Versioning](https://semver.org/).

## [1.0.0] - 2026-10-13

First release.

### Added
- Calculator modes: Standard, Scientific, Programmer, Graphing, Date calculation.
- Network tools: subnet calculator for IPv4 and IPv6 (network, broadcast, host range, mask, wildcard, exact address counts, address types, address check, splitting, host planning), address converter (dotted, decimal, hex, binary, IPv6 forms, reverse DNS) and transfer time calculator. Pure calculations, no network access.
- Converters: Currency (example rates, user-editable), Volume, Length, Weight and mass, Temperature, Energy, Area, Speed, Time, Power, Data, Pressure, Angle.
- History, memory, keyboard input, copy and paste.
- Theming: appearance (System / Light / Dark with live follow of Windows and High Contrast), six liquid-glass styles plus a flat Classic style, accent colour, corner roundness, glass and animated background switches.
- Languages: English, German, French, Spanish (automatic or chosen), decimal comma where appropriate.
- The window keeps the size the user chose across categories and restarts.
- Logo and icon (prism splitting light), file properties, About card.
- Hardening: input limits, validated settings and rates files, log without forgeable entries, rejection of network/device paths, restricted DLL search, ASLR/DEP, `asInvoker` manifest.
- 377 self-tests (including fuzzing and checks on the built program), `SHA256SUMS.txt` and `verify.ps1`.

### Known limits
- The program is not code-signed.
- Translations were not reviewed by native speakers.
- Live currency rates, an always-on-top compact view and graph export are not included.
