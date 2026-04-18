# External Integrations

**Analysis Date:** 2026-04-18

## APIs & External Services

**Third-party HTTP APIs:**
- None detected. No `HttpClient`, `RestClient`, or `HttpRequestMessage` usage anywhere in the codebase.

**Cloud SDKs:**
- None detected (no AWS, Azure, GCP, Stripe, Supabase, or similar SDKs referenced in any `.csproj`).

The application is a fully offline Windows desktop product; it does not call out to any remote APIs.

## Data Storage

**Databases:**
- SQLite (local file)
  - Provider: `Microsoft.EntityFrameworkCore.Sqlite` 10.0.5 (`Fluxo.Data/Fluxo.Data.csproj`)
  - DbContext: `Fluxo.Data/Context/FluxoDbContext.cs`
  - Connection construction: `Fluxo.Data/Context/FluxoDbContextFactory.cs` -> `Data Source={AppContext.BaseDirectory}\fluxo.db`
  - Migrations assembly: `Fluxo` (configured in `FluxoDbContextFactory.CreateDbContext`)
  - Migration files: `Fluxo/Migrations/` (runtime), `Fluxo.Data/Migrations/` (initial set)
  - DbSets exposed: `Expenses`, `ExpenseLogs`, `IncomeLogs`, `ExpenseTags`, `SavingGoals`, `SpendingSources`, `UserSettings`
  - Connection string: not externalized; built in code only. No env var.

**File Storage:**
- Local filesystem only. SQLite file (`fluxo.db`) lives next to the executable.

**Caching:**
- None detected.

## Authentication & Identity

**Auth Provider:**
- None. Single-user local desktop application; no login, no identity provider, no token handling.
- "First run" flag is stored locally in the `UserSettings` table; key defined in `Fluxo.Core/Constants/UserSettingNames.cs` (`UserSettingNames.IsFirstRun`) and consumed by `Fluxo/App.xaml.cs` `EnsureFirstRunSettingAsync`.

## Monitoring & Observability

**Error Tracking:**
- None. Uncaught startup exceptions surface to the user via `IDialogService.ShowError` or `FluxoMessageBox.Show` from `Fluxo/App.xaml.cs`.

**Logs:**
- `Serilog` 4.3.1 and `Serilog.Sinks.File` 7.0.0 are referenced in `Fluxo/Fluxo.csproj`, but no `LoggerConfiguration`, `WriteTo`, or `using Serilog` usages were found in source. The packages appear unwired.
- DI uses `Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance` for AutoMapper (`Fluxo/Extensions/ServiceCollectionExtensions.cs`), explicitly silencing logs.

## CI/CD & Deployment

**Hosting:**
- Distributed as a Windows desktop executable (`<OutputType>WinExe</OutputType>` in `Fluxo/Fluxo.csproj`). No hosted backend.

**CI Pipeline:**
- None detected. No `.github/workflows`, Azure Pipelines, or other CI configuration files in the repository.

## Environment Configuration

**Required env vars:**
- None. The application does not read environment variables for configuration. SQLite path is derived from `AppContext.BaseDirectory` at runtime.

**Secrets location:**
- No secrets, tokens, or API keys are loaded by the application. No `.env` files present.

## Webhooks & Callbacks

**Incoming:**
- None. The app exposes no HTTP endpoints or listeners.

**Outgoing:**
- None. No webhook posts or external callbacks.

## Desktop / OS Integrations

**Windows Toast Notifications:**
- Package: `Microsoft.Toolkit.Uwp.Notifications` 7.1.3 (`Fluxo.Services/Fluxo.Services.csproj`).
- No `ToastContentBuilder` / `ShowToast` usage detected in source files; package is referenced but appears unused at present.

**Windows Fonts / Resources:**
- Embedded SFT Schrifted Round TTF font family loaded as WPF resources via `Fluxo/App.xaml` and packaged in `Fluxo/Resources/Fonts/`.

**Icon Pack:**
- `MahApps.Metro.IconPacks` 6.2.1 - Icon set used in WPF views, declared in `Fluxo/Fluxo.csproj`.

---

*Integration audit: 2026-04-18*
