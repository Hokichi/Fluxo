# Codebase Concerns

**Analysis Date:** 2026-04-18

## Tech Debt

**Forced application exit on window close:**
- Issue: `MainWindow.OnWindowClosing` calls `Environment.Exit(1)` inside a `finally` block. Cancelling the close (`e.Cancel = true`) and then unconditionally exiting with code `1` bypasses the normal WPF shutdown sequence. The captured `markedIds` value is never used.
- Files: `Fluxo/Views/Shell/Main/MainWindow.xaml.cs:151-169`
- Impact: Skips graceful shutdown for other windows / popups, prevents `App.OnExit` and `IDisposable` cleanup on services, returns a non-zero exit code as if the process crashed, and leaves dead code (`markedIds`).
- Fix approach: Run the deletion cleanup work, then call `Application.Current.Shutdown(0)` (or `base.OnClosing`) without cancelling the event after the work completes. Use `e.Cancel` only while async work is in flight and re-trigger `Close()` afterwards.

**Migrations split across two projects:**
- Issue: EF Core migrations live in two distinct directories with overlapping responsibilities — newer migrations were added to the WPF host project while initial migrations live in the data project.
- Files: `Fluxo.Data/Migrations/` (4 migrations + snapshot), `Fluxo/Migrations/` (8 migrations + snapshot + `FluxoDesignTimeDbContextFactory.cs`)
- Impact: Two `FluxoDbContextModelSnapshot.cs` files exist (`Fluxo/Migrations/FluxoDbContextModelSnapshot.cs`, `Fluxo.Data/Migrations/FluxoDbContextModelSnapshot.cs`) and can drift; `MigrationsAssembly("Fluxo")` in `FluxoDbContextFactory` only points at the WPF assembly so the four `Fluxo.Data` migrations may be ignored at runtime.
- Fix approach: Consolidate all migrations into one project (`Fluxo.Data` is the natural home), delete the duplicate snapshot, and confirm `MigrationsAssembly` targets the consolidated project.

**Duplicate `IUnitOfWork` registration with manual factory:**
- Issue: `AddFluxoData` registers `IUnitOfWork` via a hand-rolled factory that `new`s a fresh `FluxoDbContext` (bypassing the `FluxoDbContext` registration on line 14) and instantiates each repository directly, even though all repositories are also registered in DI immediately after.
- Files: `Fluxo.Data/Extensions/ServiceCollectionExtensions.cs:14-44`
- Impact: Two parallel DbContext instances per resolution path (one for `IUnitOfWork`, one for any direct `FluxoDbContext` injection), duplicate change tracking, possible "entity already tracked" errors when a single operation pulls services that resolve through different paths. Repository registrations on lines 31-44 are effectively dead code for callers that go through `IUnitOfWork`.
- Fix approach: Register `FluxoDbContext` with `AddDbContext`, register `IUnitOfWork` as `AddScoped`/`AddTransient` resolving from DI (`sp => new UnitOfWork(sp.GetRequiredService<FluxoDbContext>(), ...)`) so all collaborators share one context.

**`IUnitOfWork` resolved at construction time and held forever:**
- Issue: `App.xaml.cs` resolves a single `IUnitOfWork` in the constructor and stores it on `MainWindow`, `LogMemoryManager`, `MainVM`, popup ViewModels, etc. for the lifetime of the app. Combined with the transient registration above, this means one `FluxoDbContext` lives for the entire process.
- Files: `Fluxo/App.xaml.cs:36`, `Fluxo/Views/Shell/Main/MainWindow.xaml.cs:36,69`, `Fluxo/Views/Shell/Main/MainWindow.xaml.cs:509,518,556,582,630`
- Impact: Unbounded change-tracker growth, stale data when external writes occur, defeats the purpose of `IDisposable`/`IAsyncDisposable` on `IUnitOfWork`. New ViewModels (`new QuickAddVM(_mainVM, _unitOfWork)`) reuse the long-lived context too.
- Fix approach: Introduce a `IUnitOfWorkFactory` (or use `IDbContextFactory<FluxoDbContext>`) and create + dispose a fresh unit of work per logical operation/popup.

**Empty `catch` swallowing exceptions in money parsing and brush conversion:**
- Issue: Multiple converter helpers swallow exceptions silently, masking parse/format problems.
- Files: `Fluxo/Converters/MoneyFormatUtility.cs:174-182` (FormatException, InvalidCastException, OverflowException all empty), `Fluxo/Converters/BrushToLighterBrushConverter.cs:51-56`, `Fluxo/Views/Shell/Main/MainWindow.xaml.cs:113-115` (DragMove)
- Impact: Bad input passes through as default values; `exception` parameter on `MainWindow.xaml.cs:113` is unused (compile warning candidate). Bugs become invisible.
- Fix approach: At minimum log via Serilog (already referenced) or surface a debug-time `Trace.WriteLine`; for converters return `Binding.DoNothing` instead of swallowing.

**Repetitive `throw new NotImplementedException()` in `IValueConverter.ConvertBack`:**
- Issue: 12 converters throw `NotImplementedException` from `ConvertBack`. While intentional for one-way bindings, the consistent boilerplate hides true unimplemented work and breaks if any binding direction is changed.
- Files: `Fluxo/Converters/BoolToVisibilityConverter.cs:16,29`, `Fluxo/Converters/BorderCornerClipConverter.cs:27`, `Fluxo/Converters/BrushToColorConverter.cs:19`, `Fluxo/Converters/BrushToLighterBrushConverter.cs:30`, `Fluxo/Converters/CornerRadiusConverter.cs:19`, `Fluxo/Converters/DateTimeToRelativeDateConverter.cs:28`, `Fluxo/Converters/MoneyDisplayConverter.cs:23`, `Fluxo/Converters/MoneyFullDisplayConverter.cs:15`, `Fluxo/Converters/NumberWithCommasConverter.cs:15`, `Fluxo/Converters/ObjectToNullConverter.cs:15,28`, `Fluxo/Converters/ProgressToArcGeometryConverter.cs:65`, `Fluxo/Converters/TagIconNameToGeometryConverter.cs:21`
- Impact: A `OneWay` binding accidentally turning into `TwoWay` (or `Mode=Default` on a TextBox) crashes at runtime instead of degrading.
- Fix approach: Return `Binding.DoNothing` (or `DependencyProperty.UnsetValue`) and document one-way intent in XML doc comments.

**Massive `MainVM` god class (1,059 lines):**
- Issue: `MainVM` owns dashboard metrics, expense filters, tag management, spending source aggregation, notifications routing, saving goals, fixed-expense auto-deduction, and user-settings persistence in one partial class.
- Files: `Fluxo/ViewModels/Shell/MainVM.cs` (1,059 lines), partial in `Fluxo/ViewModels/Shell/MainVM.ExpenseDetailMessenger.cs`
- Impact: Hard to reason about, hard to test (tests only exist for sub-VMs in `Fluxo.Tests/ViewModels/Shell/Main/`), every dashboard panel depends on it, registered as singleton so it accumulates state.
- Fix approach: Extract dedicated services (e.g. `IFixedExpenseAutoApplier`, `INotificationFactory`, `IUserSettingsCache`) and let `MainVM` orchestrate them. Move per-panel concerns fully into `BudgetAllocationPanelVM`/`NotificationPanelVM`/`SavingGoalsPanelVM`.

**Other oversize files (>700 lines):**
- Files: `Fluxo/ViewModels/Popups/SettingsVM.cs` (1,355 lines), `Fluxo/Services/History/LogMemoryActions.cs` (800 lines), `Fluxo/ViewModels/Popups/StartupWizardVM.cs` (769 lines), `Fluxo/Views/Shell/Main/MainWindow.xaml.cs` (758 lines), `Fluxo/ViewModels/Popups/QuickAddVM.cs` (695 lines)
- Impact: High cognitive load, multiple responsibilities per type, slow incremental compile/test cycles.
- Fix approach: Split `SettingsVM` per tab (budget/personalization/sources/tags/goals), extract each `ILogMemoryAction` implementation into its own file, and move WPF-shell helpers (Win32 monitor lookup, fade animations) out of `MainWindow.xaml.cs` into helpers under `Fluxo/Resources/CustomControls/` or a `WindowChrome` helper.

**Unused NuGet dependencies referenced in `Fluxo.csproj`:**
- Issue: `Serilog`, `Serilog.Sinks.File`, `Newtonsoft.Json`, and `Microsoft.Extensions.Hosting` are referenced but the codebase contains zero `using Serilog`, `Newtonsoft.Json`, or `JsonConvert/JsonSerializer` references.
- Files: `Fluxo/Fluxo.csproj:60-63`
- Impact: Dead binaries shipped, larger download, supply-chain surface for unused packages.
- Fix approach: Either wire Serilog into `App.OnStartup` (replacing the empty catches and `dialogService.ShowError` log path) or remove the references.

**TRIAL fonts shipped in `Resources/Fonts/`:**
- Issue: All bundled fonts are `SFTSchriftedRoundTRIAL-*.ttf` files declared as `<Resource>` in `Fluxo.csproj`.
- Files: `Fluxo/Fluxo.csproj:13-46,73-105`, `Fluxo/Resources/Fonts/`
- Impact: Distributing a TRIAL/eval font in a release build is a licensing violation in most font EULAs.
- Fix approach: Acquire a commercial license or replace with a freely licensed font (e.g. an SIL OFL alternative) and update font family references in XAML.

**`fluxo.db` stored in `AppContext.BaseDirectory`:**
- Issue: SQLite database is created next to the executable rather than in `%LocalAppData%`.
- Files: `Fluxo.Data/Context/FluxoDbContextFactory.cs:21`
- Impact: For installed apps under Program Files, writes will fail due to UAC; per-user data is shared across all users on a machine; uninstall does not remove user data; cannot run multiple installs side by side.
- Fix approach: Use `Environment.SpecialFolder.LocalApplicationData` + product folder, ensure directory exists, optionally migrate existing `fluxo.db` once.

**`*.md` ignored by `.gitignore`:**
- Issue: `*.md` is in `.gitignore`, alongside `*.db`.
- Files: `.gitignore:368`
- Impact: Any markdown documentation (including the `.planning/codebase/` artifacts produced by GSD tooling) cannot be committed without `git add -f`. README/CONTRIBUTING/CHANGELOG would be silently excluded.
- Fix approach: Remove the `*.md` line and instead ignore specific generated docs if needed.

**Stale worktree directory committed/checked in:**
- Issue: A nested worktree directory survives at `.claire/worktrees/wizardly-roentgen/` containing a single `Fluxo.Data/Repositories/IncomeLogRepository.cs`.
- Files: `.claire/worktrees/wizardly-roentgen/Fluxo.Data/Repositories/IncomeLogRepository.cs`
- Impact: Dead source file picked up by recursive globs; confuses search/navigation; appears to be an abandoned worktree from a different tool than `.claude`.
- Fix approach: Delete the `.claire/` directory and add it to `.gitignore` if it is tooling-generated.

**Reference-property warnings on entities (non-nullable strings without initializer):**
- Issue: Entity classes declare non-nullable `string` properties without defaults, e.g. `Expense.Name`, `Expense.SpendingSource`, `ExpenseLog.Notes`, `ExpenseTag.Name/HexCode/IconName`, `SpendingSource.Name`.
- Files: `Fluxo.Core/Entities/Expense.cs:10-12`, `Fluxo.Core/Entities/ExpenseLog.cs:8-12`, `Fluxo.Core/Entities/ExpenseTag.cs:7-9`, `Fluxo.Core/Entities/SpendingSource.cs:8`
- Impact: With `<Nullable>enable</Nullable>` (`Fluxo.csproj:6`) every project-wide build emits CS8618 warnings, training developers to ignore warnings; risk of `null!`-style runtime nulls.
- Fix approach: Default to `string.Empty` (as `UserSettings` already does), or mark navigations as `null!` with explicit comment, or use `required` modifier.

## Known Bugs

**Wizard close confirmation ignores user choice:**
- Symptoms: Closing the startup wizard always proceeds with dismissal regardless of whether the user clicks Yes or No on "Setup isn't finished yet" prompt.
- Files: `Fluxo/Views/Shell/Wizard/StartupWizardPopup.xaml.cs:63-77`
- Trigger: `FluxoMessageBox.Show(...)` is called as a void — its `MessageBoxResult` is discarded — and `_viewModel.DismissAsync()` runs unconditionally afterwards.
- Workaround: None at runtime. The user's "No" is silently ignored.

**`SpendingSourceService.AddIncomeAsync` writes `notes` parameter without recording history:**
- Symptoms: Income added through this service path skips the undo/redo memory action that the QuickAdd flow records.
- Files: `Fluxo.Services/Persistence/SpendingSourceService.cs:85-104` vs `Fluxo/ViewModels/Popups/QuickAddVM.cs:317-323`
- Trigger: Calling the service directly bypasses `WeakReferenceMessenger.Default.Send(new RecordLogMemoryMessage(...))`.
- Workaround: All current callers go through `QuickAddVM`, so the bug is latent — but easy to introduce if the service is called from elsewhere.

**`StartupWizardPopup` confirmation prompts return ignored:**
- Symptoms: Same pattern as above — `FluxoMessageBox.Show` is called without storing or branching on the result on lines `63-67` (close confirmation) and other prompts where the `Yes/No` answer is meant to gate behavior.
- Files: `Fluxo/Views/Shell/Wizard/StartupWizardPopup.xaml.cs:63-67`
- Trigger: User clicks "No" expecting cancellation.
- Workaround: None.

## Security Considerations

**No SQLite encryption / database stored in plaintext:**
- Risk: All user financial data is stored unencrypted in `fluxo.db` next to the executable.
- Files: `Fluxo.Data/Context/FluxoDbContextFactory.cs:19-23`, `Fluxo.Data/Context/FluxoDbContext.cs`
- Current mitigation: None — plain SQLite with no SQLCipher or DPAPI wrapping.
- Recommendations: Either wrap the file with `ProtectedData` (DPAPI) on first run, or move to SQLCipher (`Microsoft.Data.Sqlite` with `EncryptionKey`), or document that data is plaintext at rest.

**Error messages render raw exception text into UI dialogs:**
- Risk: Exception messages may leak internal paths, SQL fragments, stack details to end users.
- Files: `Fluxo/App.xaml.cs:76-79`, `Fluxo/Views/Shell/Wizard/StartupWizardPopup.xaml.cs:47,134`, `Fluxo/Views/Shell/Main/MainWindow.xaml.cs:323-326,639,705,720`, multiple popup `xaml.cs` files
- Current mitigation: None — `exception.Message` is concatenated directly into a user-facing string.
- Recommendations: Log the exception details via Serilog (already referenced) and show a generic message to the user with a correlation ID.

## Performance Bottlenecks

**`SpendingSourceService.DeleteAsync` loads every expense in the database:**
- Problem: When deleting marked spending sources, the service calls `unitOfWork.Expenses.GetAllAsync()` and filters in memory.
- Files: `Fluxo.Services/Persistence/SpendingSourceService.cs:47`
- Cause: Comment says "to avoid a table scan per source" but trades it for a full unfiltered fetch including all navigation auto-includes.
- Improvement path: Replace with `unitOfWork.Expenses.SearchAsync(new ExpenseFilter { SpendingSourceIds = markedIds })` (or add such a method), pushing filtering into SQL.

**`Repository.LoadReferenceNavigationsAsync` recursively walks every reference:**
- Problem: `EnsureReferenceNavigationsLoadedAsync` recursively loads every reference navigation property of an entity returned by `GetByIdAsync`, regardless of whether the caller needs it.
- Files: `Fluxo.Data/Repositories/Repository.cs:92-123`
- Cause: Combined with `ConfigureReferenceAutoIncludes` in `FluxoDbContext.cs:133-149`, every fetch already auto-includes references; this layer adds another pass and a recursive `HashSet<object>` walk on top.
- Improvement path: Drop one of the two mechanisms (auto-include OR explicit recursive load); prefer explicit `.Include()` chains in repositories that need them (as `ExpenseLogRepository.QueryWithNavigations` already does).

**Singleton long-lived `FluxoDbContext` accumulates change tracker:**
- Problem: The single `IUnitOfWork`/`FluxoDbContext` lives for the entire app lifetime and never resets its change tracker.
- Files: `Fluxo/App.xaml.cs:36`, `Fluxo.Data/Extensions/ServiceCollectionExtensions.cs:17-29`
- Cause: See "Tech Debt" — DI registration plus capture-in-constructor pattern.
- Improvement path: Per-operation `IUnitOfWork` (factory) so the change tracker resets between user actions.

**`MainVM.ReloadCurrentDataAsync` re-maps every collection on every change:**
- Problem: Every undo/redo, every save, every panel refresh calls `ReloadCurrentDataAsync` which re-fetches and re-maps `SpendingSources`, `Expenses`, `ExpenseLogs`, `SavingGoals`, `Tags` from the database in series.
- Files: `Fluxo/ViewModels/Shell/MainVM.cs:240-272`
- Cause: No incremental update path; the cheap and easy hammer is "reload everything".
- Improvement path: Apply targeted updates from `ILogMemoryAction` snapshots (the data is already in-memory) and only fall back to full reload on initialization.

## Fragile Areas

**`LogMemoryManager` undo/redo couples to `MainVM` and reloads everything:**
- Files: `Fluxo/Services/History/LogMemoryManager.cs:43-99`
- Why fragile: Every undo/redo requires `MainVM` to be reloadable from scratch, swallows partial failures by re-pushing the action, and uses a shared mutable `_isExecuting` flag without thread sync; concurrent undo/redo from keyboard shortcut + button is possible.
- Safe modification: Wrap `_isExecuting` updates in a lock, return `false` on contention, never call `ReloadCurrentDataAsync` if the action's own undo/redo failed.
- Test coverage: No tests for `LogMemoryManager` itself; only `Fluxo.Tests/ViewModels/Popups/GoalUpdateTransactionSupportTests.cs` and individual action paths via VM tests.

**`async void` event handlers everywhere:**
- Files: `Fluxo/App.xaml.cs:39`, `Fluxo/Views/Shell/Wizard/StartupWizardPopup.xaml.cs:39,54,84,89,144,158,164,170,176,186,199,209,222,232,245`, `Fluxo/Views/Components/IncomeSource.xaml.cs:44,50,56`, `Fluxo/Views/Shell/Main/MainWindow.xaml.cs:151,419,473,479`, all popup `OnSaveButtonClick` overrides (`AddFixedExpensePopup`, `AddNewTransaction`, `AddSavingGoalPopup`, `AddSpendingSourcePopup`, `AddTagPopup`, `ExpenseDetailPopup`, `TransferFundsPopup`, `SettingsPopup`, `SpendingSourceDetailPopup`)
- Why fragile: Unhandled exceptions in an `async void` handler crash the process via `SynchronizationContext`, bypass the `try/catch` patterns elsewhere, and can't be awaited by tests.
- Safe modification: Wrap each handler body in `try/catch` (most do, but not all — e.g. `OnDotClick`, `OnAddSpendingSourceClick`), or migrate to `RelayCommand`/`AsyncRelayCommand` from CommunityToolkit.Mvvm (already a dependency).

**`MainVM` registered as `Singleton` while popups are `Transient`:**
- Files: `Fluxo/Extensions/ServiceCollectionExtensions.cs:46,67-74`
- Why fragile: Popup VMs (`QuickAddVM`, `SettingsVM`, …) capture the singleton `MainVM` and the singleton `IUnitOfWork`; closing/reopening a popup leaves message handlers registered on `WeakReferenceMessenger.Default` and event handlers on `MainVM.SavingGoals/Notifications` collections without explicit unregistration.
- Safe modification: Audit each popup's destructor/`Closing` to ensure handlers detach; consider `IRecipient<T>` with explicit `IsActive=false` lifecycle.

**`MainWindow` mixes Win32 P/Invoke, custom render-loop animation, and DI orchestration:**
- Files: `Fluxo/Views/Shell/Main/MainWindow.xaml.cs:235-308`
- Why fragile: `CompositionTarget.Rendering` handler holds a strong reference; cancellation logic relies on `_renderHandler` mutation not being re-entered; window animation, popup hosting, header menu, keyboard shortcuts, undo/redo, and dispatcher timers all share state.
- Safe modification: Extract animation into a behavior, monitor work-area into a static helper, header-menu state into a small controller class.

## Scaling Limits

**Single SQLite file with no migration safety net:**
- Current capacity: SQLite handles GB-scale data fine for personal finance.
- Limit: With migrations split across two projects (above), upgrading users may end up with an out-of-sync schema. There is no migration-error recovery in `App.OnStartup`.
- Scaling path: Run `Database.Migrate()` on startup with try/catch + backup copy; consolidate migration assemblies first.

**Long-lived in-memory caches in `MainVM`:**
- Current capacity: A few thousand expense logs renders fine.
- Limit: Every reload calls `Concat` on three `ObservableCollection<ExpenseLogVM>` (`Fluxo/ViewModels/Shell/MainVM.cs:230-233`) and re-maps via AutoMapper.
- Scaling path: Page or virtualize; AutoMapper profiles for entity↔DTO↔VM produce three layers of allocations per row (see `Fluxo/Mappings/DtoViewModelProfile.cs`, `Fluxo.Services/Mappings/EntityDtoProfile.cs`).

## Dependencies at Risk

**`MahApps.Metro.IconPacks 6.2.1`:**
- Risk: Pinned to an older 6.x line; current major is 6.x but watch for .NET 10 targeting compatibility.
- Impact: Icon rendering across the app.
- Migration plan: Verify against latest minor release; package only what is referenced (Geometry resources are also defined in `Fluxo/Resources/Icons.xaml`).

**`Microsoft.EntityFrameworkCore.Design 10.0.5` referenced as runtime dependency:**
- Risk: Marked `<PrivateAssets>all</PrivateAssets>` so it is build-time only — fine — but `Microsoft.Extensions.Hosting 10.0.3` is referenced and never used.
- Files: `Fluxo/Fluxo.csproj:56-64`
- Impact: Dead dependency surface.
- Migration plan: Remove `Microsoft.Extensions.Hosting` if no plans to introduce a generic host; otherwise wire `App` through `IHost`.

## Missing Critical Features

**No application-wide logging:**
- Problem: Serilog is referenced but not configured; failures are silently swallowed in catch blocks (see Tech Debt) or shown as raw `MessageBox`es.
- Blocks: Production diagnostics, support workflows, debugging undo/redo issues.

**No global unhandled exception handler:**
- Problem: `App.xaml.cs` does not subscribe to `DispatcherUnhandledException`, `AppDomain.CurrentDomain.UnhandledException`, or `TaskScheduler.UnobservedTaskException`.
- Blocks: Crash-reporting, graceful degradation when an `async void` handler throws.

**No backup / export of `fluxo.db`:**
- Problem: User has no in-app way to back up or export financial data; `DeleteAllDataPopup` exists but no equivalent export.
- Files: `Fluxo/Views/Popups/DeleteAllDataPopup.xaml.cs`
- Blocks: Data portability, recovery from corruption.

## Test Coverage Gaps

**No tests for `Fluxo.Data` (repositories, UnitOfWork, DbContext):**
- What's not tested: All repository implementations, EF query shapes, navigation auto-include behavior, change-tracker conflict handling in `Repository.Update/Remove`.
- Files: `Fluxo.Data/Repositories/*.cs`, `Fluxo.Data/UnitOfWork.cs`, `Fluxo.Data/Context/FluxoDbContext.cs`
- Risk: Schema drifts and EF query bugs surface only at runtime.
- Priority: High.

**No tests for `Fluxo.Services` (persistence services):**
- What's not tested: `ExpenseService.AddAsync` balance arithmetic, `SpendingSourceService.DeleteAsync` cascade restoration, `ExpenseLogService`, `TagService`.
- Files: `Fluxo.Services/Persistence/*.cs`
- Risk: Money arithmetic bugs (balance/SpentAmount drift) are silent.
- Priority: High.

**No tests for `LogMemoryManager` and `ILogMemoryAction` implementations:**
- What's not tested: Undo/redo round-tripping for each action type (12+ implementations).
- Files: `Fluxo/Services/History/LogMemoryManager.cs`, `Fluxo/Services/History/LogMemoryActions.cs`
- Risk: Undo corrupts financial data; no safety net.
- Priority: High.

**Test infrastructure forces ad-hoc `IUnitOfWork` test doubles:**
- What's not tested cleanly: Tests that need a partial `IUnitOfWork` end up reimplementing the full interface with `throw new NotSupportedException()` per repository.
- Files: `Fluxo.Tests/ViewModels/Popups/StartupWizardVMTests.cs:60-83`, `Fluxo.Tests/ViewModels/Popups/GoalUpdateTransactionSupportTests.cs:50-58`
- Risk: Adding a repository property to `IUnitOfWork` breaks every test by requiring a new stub; encourages bypassing tests rather than expanding them.
- Priority: Medium — extract a `TestUnitOfWorkBuilder` or use Moq/NSubstitute (no mocking framework currently referenced).

**No tests for converters or custom controls:**
- What's not tested: `MoneyFormatUtility`, `MoneyDisplayConverter`, `ProgressToArcGeometryConverter`, `BalloonBorder`, `MoneyTextBox`, `SwipeRevealContainer`, `FadingScrollViewer`.
- Files: `Fluxo/Converters/*.cs`, `Fluxo/Resources/CustomControls/*.cs`
- Risk: Money formatting is correctness-critical; silent regressions.
- Priority: Medium.

**No UI / integration tests:**
- What's not tested: End-to-end flows (add expense → see it on dashboard → undo → confirm balance restored).
- Files: `Fluxo.Tests/Views/` only contains `MainWindowShortcutMatcherTests.cs`.
- Risk: WPF binding errors and ViewModel-View wiring regressions.
- Priority: Low (no UI test framework currently set up).

---

*Concerns audit: 2026-04-18*
