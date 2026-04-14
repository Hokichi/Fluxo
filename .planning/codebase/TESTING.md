# Testing Patterns

**Analysis Date:** 2026-04-14

## Test Framework

**Status:** Not Detected

No unit test projects (.Test.csproj or xUnit/NUnit/MSTest references) are present in the solution. The codebase is a production WPF application with no apparent automated test infrastructure.

**Project Structure:**
- Solution file: `Fluxo.slnx`
- Production projects only:
  - `Fluxo.csproj` - Main WPF application
  - `Fluxo.Core.csproj` - Core business logic and interfaces
  - `Fluxo.Data.csproj` - Data access layer
  - `Fluxo.Services.csproj` - Services layer

**Build Configuration:**
- Target Framework: `.NET 10.0` (net10.0-windows)
- WPF enabled: `<UseWPF>true</UseWPF>`
- No test-related package references in any .csproj file

## Test Coverage

**Current State:** No tests

**Coverage Target:** Not defined

There is no coverage reporting, test configuration file (xunit.json, or app.config for NUnit), or CI/CD pipeline that would enforce coverage requirements.

## Testing Approach

### Manual Testing Areas

Since no automated tests exist, the following areas would need manual verification:

**ViewModel Behavior:**
- Observable property changes trigger UI updates correctly
- Command relay methods execute and update state
- CollectionView filtering and sorting functions
- Date navigation and period selection logic
- Tag management and promotion in UI

**Data Access:**
- Repository queries return correct filtered results
- Unit of Work transaction scopes work correctly
- Database migrations execute without errors
- Navigation properties load correctly with `.Include()` statements

**Business Logic:**
- Expense categorization (Needs/Wants/Invest) calculations
- Daily/weekly/monthly aggregations
- Saving goal progress calculations
- Spending source balance tracking
- Budget threshold warnings

**Integration Points:**
- Startup wizard flow and first-run logic
- File system interactions (database location)
- Settings persistence to database
- Expense cleanup and deletion workflows

### Observable Property Testing Pattern

If unit tests were to be added, the Community Toolkit MVVM pattern would be verified as:

```csharp
// Example pattern for ViewModel testing (currently not implemented)
// Would test [ObservableProperty] auto-generated backing fields
var vm = new MainVM(...);
vm.Username = "NewName";
Assert.Equal("NewName", vm.Username);
// PropertyChanged event verification via ObservableObject.OnPropertyChanged
```

### Repository Testing Pattern

If unit tests were to be added, repositories would be tested with mock DbContext:

```csharp
// Example pattern for Repository testing (currently not implemented)
public class ExpenseLogRepositoryTests
{
    [Fact]
    public async Task GetByDayAsync_WithValidDate_ReturnsCorrectRecords()
    {
        // Arrange
        var dbContext = CreateMockDbContext();
        var repository = new ExpenseLogRepository(dbContext);
        var testDate = new DateTime(2026, 4, 14);
        
        // Act
        var result = await repository.GetByDayAsync(testDate);
        
        // Assert
        Assert.NotEmpty(result);
        Assert.All(result, log => 
            Assert.True(log.DeductedOn.Date == testDate));
    }
}
```

## Key Testable Components

**Interfaces for Dependency Injection:**
- `IUnitOfWork` - Can be mocked for ViewModel testing
- `IRepository<T>`, `IReadRepository<T>`, `IWriteRepository<T>` - Can be mocked
- `IExpenseRepository`, `IExpenseLogRepository` - Specialized repository interfaces
- `IViewModelReadUnitOfWork<...>`, `IViewModelWriteUnitOfWork<...>` - Complex generic interfaces

**Converter Testing:**
All converters in `Fluxo/Converters/` implement `IValueConverter` or `IMultiValueConverter`:
- `BoolToVisibilityConverter` - Simple bool-to-Visibility conversion
- `BoolToNotificationIconConverter` - Bool to geometry resource lookup
- `DateTimeToRelativeDateConverter` - DateTime formatting
- `NumberWithCommasConverter` - Decimal to formatted string
- `ProgressToArcGeometryConverter` - Numeric progress to WPF geometry
- `BrushToLighterBrushConverter` - Color manipulation with error handling
- `CornerRadiusConverter` - Multi-value conversion for border clipping

These would be testable with `Convert()` method unit tests.

**Query Methods on Repositories:**
All repositories follow a consistent async query pattern:
- `GetAllAsync()` - All records
- `GetByIdAsync(int id)` - Single record by key
- `GetByDayAsync(DateTime day)` - ExpenseLog only
- `GetByWeekAsync(DateTime start, DateTime end)` - Date range filtering
- `GetByMonthAsync(int month)` - Month-based filtering
- `GetByCategoryAsync(ExpenseCategory category)` - Enum-based filtering
- `GetBySpendingSourceIdAsync(int id)` - Foreign key filtering
- `GetTagsByCountDescendingAsync()` - Tuple return with aggregation

## Error Handling in Components

**Converter Error Patterns:**
```csharp
// From BorderCornerClipConverter.cs - validates input parameters
public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
{
    if (values[0] is not CornerRadius cornerRadius ||
        !double.TryParse(values[1].ToString(), out var width) ||
        !double.TryParse(values[2].ToString(), out var height))
        throw new Exception("Invalid Parameters");
    // ... conversion logic
}
```

**Repository Error Patterns:**
```csharp
// From LogMemoryActions.cs - explicit error checks
private ExpenseTag GetExpenseTag(int expenseTagId)
{
    return expenseTag ?? throw new InvalidOperationException($"Unable to find expense tag {expenseTagId}.");
}
```

## Dependency Injection & Mocking

**Service Registration Pattern:**
All components are registered via ServiceCollection extensions, making them mockable:

```csharp
// From ServiceCollectionExtensions.cs
services.AddTransient<IRepository<Expense>, ExpenseRepository>();
services.AddTransient<IUnitOfWork>(_ => new UnitOfWork(dbContext, ...));
```

This pattern allows tests to substitute implementations:
```csharp
// Test pattern (not currently implemented)
var mockRepository = new Mock<IExpenseRepository>();
services.AddTransient<IExpenseRepository>(_ => mockRepository.Object);
```

## Async/Await Testing Patterns

All data access methods are async and require CancellationToken parameter. Testing pattern would be:

```csharp
// Example pattern for async testing (currently not implemented)
[Fact]
public async Task GetByIdAsync_WithValidId_ReturnsEntity()
{
    // Arrange
    var repository = new ExpenseRepository(dbContext);
    var expenseId = 1;
    
    // Act
    var result = await repository.GetByIdAsync(expenseId, CancellationToken.None);
    
    // Assert
    Assert.NotNull(result);
    Assert.Equal(expenseId, result.Id);
}
```

## Integration Testing Points

**Startup & Initialization:**
- App.xaml.cs `OnStartup()` method contains error handling for initialization
- First-run detection via UserSettings
- Wizard popup display logic
- Service provider bootstrap

**Service Composition:**
- Extensions methods chain multiple registrations
- Generic repository factory patterns for ViewModel<->Entity mapping
- Multi-interface implementation pattern requiring careful wiring

**Data Flow End-to-End:**
- UI command triggers ViewModel method
- ViewModel uses injected repositories
- Repository queries EF Core DbContext
- Results mapped via AutoMapper to ViewModels
- Changes propagate back through Unit of Work SaveChangesAsync()

## CI/CD & Automation

**Status:** Not Detected

- No `.github/workflows/` directory
- No GitHub Actions configuration
- No AppVeyor, Azure Pipelines, or other CI config files
- No pre-commit hooks observed
- No test runner scripts in solution

## Recommendations for Adding Tests

If testing is to be introduced, recommended approach:

1. **Start with Repository Layer:**
   - xUnit or NUnit for test framework
   - Entity Framework In-Memory database or SQLite for test contexts
   - Test each repository's query methods
   - Validate eager loading with `.Include()` chains

2. **ViewModel Unit Tests:**
   - Mock IUnitOfWork and repositories
   - Verify ObservableObject property change notifications
   - Test RelayCommand execution
   - Verify state transitions

3. **Converter Unit Tests:**
   - Simple test cases for each conversion
   - Edge cases and invalid input handling
   - Resource lookup verification for icon converters

4. **Integration Tests:**
   - Full service composition with test database
   - End-to-end startup sequence
   - Data persistence and retrieval cycles

