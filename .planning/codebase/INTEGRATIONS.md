# External Integrations

**Analysis Date:** 2026-05-09

## External APIs and Services

- No outbound HTTP, REST, GraphQL, webhook, payment, email, SMS, analytics, or cloud service integration detected.
- No `HttpClient`, `WebRequest`, or app-side network client usage found in production source.
- No external authentication or identity provider detected.

## Storage Integrations

- Primary datastore is local SQLite through EF Core.
- Connection string is built by `FluxoDbContextFactory.BuildConnectionString()`.
- Database path resolves to `Path.Combine(AppContext.BaseDirectory, "fluxo.db")`.
- App startup applies EF migrations and can infer/seed migration history for older local databases.
- MSI packaging excludes `*.db` files from installed app payloads.
- Logs are local files under `AppContext.BaseDirectory\logs`.
- Installer diagnostics append to `%TEMP%\Fluxo.Installer\bootstrapper-error.log`.

## OS and Platform Integrations

- WPF desktop windowing, dispatcher, controls, popups, and resource dictionaries.
- Windows Forms `NotifyIcon` is used for system tray presence and tray menu interaction.
- Windows registry is used for run-at-startup:
  - HKCU `Software\Microsoft\Windows\CurrentVersion\Run`
  - value name `Fluxo`
  - app argument `--startup-tray`
- Windows registry is used by the installer for installed-version detection:
  - HKLM `SOFTWARE\Microsoft\Windows\CurrentVersion\fluxo`
  - value name `InstalledVersion`
  - reads both 64-bit and 32-bit registry views.
- Main app single-instance behavior uses a local named mutex and named pipe:
  - mutex `Local\Fluxo.SingleInstance`
  - pipe `Fluxo.Activate`
- Installer single-instance behavior uses mutex `Global\Fluxo.Installer.SingleInstance`.
- App restart and installer launch flows use `Process.Start`.
- Installer runtime detection shells out to `dotnet --list-runtimes`.

## Installer and Deployment Integrations

- WiX Toolset 7 authors the MSI and bundle.
- `Fluxo.Installer.Bundle` embeds the managed bootstrapper executable and required payload DLLs.
- Bundle chains the MSI as `FluxoMsi` and passes `INSTALLFOLDER`.
- Default install location is `C:\Program Files\fluxo`.
- MSI writes the installed version to HKLM and uses major-upgrade behavior.
- Installer can run interactive WPF UI or headless Burn flow depending on bootstrapper display mode.
- Installer handles install, repair, uninstall, rollback display, up-to-date detection, and deferred cleanup scripts.
- Installer asks before terminating running Fluxo processes during install/repair/uninstall flows.
- Release workflow publishes the generated installer executable as a GitHub release asset for matching final-commit messages.

## Logging and Observability

- Serilog is initialized at app startup through `FluxoLogManager.Initialize`.
- Log filename is based on the configured app display name and current date; invalid filename characters are removed.
- EF Core logs database/update/infrastructure messages through `ILogService` when available.
- Global exception handlers log dispatcher, app-domain, and unobserved task exceptions.
- No external telemetry, metrics, crash reporting, or centralized log shipping detected.

## Dependency Injection and Composition

- Composition is manual via `new ServiceCollection()` in `App`.
- Registration extension chain:
  - `AddFluxoData()`
  - `AddFluxoPresentation()`
  - `AddUIData()`
- EF `FluxoDbContext`, repositories, unit of work, and app data services are scoped.
- Application services and most popup/view-model registrations are transient or singleton according to UI lifetime needs.
- `MainWindow`, core shell view models, dialog service, log service, messenger, startup registration service, and UI settle awaiter are singleton.
- `Microsoft.Extensions.Hosting` is referenced by the app project but the runtime currently builds a service provider directly rather than using a generic host.

## MVVM and In-Process Messaging

- CommunityToolkit.Mvvm provides observable object and command patterns.
- `WeakReferenceMessenger.Default` is registered as the app-wide `IMessenger`.
- Message contracts live in `Fluxo.Resources/Resources/Messages/**`.
- Messaging coordinates dashboard invalidation, settings apply/revert flows, startup wizard draft changes, log-memory actions, view-mode changes, date-range changes, and related UI updates.

## Notifications

- Current notification behavior is local/in-app: persisted notification entities, grouping services, checklist actions, panels, and tray startup summary popup.
- No active Windows toast notification integration found, despite `Microsoft.Toolkit.Uwp.Notifications` being referenced.

## Secrets and Configuration

- No `.env` files or production `Environment.GetEnvironmentVariable(...)` usage detected.
- No secrets manager, cloud config provider, OAuth, API keys, or token storage detected.
- User settings are stored in the local SQLite `UserSettings` table.

## Integration Risk Summary

- Operational exposure is mostly local: SQLite durability/migrations, installation/upgrade behavior, registry access, process termination, tray lifecycle, and single-instance IPC.
- Low external-service risk because the app has no detected network dependency.
- Installer and runtime assume Windows-specific APIs and paths; cross-platform execution is not a current target.

---

*Integration analysis refreshed: 2026-05-09*
