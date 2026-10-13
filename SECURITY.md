# Prisma - security notes

## What the program does and does not touch
| Area | Behaviour |
|---|---|
| Network | None. No socket, HTTP or download API is referenced; the self-tests check this on the built program. A running instance has 0 TCP connections and 0 UDP endpoints. |
| Processes | Starts none. |
| Privileges | Runs as the normal user (`asInvoker`), never asks for administrator rights, no UI-access. |
| Files it writes | `%LOCALAPPDATA%\Prisma\prisma.log`, `settings.ini` (and `prisma.log.1`). Nothing else. |
| Files it reads | its own settings, optionally `rates.ini`. |
| Registry | Reads the Windows "app light/dark" value only. Writes nothing. |
| Clipboard | Copies the current result as text; reads text only when you press Ctrl+V. |

## Protections built in
- **Input limits:** expressions are limited to 1000 characters and 100 levels of nesting, prefix-operator chains to 200. Without these, a long run of `(` crashes any recursive parser with a stack overflow (shown by the self-tests on an unhardened copy). Text boxes, paste and the memory/history lists are capped too.
- **Files are untrusted input:** `settings.ini` accepts only known keys with valid values, is capped at 16 KB, and is written through a temporary file so a crash cannot leave it half written. `rates.ini` is capped at 16 KB and every rate is range-checked.
- **Log:** one entry is always one line (line breaks and control characters are neutralised, so entries cannot be forged); messages are length-capped; the file rolls over at 1 MB.
- **Paths from the command line** (`--log`, `--settings`): network (UNC) paths, device paths, reserved device names and folders are rejected.
- **DLL loading:** the program restricts DLL search to the Windows system folder (`SetDefaultDllDirectories`), so a DLL placed next to the program or on the PATH is not loaded.
- **Exploit mitigations:** ASLR, high-entropy ASLR and DEP are on in the built file (checked by the self-tests). The build treats warnings as errors.
- **Crash safety:** unhandled errors are logged and, in the UI, do not close the window; the process also logs fatal errors.

## Checked by tests (run `Prisma.exe --selftest`)
Random-input fuzzing (15,000 expressions, 12,000 key sequences on the two engines, 8,000 date cases, 10,000 extreme unit conversions), hostile `settings.ini` / `rates.ini` / log targets, path rejection, and properties of the built program (mitigations, manifest, referenced assemblies, no networking imports).

## Verifying a download
`powershell -ExecutionPolicy Bypass -File verify.ps1 <folder>` compares the files with `SHA256SUMS.txt`. The checksum file travels with the program, so it detects accidental damage but not a deliberate swap of both; compare the hash of the zip against a value you got another way.

## Known limits
- The program is **not code-signed** (no certificate). Windows SmartScreen may warn on first start.
- It is built with the C# 5 compiler that ships with Windows and runs on .NET Framework 4.x; it receives security fixes only through Windows Update.
- Any program running as your user can read or change `settings.ini` and `rates.ini`; the program treats both as untrusted input but cannot prevent that.
- Security software that injects its own DLLs into every process shows up in a process's module list; that is not Prisma.
- Translations were written without native-speaker review; a wrong translation is a usability issue, not a security one.

## Reporting a problem
Please report vulnerabilities **privately** through GitHub: open the repository's **Security** tab and choose **Report a vulnerability** (private vulnerability reporting). Do not open a public issue for a security problem. Include the Prisma version, your Windows version, the steps to reproduce and, if relevant, the matching lines of `prisma.log` (remove personal data first).

You can expect an acknowledgement within a few days. Fixes are released as a new version and listed in `CHANGELOG.md`.

## Supported versions
Only the latest release receives security fixes.

## Checking a release
Each release lists the SHA-256 hash of `Prisma.zip` in its description and contains `SHA256SUMS.txt`. For public repositories the release workflow also publishes a signed build provenance attestation, which proves that the file was built by this repository's workflow (`gh attestation verify Prisma.zip --repo 30jannik06/Prisma-Calc`).
