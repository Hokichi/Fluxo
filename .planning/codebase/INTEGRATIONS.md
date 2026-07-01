# Codebase Map: Integrations

Generated: 2026-07-01
Branch: `main`

## Summary

Fluxo is local-first and has no application server, cloud database, telemetry, identity provider, or payment/bank API. Runtime network traffic is limited to GitHub release checks/downloads and Microsoft .NET runtime metadata/downloads used by the installer.

## SQLite And EF Core

- EF Core SQLite database: `%LocalAppData%\fluxo\fluxo.db`.
- Path/connection-string source: `Fluxo.Data/Context/FluxoDbContextFactory.cs`; parent directory is created explicitly.
- Runtime registration: `Fluxo.Data/Extensions/ServiceCollectionExtensions.cs`; context is scoped and database/update/infrastructure events are forwarded into Fluxo logging.
- Migrations are compiled into assembly `fluxo` and stored under `Fluxo/Migrations/`.
- Startup migration logic in `Fluxo/App.xaml.cs` handles fresh databases, legacy schemas/missing history, migration-history seeding, and then `MigrateAsync`.
- No remote database connection or configurable connection string was found.

## Local Backups, Import, And Export

- Startup database copies use `%LocalAppData%\fluxo\backup\{username}_{timestamp}_backup.db`; first-run copies are skipped and files older than three days are pruned.
- Data-management safety copies also use `%LocalAppData%\fluxo\backup\data-management_{timestamp}.db` before destructive restore operations.
- User-selectable JSON backups default to `%LocalAppData%\fluxo\user_backups\fluxo_user-backup_{timestamp}.json`.
- JSON backup schema is version `2`, camelCase, indented, and enum names are serialized with `System.Text.Json`; append and overwrite restore modes are supported.
- WPF `OpenFileDialog`/`SaveFileDialog` select JSON import/export paths.
- Ledger export uses `SaveFileDialog` and writes UTF-8-with-BOM CSV with invariant date/money formatting.
- These files are local and user-controlled; no automatic cloud backup/sync exists.

## Local Logging And Configuration

- Serilog writes under `%LocalAppData%\fluxo\logs\` with `db`, `issues`, and `others` subdirectories.
- Log filenames include sanitized app username/date; unhandled exceptions additionally create timestamped `fluxo_exception_*.log` files.
- User preferences and operational state are database-backed `UserSettings`, not `appsettings.json` or environment configuration.
- `LOCALAPPDATA` is consulted for log placement with `Environment.SpecialFolder.LocalApplicationData` fallback.
- No appsettings files, secret store, command-line config framework, or runtime feature-flag service was found.

## Local Authentication And Secret Protection

- No user account, OAuth/OIDC, API-key, session, or remote authentication integration exists.
- Optional UI-lock password is protected with Windows DPAPI (`ProtectedData`, `DataProtectionScope.CurrentUser`) and fixed application entropy before storage in user settings.
- UI locking is local application access control, not encryption of the SQLite database or exported backups.

## Windows Registry And Installation

- Run-at-startup registration writes/removes `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\Fluxo` with `--startup-tray`.
- Installed version/location are read from 64-bit then 32-bit HKLM views at `SOFTWARE\Microsoft\Windows\CurrentVersion\fluxo`.
- Installer-owned runtime metadata is stored in 64-bit HKLM at `SOFTWARE\fluxo\Runtime` with version, RID, installer URL, and ownership marker.
- MSI is per-machine x64, defaults to `C:\Program Files\fluxo`, and performs elevated HKLM cleanup during uninstall.
- Installer can relaunch elevated and uses WiX Bootstrapper Application APIs for install/repair/uninstall flow.

## Windows Shell, IPC, And Processes

- System tray integration uses `System.Windows.Forms.NotifyIcon`; app supports minimize-to-tray and tray relaunch/update shutdown flows.
- Main-app single-instance coordination uses a named Windows mutex plus named pipe activation; installer separately uses global mutex `Global\Fluxo.Installer.SingleInstance`.
- Process launching is used for elevation, runtime installers/uninstallers, app updates, and app restart. Installer child processes run hidden/non-shell where appropriate.
- Windows desktop notifications are available through `Microsoft.Toolkit.Uwp.Notifications`; application notification state/actions are persisted locally.

## GitHub Releases And App Updates

- Update endpoint: `https://api.github.com/repos/Hokichi/Fluxo/releases/latest`.
- `Fluxo/Services/Updates/AppUpdateService.cs` sends `User-Agent: Fluxo-Updater`, parses release JSON, and selects assets matching `fluxo-.+-Installer.exe`.
- Downloaded update installers are written to the Windows temp directory, validated for expected response/content, launched by the app, and deleted on failed/cancelled downloads.
- No GitHub authentication token is used by the desktop app; requests target public release metadata/assets.

## Microsoft .NET Runtime Acquisition

- Managed installer checks for the required .NET 10 Windows Desktop runtime.
- Release index endpoint: `https://dotnetcli.blob.core.windows.net/dotnet/release-metadata/releases-index.json`.
- Installer follows the release metadata URL, selects latest Windows Desktop `win-x64` executable, downloads it to temp storage, runs it silently, and records ownership for uninstall cleanup.

## GitHub Actions Release Automation

- `.github/workflows/build-on-final-commit.yml` runs on every push but builds only when the head message exactly matches `Final commit for build vX.Y.Z`.
- GitHub-hosted Ubuntu parses the version and creates the tag; `windows-latest` restores/builds the installer with .NET `10.0.x`.
- `actions/upload-artifact@v4` and `actions/download-artifact@v4` transfer the installer between jobs.
- `softprops/action-gh-release@v2` publishes the versioned installer using workflow `contents: write` permission.

## Explicitly Absent

- No cloud sync/storage, web API backend, bank/payment provider, analytics/telemetry SDK, crash-reporting service, email/SMS provider, webhook receiver, or external identity system found.
- Financial data remains in local SQLite unless user exports JSON/CSV/database backups or manually copies the local data directory.
