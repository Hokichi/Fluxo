# Codebase Map: Integrations

Generated: 2026-06-15

## Summary

Fluxo is local-first. Its main persistent integration is a local SQLite database under the user's Windows local app data folder. Network use is limited to update checks and installer/runtime downloads.

## Local Database

- Database engine: SQLite through EF Core.
- Database path: `%LocalAppData%\fluxo\fluxo.db`.
- Path resolver: `Fluxo.Data/Context/FluxoDbContextFactory.cs`.
- Context: `Fluxo.Data/Context/FluxoDbContext.cs`.
- Migrations assembly is configured as `fluxo` in `Fluxo.Data/Extensions/ServiceCollectionExtensions.cs`.
- App startup ensures the database directory exists in `Fluxo/App.xaml.cs`.
- App startup runs EF migrations through `MigrateAsync` in `Fluxo/App.xaml.cs`.

## Database Backup

- Startup backups are created by `BackupDatabaseOnStartupAsync` in `Fluxo/App.xaml.cs`.
- Backup folder: `%LocalAppData%\fluxo\backup`.
- Backup names include sanitized display name and timestamp.
- Backup retention is three days via `PruneExpiredBackups`.
- Backup service abstractions also exist in `Fluxo.Services/Backups/`.

## Windows Registry

- Startup registration writes to the current user's Run key in `Fluxo/Services/Ui/StartupRegistrationService.cs`.
- Installer reads installed version and install location from local machine registry views in `Fluxo.Installer/Services/InstalledVersionRegistryReader.cs`.
- Runtime ownership metadata for .NET runtime installation uses registry helpers in `Fluxo.Installer/Services/DotNetRuntimeOwnershipStore.cs`.
- Installer maintenance cleanup can delete registry subkeys in `Fluxo.Installer/ViewModels/InstallerViewModel.cs`.

## Windows Shell And Process Integration

- Tray integration uses `System.Windows.Forms.NotifyIcon` in `Fluxo/App.xaml.cs`.
- Single-instance behavior uses a named mutex and named pipe in `Fluxo/Infrastructure/SingleInstance/SingleInstanceCoordinator.cs`.
- Main app can relaunch itself from tray with `Process.Start` in `Fluxo/App.xaml.cs`.
- Main app can launch an update installer and shut down through `LaunchUpdateInstallerAndShutdown` in `Fluxo/App.xaml.cs`.
- Installer uses a global mutex in `Fluxo.Installer/Program.cs`.
- Installer can relaunch elevated through `Fluxo.Installer/InstallerElevationRelaunch.cs`.

## GitHub Releases

- Update checks call `https://api.github.com/repos/Hokichi/Fluxo/releases/latest`.
- Update client: `Fluxo/Services/Updates/AppUpdateService.cs`.
- The update HTTP client sets `User-Agent: Fluxo-Updater`.
- Installer assets are selected by name pattern `fluxo-.+-Installer.exe`.
- Downloaded installers are saved to the temp directory and deleted on failure.

## .NET Runtime Downloads

- Installer runtime detection and install flow lives under `Fluxo.Installer/Services/`.
- `Fluxo.Installer/Services/DotNetRuntimeDetector.cs` detects installed runtime state.
- `Fluxo.Installer/Services/DotNetRuntimeReleaseResolver.cs` resolves runtime release metadata.
- `Fluxo.Installer/Services/DotNetRuntimeInstaller.cs` performs runtime download/install actions.

## GitHub Actions

- Workflow: `.github/workflows/build-on-final-commit.yml`.
- Trigger: every push.
- Build only proceeds when head commit message matches `Final commit for build vX.Y.Z`.
- Workflow builds the WiX bundle on Windows, creates tag `vX.Y.Z`, and publishes the installer executable to a GitHub release.

## Notifications

- Package reference `Microsoft.Toolkit.Uwp.Notifications` exists in `Fluxo.Services/Fluxo.Services.csproj`.
- In-app notification grouping and actions live under `Fluxo/Services/Notifications/`.
- Startup notification summary is built by `Fluxo/Services/Notifications/StartupNotificationSummaryService.cs`.

## No Cloud Sync Found

- README says app is local-first.
- No cloud database, identity provider, webhook receiver, or external analytics integration was found.
- Financial data appears to stay in local SQLite unless exported/backed up by the user.
