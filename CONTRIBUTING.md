# Contributing to Prisma

Thanks for helping. Prisma is a small calculator for Windows written in C# 5 and WPF. It builds with the compiler that ships with Windows, so there is nothing to install.

## Build and test
You need Windows 10 or 11 and Windows PowerShell 5.1 (both are there by default).
```
powershell -ExecutionPolicy Bypass -File build.ps1    # warnings are errors
powershell -ExecutionPolicy Bypass -File test.ps1     # builds the test program, runs all self-tests; must end with "0 failed"
powershell -ExecutionPolicy Bypass -File package.ps1  # optional: dist\ + Prisma.zip + SHA256SUMS.txt
powershell -ExecutionPolicy Bypass -File verify.ps1   # optional: check the package
```
Run `dist\Prisma.exe` to try your change. Please run the tests before opening a pull request; CI runs the same scripts.

## Rules that keep the project safe and simple
- **C# 5 only.** The Windows compiler is C# 5: no string interpolation (`$""`), `?.`, `nameof`, expression-bodied members, `out var`, tuples or `async`-heavy code.
- **No network, no process launching, no third-party code.** The self-tests check the built program for this. If you think a feature needs one of them, open an issue first.
- **Treat files and pasted text as untrusted.** Validate and cap anything read from disk, the clipboard or the command line (see `Settings.IsValid`, `Expr.MaxLength`, `SafePath`).
- **Every visible text goes through `Loc.T(...)`** and has German, French and Spanish entries in `src\Loc.cs`. A self-test fails if an entry is missing or a `{0}` placeholder is lost.
- **New behaviour comes with a self-test** in `src\SelfTest.cs` or `src\HardeningTests.cs`.
- **Keep colours readable.** Theme palettes are checked for contrast; run the tests after changing `ThemeManager`.

## Where things are
| Path | What |
|---|---|
| `src\Engines.cs` | expression parser, standard/programmer engines, dates, units |
| `src\Net.cs`, `src\ViewsNet.cs` | network tools: IPv4/IPv6 maths, subnets, conversions, transfer time, and their views |
| `src\Core.cs` | log, settings, theme palettes |
| `src\Ui.cs`, `src\MainWindow.cs`, `src\Views*.cs`, `src\SettingsView.cs` | interface |
| `src\Loc.cs` | translations (one line per text) |
| `tests\` | self-tests and fuzz tests. They are built into a separate `Prisma.Tests.exe` that loads the real `Prisma.exe`; **never put test code or test-only strings into `src\`**, they would end up in the released program |
| `assets\`, `tools\make-icon.ps1` | logo and icon; run the script after changing the design |
| `docs\images\`, `tools\make-screenshots.ps1` | README screenshots; build first, then run the script to refresh them (it starts the program, fills the views and photographs the window, so leave the mouse alone for a minute) |
| `assets\social-preview.png`, `tools\make-social-preview.ps1` | the 1280 x 640 image GitHub shows when the repository is shared; regenerate after a design change and upload it under Settings > General > Social preview |

## Translations
Edit the table at the top of `src\Loc.cs`: `A("English text", "Deutsch", "Français", "Español")`. To add a language, add a column and a code to `Loc.Codes` / `Loc.Native`.

## Releases (maintainers)
1. Update `CHANGELOG.md` and the version in `src\AssemblyInfo.cs` (`AssemblyVersion` and `AssemblyFileVersion`, e.g. `1.1.0.0`).
2. Merge to `main`, then tag: `git tag v1.1.0 && git push origin v1.1.0`.
3. The `Release` workflow builds, tests, packages and publishes `Prisma.zip` and `SHA256SUMS.txt`. It refuses a tag that does not match the program version.

GitHub Actions are referenced by version tag and kept up to date by Dependabot. For stricter supply-chain protection you can pin them to full commit SHAs.

## Reporting security problems
Please do not open a public issue for a vulnerability. See `SECURITY.md`.
