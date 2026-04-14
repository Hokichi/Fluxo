# Quick Task 260414-amz Summary

**Task:** StartupWizard: skip Fixed expenses and Saving goals when no spending sources on back navigation
**Date:** 2026-04-14
**Commit:** 3d7c56c

## What Was Done

**`Fluxo/ViewModels/Popups/StartupWizardVM.cs`:**
- `GoBack()` now checks: if on step 5 (Budget Allocation) and `HasSpendingSources` is false, jump to step 2 (Spending Sources) instead of step 4 (Saving Goals)

**`Fluxo/Views/Popups/StartupWizardPopup.xaml.cs`:**
- `OnDotClick` now blocks navigation to step 3 (Fixed Expenses) or step 4 (Saving Goals) when `HasSpendingSources` is false

## Status: Complete
