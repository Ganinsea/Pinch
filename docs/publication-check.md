# V2.3.0 public repository check

Date: 2026-09-17.

- Exported from the maintained Pinch V2.3.0 source.
- Rebuilt the application, uninstaller and single-file installer from the isolated public export.
- Full Windows regression suite: **83 checks passed**.
- The legacy rollback fixture is included in the repository; no private history directory is required.
- Public text files were checked for token/private-key patterns and machine-specific private metadata before upload.
- Versioned executable snapshots are in `releases/v2.3.0/`; `release.json` records their original SHA-256 values.

The published executable snapshots are the previously validated V2.3.0 release. Rebuilding the same source can change PE build metadata and file hashes; compare downloaded release assets with the published `release.json`, not with a later local rebuild.

Validation limits are documented in `HANDOFF.md` and `docs/upgrade-2.3.0.md`. This check does not claim every Windows/DPI/display combination has been verified.
