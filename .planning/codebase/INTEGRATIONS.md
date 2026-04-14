# External Integrations

**Analysis Date:** 2026-04-14

## APIs & External Services

**Not detected:** No external APIs or cloud services are currently integrated into the application.

The application is a standalone desktop finance management tool with no outbound API integrations (no Stripe, PayPal, banking APIs, etc.).

## Data Storage

**Database:**
- SQLite (local file-based)
  - Provider: `Microsoft.EntityFrameworkCore.Sqlite` 10.0.5
  - Location: `{AppContext.BaseDirectory}/fluxo.db`
  - Connection Factory: `Fluxo.Data/Context/FluxoDbContextFactory.cs`
  - Context: `Fluxo.Data/Context/FluxoDbContext.cs`
  - Tables: Expenses, ExpenseLogs, IncomeLogs, ExpenseTags, SavingGoals, SpendingSources, UserSettings

**File Storage:**
- Local filesystem only
  - Database file in application directory
  - No cloud storage integrations

**Caching:**
- None detected

## Authentication & Identity

**Auth Provider:**
- Custom/Internal only
- No external authentication provider integrated
- User settings stored in local UserSettings table
- First-run detection via UserSettings.IsFirstRun flag

**Implementation:**
- User management via `Fluxo.Core/Entities/UserSettings` entity
- Settings persisted to SQLite UserSettings table

## Monitoring & Observability

**Error Tracking:**
- None detected (no Sentry, Rollbar, Application Insights, etc.)
- Errors logged to file and console

**Logs:**
- Serilog 4.3.1 framework
- File sink: `Serilog.Sinks.File` 7.0.0
- Structured logging (Serilog format)
- Configured in `Fluxo/App.xaml.cs` startup

**Exception Handling:**
- Standard try-catch in App startup with MessageBox error display
- Async exception handling in async initialization flows

## Notifications

**Windows Notifications:**
- `Microsoft.Toolkit.Uwp.Notifications` 7.1.3 (`Fluxo.Services/`)
- Used for toast notifications in Windows notification center
- In-app notification system with NotificationSeverity levels

**Notification UI:**
- In-app notification display in ViewModels (`Fluxo/ViewModels/Notifications/NotificationItemVM.cs`)
- Converters for notification icons (`Fluxo/Converters/BoolToNotificationIconConverter.cs`)

## CI/CD & Deployment

**Hosting:**
- Standalone Windows desktop application (WinExe)
- No cloud deployment required
- Distributed as compiled executable

**CI Pipeline:**
- Not detected in repository

## Environment Configuration

**Required env vars:**
- None detected (no external service configuration)

**Secrets location:**
- Not applicable - no external API keys or secrets needed
- All configuration internal to application

## Webhooks & Callbacks

**Incoming:**
- None - Desktop application, no server component

**Outgoing:**
- None detected - No external service integrations

## Data Exchange

**Import/Export:**
- Not implemented in current codebase
- Application operates entirely on local SQLite database

**Synchronization:**
- Not applicable - Single-machine desktop application

---

*Integration audit: 2026-04-14*
