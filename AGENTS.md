# Pinch Agent Entry

Pinch is Han's Windows WinForms image compressor. Read `README.md`, `HANDOFF.md` and `docs/upgrade-2.3.0.md` before editing.

- Stack: C# compiled with Windows .NET Framework, WinForms, System.Drawing. No external packages.
- Build: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Build-Installer.ps1`.
- Test: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Test.ps1`.
- Version: `src/AppInfo.cs`; keep the manifest identity consistent.
- Never overwrite input images or previous outputs. Validate temporary output and publish without replacement.
- Keep compression off the UI thread. Marshal result updates to the UI thread.
- Settings changes apply only on Save. Queue entries retain their own size limit and destination.
- Test all EXIF orientations and both TIFF byte orders in mixed batches.
- Preserve the transparent icon and tray-only operation.
- Build failures must stop packaging. Verify version and artifact hashes before publication.
- Updates must use cooperative shutdown, not force-kill image processing. V2.1 users must exit via the tray menu.
- Preserve user images, settings and logs on uninstall.
- `releases/` contains published artifacts; `tests/fixtures/legacy/` contains rollback data, not current programs.
- Keep credentials, local state and generated test outputs out of Git.
- Remote publication requires the user's authorization for the target repository and visibility.
