# Windows installation implementation plan

> Execute the following tasks inline with the executing-plans and verification
> skills. User authorization covers this TODO's reversible implementation work.

**Goal:** Install, reopen and remove the bundled Windows application while
preserving authored notes and unrelated Windows settings.

**Architecture:** Inno Setup owns binary delivery and registered removal. A small
Pascal include owns user PATH changes; CLI and Notes expose a shared lifetime
mutex. Installer integration tests compile the same recipe with isolated names
and exercise real Windows installation and removal under a disposable target.

**Tech stack:** .NET 10, PowerShell 7, pinned Inno Setup 6.7.3.

**Spec:** [installation design](../specs/2026-09-10-windows-installation-design.md).

## Constraints

- Per-user only; no forced application termination, reboot or file execution.
- Preserve authored examples, other files, registry defaults and PATH ordering.
- Original source notes and design documents are not build or test inputs.
- Unsigned workspace package; no public release or clean-account claim without evidence.

## Tasks

- [x] **1. Define executable acceptance checks.** Add
  `tests/Invoke-InstallationSmoke.ps1`, using test-owned destinations and exact
  registry reads. Require a setup artifact, then check install, repeat install,
  preserved edited examples, literal association arguments, Start links, PATH
  ownership, open-process refusal and uninstall. First run must fail because no
  installer exists. Tests retain logs and record account/elevation.
- [x] **2. Build the real installer.** Add `src/Installer/DERT.iss`,
  `src/Installer/UserPath.iss` and `src/Build-Installer.ps1`.
  `Build-Installer.ps1 -Compiler <ISCC.exe> -PackageDirectory <published folder>`
  produces `.artifacts/installer/DERT-Setup.exe`; `-ProofId <guid>` produces an
  isolated test identity. The proof recipe changes integration names and
  roots and enables explicit late-failure injection absent from production.
  Include both runtimes, preserve user examples, quote direct Notes file
  opening, expose optional PATH and Open with integration, and register removal.
- [x] **3. Protect running work.** Add `ApplicationLifetime` to the CLI assembly,
  acquire it for CLI and Notes process lifetimes, and have setup/remove refuse
  while the named mutex exists. Extend the executable test to open a real Notes
  window, observe refusal, close it normally, and proceed. No kill-based repair.
- [x] **4. Verify delivery within this machine's evidence limits.** Run the installer tests, inspect actual wizard and
  installed Notes, and run the full solution and published executable suites.
  Add compiler/source-copy support to `Invoke-SourceOnlySmoke.ps1` without making
  the ordinary build depend on an installed compiler. Preserve test artifacts.
  Final: 1,252 tests, eleven existing executable suites, then 80 installation
  assertions and six native interaction checks passed. Installed Notes opening
  and normal close were verified; inactive-desktop WPF capture was black, so
  visual evidence is limited to separate native control renders. Clean-account
  and human first-contact acceptance remain open in TODO.
- [x] **5. Reconcile docs.** Update README, TODO, implementation status and
  changelog with observed behavior, the concrete setup path and exact remaining
  evidence. Leave clean-account validation unchecked unless actually performed.
