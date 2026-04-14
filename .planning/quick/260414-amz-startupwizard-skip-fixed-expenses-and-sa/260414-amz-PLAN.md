---
phase: quick
plan: 260414-amz
type: execute
wave: 1
depends_on: []
files_modified:
  - Fluxo/ViewModels/Popups/StartupWizardVM.cs
  - Fluxo/Views/Popups/StartupWizardPopup.xaml.cs
autonomous: true
---

<objective>
When navigating back from Budget Allocation (step 5) with no spending sources, skip steps 3 and 4 and go directly to Spending Sources (step 2).
Block dot-click navigation to steps 3 and 4 when no spending sources exist.
</objective>

<tasks>

<task type="auto">
  <name>Task 1: Modify GoBack to skip Fixed Expenses/Saving Goals when no spending sources</name>
  <files>Fluxo/ViewModels/Popups/StartupWizardVM.cs</files>
  <action>
In `GoBack()` (line ~167), after the `IsFinalStep` check, add:

```csharp
// When going back from Budget Allocation with no spending sources, skip Fixed Expenses and Saving Goals
if (CurrentStepIndex == 5 && !HasSpendingSources)
{
    CurrentStepIndex = 2;
    return;
}
```

This ensures back navigation from step 5 jumps to step 2 when SpendingSources is empty.
  </action>
  <verify>
    <automated>cd /c/Users/Admins/source/repos/Fluxo && dotnet build Fluxo/Fluxo.csproj --no-restore 2>&1 | tail -5</automated>
  </verify>
  <done>GoBack() from step 5 with no spending sources navigates to step 2. Build succeeds.</done>
</task>

<task type="auto">
  <name>Task 2: Block dot navigation to steps 3/4 when no spending sources</name>
  <files>Fluxo/Views/Popups/StartupWizardPopup.xaml.cs</files>
  <action>
In `OnDotClick` (line ~242), after the `targetStep == _viewModel.CurrentStepIndex` guard, add:

```csharp
// Prevent navigation to Fixed Expenses (3) or Saving Goals (4) when no spending sources
if ((targetStep == 3 || targetStep == 4) && !_viewModel.HasSpendingSources)
    return;
```
  </action>
  <verify>
    <automated>cd /c/Users/Admins/source/repos/Fluxo && dotnet build Fluxo/Fluxo.csproj --no-restore 2>&1 | tail -5</automated>
  </verify>
  <done>Dot clicks to steps 3 and 4 are blocked when HasSpendingSources is false. Build succeeds.</done>
</task>

</tasks>
