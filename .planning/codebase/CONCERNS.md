# Codebase Concerns

**Analysis Date:** 2026-04-14

## Tech Debt

### Decimal Storage as TEXT in Database

**Issue:** Decimal currency values are stored as TEXT columns in the SQLite database instead of using proper numeric types (REAL or NUMERIC).

**Files:**
- `Fluxo.Data/Context/FluxoDbContext.cs` (lines 35, 54, 75, 101, 102, 112, 113, 114, 117)
  - Amount fields in Expense, ExpenseLog, IncomeLog stored as TEXT
  - Budget fields in SavingGoal (TargetAmount, CurrentAmount) stored as TEXT
  - SpendingSource balance fields (AccountLimit, SpentAmount, Balance, InterestRate) stored as TEXT

**Impact:** 
- Queries cannot use numeric comparisons or aggregations efficiently
- Type conversions required every time data is read
- Risk of storage corruption from invalid text values
- Performance penalty on financial calculations and reporting
- Inconsistent with domain (financial values should be numeric)

**Fix approach:** 
Create migrations to convert TEXT columns to NUMERIC type with appropriate precision. Add validation to ensure all existing data converts successfully. Requires careful testing with actual decimal values to avoid rounding issues during migration.

---

### Oversized ViewModels (God Objects)

**Issue:** Main application ViewModels have grown significantly, making them difficult to maintain and test.

**Files:**
- `Fluxo/ViewModels/Shell/MainVM.cs` (1,384 lines)
  - Handles: dashboard metrics, data loading, filtering, notifications, tag management, expense tracking, day/week/month/all-time views, spending source management
  - Contains 30+ observable properties for different UI states
  - Multiple nested methods handling different data transformations

- `Fluxo/ViewModels/Popups/SettingsVM.cs` (1,302 lines)
  - Handles: user preferences, budget allocation, notification settings, spending source visibility, fixed expense visibility
  - Complex property interdependencies with cascading validations

- `Fluxo/ViewModels/Popups/StartupWizardVM.cs` (783 lines)
  - Manages 10-step wizard with complex step transitions, currency selection, budget allocation

**Impact:**
- Difficult to understand control flow
- High risk of unintended side effects when modifying logic
- Testing individual features requires setting up entire ViewModel state
- Maintenance burden: finding and fixing bugs requires navigating large files

**Fix approach:**
Extract concerns into smaller, focused ViewModels:
- Separate dashboard metrics calculation from view coordination
- Extract budget allocation validation into dedicated service
- Create separate ViewModels for expense filtering, tag management, notification management
- Use composition over inheritance for shared behavior

---

### Reflection-Based Entity Tracking in Repository

**Issue:** Repository class uses runtime reflection to access entity IDs for tracking conflicts.

**Files:**
- `Fluxo.Data/Repositories/Repository.cs` (lines 10, 41-48, 60-67, 80-86)
  - `IdProperty` obtained via reflection on generic type parameter
  - Multiple calls to `GetValue()` and `FindTrackedEntity()` per operation

**Impact:**
- Performance overhead with reflection calls on every Update/Remove operation
- Fragile: breaks if entity doesn't have "Id" property (no compile-time check)
- Complexity hidden in base repository class

**Fix approach:**
Use a generic constraint requiring an interface with Id property, or move tracking logic into specialized repositories that know the exact entity types. Alternatively, use a static cache to store PropertyInfo lookups per type.

---

## Known Bugs / TODOs

### Empty Catch Block in MainWindow

**Issue:** DragMove() operation swallows all exceptions silently without logging or recovery.

**Files:**
- `Fluxo/Views/Shell/MainWindow.xaml.cs` (lines 81-87)
  ```csharp
  try
  {
      DragMove();
  }
  catch (Exception exception)
  {
  }
  ```

**Problem:** 
- No visibility into drag failures
- Could mask legitimate errors (e.g., invalid window state)
- Silent failures make debugging difficult

**Recommendation:** Either remove the try-catch if drag failures are acceptable, or log the exception and provide a fallback behavior.

---

### Potential Null Reference Issues in Day Selection

**Issue:** Multiple fallback chains for day selection could introduce null reference bugs if DaysOfWeek collection is empty.

**Files:**
- `Fluxo/ViewModels/Shell/MainVM.cs` (lines 372, 604, 609)
  ```csharp
  var selectedItem = SelectedDay ?? DaysOfWeek.FirstOrDefault() ?? new DayOfWeekVM { Date = DateTime.Today };
  ```

**Problem:**
- If DaysOfWeek is empty, creates anonymous DayOfWeekVM without proper initialization
- Fallback objects may not have all required properties set

**Risk:** Low if DaysOfWeek collection is guaranteed to be populated during initialization, but defensive programming would be safer.

---

## Security

### No Input Validation on User Settings

**Issue:** User settings and preferences from UI are persisted to database without validation.

**Files:**
- `Fluxo/ViewModels/Popups/SettingsVM.cs` - handles preferred app name, currency selection
- `Fluxo.Services/Persistence/ExpenseCleanupService.cs` - deletes expense logs based on marked state

**Current mitigation:** 
- TextBox controls in XAML may have built-in constraints
- Budget allocation validated to sum to 100%

**Recommendations:**
- Add explicit validation on PreferredAppName (length, special characters)
- Validate currency codes against known ISO 4217 list
- Add audit logging for sensitive operations (deletions, settings changes)
- Implement transaction rollback for cleanup service if partial deletion occurs

---

## Performance

### Full Data Reload on View Mode Change

**Issue:** Switching between daily/weekly/monthly/all-time views reloads ALL data for that period type.

**Files:**
- `Fluxo/ViewModels/Shell/MainVM.cs` (lines 226-230, 416-454)
  - OnSelectedMainContentViewModeChanged calls LoadAllTimeData() or navigates with full reload
  - Initialize() loads 8 full entity collections upfront

**Impact:**
- No pagination or lazy loading
- UI may freeze during data load with large datasets
- All-time view loads every transaction in memory
- Mobile/low-end hardware will struggle

**Bottlenecks:**
1. `GetAllAsync()` on ExpenseLogs, IncomeLog (no period filtering at query level)
2. `CacheAllTimeExpenseTotals()` iterates entire log collection
3. No indexed queries for date ranges

**Improvement path:**
- Implement server-side filtering (GetByMonthAsync, GetByWeekAsync work but GetAllAsync doesn't filter)
- Add pagination for all-time view
- Cache period summaries instead of loading every transaction
- Use compiled LINQ queries for date filtering

---

### No Query Optimization for UI-Intensive Operations

**Issue:** Collection filtering and sorting happens in memory after full load.

**Files:**
- `Fluxo/ViewModels/Shell/MainVM.cs` - filters expenses, income, saving goals in-memory
- Multiple LINQ queries applied after GetAllAsync()

**Problem:**
- Database returns all records, then C# filters in memory
- ObservableCollection operations trigger UI updates for each addition
- No indexes on commonly-filtered fields (Date, Category, SpendingSourceId)

**Recommendation:** Move filtering to query layer where possible. Use DbContext.Local for already-loaded entities but query database for initial loads.

---

### Event Handler Attachment/Detachment in Collection Change

**Issue:** Spending sources collection change handlers are attached/detached for every collection replacement.

**Files:**
- `Fluxo/ViewModels/Shell/MainVM.cs` (lines 232-252)
  - OnSpendingSourcesChanged detaches old handlers and attaches new ones
  - RefreshDashboardMetrics() called on every collection change

**Impact:**
- Frequent event subscription/unsubscription overhead
- Potential for lingering references if detachment fails
- Multiple property change notifications cascade

**Mitigation:** Use weak event patterns or consolidate notifications into batch updates.

---

## Fragile Areas

### Complex State Management in StartupWizardVM

**Issue:** Wizard maintains interdependent state across 10 steps with complex navigation rules.

**Files:**
- `Fluxo/ViewModels/Popups/StartupWizardVM.cs` (783 lines)
  - OnCurrentStepIndexChanged manually updates 10+ other properties
  - Budget validation in NeedsAllocationPercentageChanged, WantsAllocationPercentageChanged, InvestAllocationPercentageChanged
  - Step visibility rules scattered across multiple boolean properties

**Why fragile:**
- Adding new step or property requires updates in 5+ places
- Manual property change synchronization error-prone
- No state machine to enforce valid transitions
- Hard to test step interactions

**Safe modification:**
- Use state machine pattern (e.g., Stateless library)
- Extract step definitions into data-driven configuration
- Centralize property change cascading logic

---

### Expense Log Deletion with Soft Delete Pattern

**Issue:** Uses IsForDeletion flag (soft delete) but also performs actual deletion cleanup asynchronously.

**Files:**
- `Fluxo/ViewModels/Shell/MainVM.cs` - marks logs as IsForDeletion
- `Fluxo.Services/Persistence/ExpenseCleanupService.cs` - performs actual deletion
- `Fluxo/Views/Shell/MainWindow.xaml.cs` - triggers cleanup on window close

**Problem:**
- Soft-deleted logs still appear in queries unless filtered
- Async cleanup may fail silently without user notification
- If cleanup fails, orphaned logs accumulate
- Related expenses are only deleted if no other logs reference them

**Risk:** Data corruption if cleanup service fails mid-operation.

**Fix approach:**
- Use transactions for cleanup service operations
- Add logging/monitoring for failed deletions
- Consider hard delete immediately with undo via LogMemoryManager
- Implement cascade delete constraint at database level

---

### MainWindow Complex Animation and State Management

**Issue:** MainWindow handles window chrome, animations, popup overlay, and event routing.

**Files:**
- `Fluxo/Views/Shell/MainWindow.xaml.cs` (652 lines)
  - Manages window minimize/maximize/restore with custom animations
  - Blur overlay for popups
  - Drag move, keyboard shortcuts, system commands
  - Complex state variables: _isMaximized, _wasMinimized, _isClosing, etc.

**Why fragile:**
- WPF animation state can get out of sync with actual window state
- Multiple event handlers (Closing, Deactivated, StateChanged, PreviewKeyDown) interact
- DragMove() blocked by try-catch, making drag failures silent
- Popup overlay blur state must be manually synchronized

**Safe modification:**
- Extract animation coordination into separate class
- Use behaviors for window chrome management
- Implement state machine for window states
- Add logging for animation completion/failure

---

### ExpenseCleanupService Two-Phase Deletion

**Issue:** Cleanup service uses two separate UnitOfWork instances without transaction.

**Files:**
- `Fluxo.Services/Persistence/ExpenseCleanupService.cs` (lines 15-53)
  - Phase 1: Fetch logs, delete marked logs, save (lines 15-33)
  - Phase 2: Fetch remaining logs, fetch expenses, delete orphaned expenses, save (lines 38-53)

**Problem:**
- If Phase 2 fails, Phase 1 deletions are already persisted
- No rollback mechanism between phases
- Two separate database connections = potential consistency issues
- If logs are added between phases, may delete too much

**Risk:** Data loss if process crashes between phases.

**Fix approach:**
- Wrap entire operation in single DbContext transaction
- Use DbSet.Local to avoid re-querying already-loaded entities
- Add error handling with rollback capability
- Log each phase completion for monitoring

---

## Recommendations

### Priority 1: Critical Issues

1. **Remove Empty Catch Block** (`MainWindow.xaml.cs`)
   - Either handle DragMove failure gracefully or remove try-catch and let WPF handle it
   - If exceptions are expected, add logging
   - Effort: 30 minutes

2. **Add Transactional Safety to ExpenseCleanupService**
   - Wrap deletion phases in single transaction
   - Add rollback on failure
   - Effort: 2 hours

3. **Validate User Input on Settings Changes**
   - Add validators for PreferredAppName, currency codes
   - Effort: 1.5 hours

---

### Priority 2: Important Tech Debt

1. **Convert Decimal Storage from TEXT to NUMERIC** (1-2 weeks planning, 1-2 days implementation)
   - Create migration with data conversion
   - Update DbContext configuration
   - Test with various decimal values

2. **Implement Pagination for All-Time View** (3-5 days)
   - Add page size limit
   - Implement virtual scrolling or paging controls
   - Add progress indicator for large datasets

3. **Refactor Large ViewModels** (2-3 weeks)
   - Start with MainVM: extract metrics calculation, tag management
   - Reduce from 1,384 to <500 lines through composition
   - Add unit tests for extracted services

---

### Priority 3: Maintainability Improvements

1. **Add State Machine to StartupWizardVM** (3-4 days)
   - Use Stateless library for step transitions
   - Reduce manual state synchronization
   - Make step definitions data-driven

2. **Extract Window Chrome Management** (2-3 days)
   - Move animation logic to separate class
   - Use behaviors for state synchronization
   - Improve testability

3. **Implement Query Logging** (1-2 days)
   - Add EF Core query logging to detect N+1 issues
   - Monitor query execution time
   - Identify slow queries for optimization

---

### Priority 4: Documentation

- Document soft-delete cleanup flow with sequence diagrams
- Add comments to complex state transitions in ViewModels
- Document database schema constraints and migration risks

---

*Concerns audit: 2026-04-14*
