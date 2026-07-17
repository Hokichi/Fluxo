<p align="center">
  <img src="docs/images/fluxo-logo.png" alt="Fluxo logo placeholder" width="160">
</p>

Fluxo is a Windows personal finance app for keeping everyday money decisions visible. It helps you track spending, manage accounts, plan around recurring expenses, monitor saving goals, and stay aware of upcoming payments or budget pressure from one desktop dashboard.

![Fluxo dashboard screenshot placeholder](docs/images/fluxo-dashboard-overview.png)

---

- [Why Use Fluxo?](#why-use-fluxo)
- [Getting Started](#getting-started)
- [Dashboard Overview](#dashboard-overview)
- [Adding Transactions](#adding-transactions)
- [Managing Accounts](#managing-accounts)
- [Budgeting With Needs, Wants, and Savings](#budgeting-with-needs-wants-and-savings)
- [Saving Goals](#saving-goals)
- [Notifications and Reminders](#notifications-and-reminders)
- [Analytics](#analytics)
- [Settings](#settings)
- [Tray and Startup Behavior](#tray-and-startup-behavior)
- [Updates](#updates)
- [Your Data and Privacy](#your-data-and-privacy)
- [Troubleshooting](#troubleshooting)
	- [Fluxo opens in the tray](#fluxo-opens-in-the-tray)
	- [Fluxo does not open](#fluxo-does-not-open)
	- [Update check fails](#update-check-fails)
	- [Installer says Fluxo is already installed](#installer-says-fluxo-is-already-installed)
	- [Data looks missing after reinstall](#data-looks-missing-after-reinstall)
	- [Notifications do not appear](#notifications-do-not-appear)
- [Support](#support)
- [Change Log](#change-log)

---

## Why Use Fluxo?

Fluxo is built for people who want a practical view of where their money is going without turning budgeting into a spreadsheet project. The app centers your finances around accounts, quick transaction entry, budget categories, reminders, and local-first storage.

With Fluxo, you can:

- Track cash, checking, credit, and saving accounts.
- Record expenses, income, and saving goal contributions.
- Split spending into Needs, Wants, and Savings/Invest categories.
- Review spending by date range, account, tag, and category.
- Watch saving goals progress toward a target amount and deadline.
- Receive startup-evaluated cards for overdue payments, recurring work, goals, and budget pressure.
- Keep your financial data local on your Windows device.

## Getting Started

When Fluxo opens for the first time, it guides you through setup so the dashboard has enough information to be useful right away.

![Fluxo setup wizard screenshot placeholder](docs/images/fluxo-setup-wizard.png)

1. Install and open Fluxo.
2. Enter the display name you want Fluxo to use.
3. Add your accounts, such as cash, checking accounts, credit cards, or savings.
4. Choose your budget allocation percentages for Needs, Wants, and Savings/Invest.
5. Add fixed expenses that repeat monthly.
6. Add saving goals with target amounts and deadlines.
7. Choose which financial reminders you want enabled.
8. Finish setup and start from the dashboard.

You can change these choices later from Settings.

## Dashboard Overview

The dashboard is the main workspace in Fluxo. It combines your budget, spending activity, accounts, notifications, and goals into one view.

The dashboard includes:

- **Date controls** for reviewing today, another period, or all-time activity.
- **Budget allocation** for Needs, Wants, and Savings/Invest.
- **Account cards** that show balances, credit usage, and account differences for the selected period.
- **Transaction buckets** that group spending by category.
- **Tag and account filters** for narrowing down the visible transactions.
- **Daily allowance and total spent** summaries.
- **Saving goal carousel** for active goals.
- **Notification panel** for reminders and action items.
- **Analytics access** for deeper spending and income review.

## Adding Transactions

Use Quick Access when you want to open common Fluxo actions immediately.

![Fluxo quick access screenshot placeholder](docs/images/fluxo-quick-add.png)

Quick Access includes common creation, setup, update, planning, account, and settings actions.

- **Expense**: money spent from an account, assigned to a category and tag.
- **Income**: money added to a cash, checking, or saving account.
- **Goal contribution**: money moved toward a saving goal.

For each entry, Fluxo lets you choose the account, amount, date, category, tag, goal, and notes where relevant. After saving, Fluxo updates balances, budget totals, notifications, and goal progress.

## Managing Accounts

Accounts are the money pools Fluxo uses to calculate your budget and transaction impact.

Fluxo supports:

- **Cash** for physical money or cash-like balances.
- **Checking** for everyday bank accounts.
- **Credit** for credit cards with account limits and due dates.
- **Saving** for savings accounts or reserved funds.

Accounts can have balances, account limits, spent amounts, due dates, visibility settings, and enabled states. Credit accounts behave differently from cash or checking accounts: expenses increase the spent amount, while payments reduce it.

## Budgeting With Needs, Wants, and Savings

Fluxo organizes spending into three budget categories:

- **Needs**: essential spending such as groceries, bills, transport, rent, or utilities.
- **Wants**: optional spending such as entertainment, dining out, hobbies, or shopping.
- **Savings/Invest**: saving goal activity and money set aside for future use.

Your allocation percentages determine how much of your available money is reserved for each category. As you add expenses, Fluxo compares category spending against those available amounts and shows remaining budget, usage percentages, and threshold warnings.

Filters help you review spending by:

- Date range.
- Account.
- Expense tag.
- Budget category.

## Saving Goals

Saving goals help you track progress toward a target amount.

A goal can include:

- Name.
- Target amount.
- Current amount.
- Deadline.
- Active or hidden state.

When you add a goal contribution, Fluxo records the transaction, updates the goal's current amount, and adjusts the selected account. An incomplete goal past its deadline can appear as an overdue card at startup.

## Notifications and Reminders

Fluxo evaluates current data and settings during startup. Cards stay in memory for that app session; Clear All clears only current cards, while Snooze All suppresses cards for 24 hours.

Notifications can include:

- Overdue credit payments.
- Overdue recurring transactions.
- Overdue saving goals.
- Budget categories near or past their warning threshold.
- Low cash or checking balances.
- High credit usage.
- Daily allowance warnings.

Overdue payment, goal, and recurring cards expose Process. Fluxo uses Add New Transaction to save each item and move to the next one.

## Analytics

Analytics gives you a more detailed view of money movement over a selected period.

![Fluxo analytics screenshot placeholder](docs/images/fluxo-analytics.png)

Fluxo can summarize:

- Total income.
- Total expenses.
- Income and expenses over time.
- Spending by Needs, Wants, and Savings/Invest.
- Top spending tags.
- Goals created during the selected period.

Use analytics when you want to understand patterns instead of only entering transactions.

## Settings

Settings lets you adjust Fluxo after setup.

![Fluxo settings screenshot placeholder](docs/images/fluxo-settings.png)

Settings areas include:

- **Personalization**: display name and app behavior preferences.
- **Accounts**: account details, visibility, due dates, balances, and account types.
- **Fixed expenses**: recurring monthly expenses.
- **Saving goals**: targets, deadlines, hidden goals, and disabled goal reminders.
- **Tags**: labels and colors for expense organization.
- **Budget**: Needs, Wants, and Savings/Invest percentages.
- **Notifications**: reminder types and warning thresholds.
- **About**: app version and update checks.

## Tray and Startup Behavior

![Fluxo tray screenshot placeholder](docs/images/fluxo-tray.png)

Fluxo can run from the Windows system tray so it stays nearby without taking space on the taskbar.

Depending on your settings, Fluxo can:

- Start with Windows.
- Open directly to the tray.
- Minimize to the tray when closed.
- Be reopened, restarted, or exited from the tray menu.

If Fluxo appears to close but the tray icon remains, it may be using the minimize-to-tray close behavior.

## Updates

Fluxo can check for newer releases from the project's GitHub releases.

When an update is available, Fluxo can download the installer and launch it. The app may close during the update process so the installer can replace the existing files.

If update checks fail, check your internet connection and try again later.

## Your Data and Privacy

Fluxo stores financial data locally on your Windows device.

The local database is stored at:

```text
%LocalAppData%\fluxo\fluxo.db
```

Fluxo also creates startup backups under:

```text
%LocalAppData%\fluxo\backup
```

Backups are kept for a short period and older backup files are pruned automatically. If you are moving to a new device or reinstalling Windows, back up the `%LocalAppData%\fluxo` folder first.

The current app is local-first. No cloud sync is described by Fluxo's current behavior.

## Troubleshooting

### Fluxo opens in the tray

Check the Windows tray area for the Fluxo icon. Open the tray menu to show the app, restart it, or exit it.

### Fluxo does not open

Try launching Fluxo again from the Start menu or installation folder. If it still does not open, restart Windows and try again.

### Update check fails

Make sure your internet connection is available. Fluxo uses GitHub release information to check for updates, so network restrictions that block GitHub can prevent update checks.

### Installer says Fluxo is already installed

Use the installer maintenance options if available, or uninstall the existing version before installing again.

### Data looks missing after reinstall

Fluxo stores data under `%LocalAppData%\fluxo`. If that folder was removed during cleanup, the app may start with a fresh database. Check the backup folder if it still exists.

### Notifications do not appear

Open Settings and confirm that the relevant notification type is enabled. Some reminders only appear when the matching account, due date, threshold, or saving goal condition applies.

## Support

If you report a problem, include:

- Fluxo version.
- Windows version.
- What you were doing when the problem happened.
- Any screenshots that help explain the issue.
- Whether the app was running normally, from startup, from the tray, or during an update.

Project releases and issue tracking may be available from the Fluxo GitHub repository.

## Change Log

# Changelog

All notable changes to Fluxo will be documented in this file.

---

## [1.0.5]

### New Features
- Data import/export with append, overwrite, and conflict handling.
- Expense splitting, nested transactions, and split details.
- Sub-sub-transactions.
- Calendar view with filtering, smooth scrolling, Today button, and future-date navigation.
- Ledger drawer, CSV export, bulk editing, and transaction details.
- Budget Management settings.
- Budget Reconciliation and Budget Forecast.
- Allocation periods and recurring categories.
- Transaction history, history drawer, undo/redo, and revert.
- Transaction pinning and detail history.
- Password lock with auto-lock presets.
- Installment transactions with expiry.
- Debt/IOU tagging and resolution tools, with posted/unposted IOU modes.
- Repayment modes, shared repayments, credit-account handling, and custom amounts.
- Overdue-notification processing directly from transaction popups.
- Notification snoozing, floating notifications, and update notifications.
- Linked transactions and reversal.
- Upcoming Events.
- Collapsible Dashboard cards.
- Keyboard shortcuts and a shortcut overview.
- Expanded Quick Add into Quick Access.
- Analytics trend scaling and grid lines.
- Default account selection.

### Improvements
- Improved Settings, Setup Wizard, account management, and account selection UI.
- Improved colors, contrast, borders, and overall visual styling.
- Improved Dashboard headers, goals, allocation cards, totals, and activity layout.
- Improved Ledger filters, layout, comboboxes, and clear actions.
- Improved transaction modes, popup layouts, installments, tags, and duplicate protection.
- Improved button labels, expansion behavior, hover feedback, and visual states.
- Improved notification layout, severity colors, wording, and sizing.
- Added search animation, tag bubbling, and fading scroll edges.
- Improved transaction-split and popup workflows.
- Updated installer styling.
- Renamed `Spending Source` to `Account`.
- Renamed expense lists to transaction lists.
- Made budget-allocation behavior consistent across the app.

### Major Fixes
- Corrected insufficient-funds locks.
- Fixed budget allocation and calculation errors.
- Fixed `Delete All Data`.
- Stabilized page navigation and transitions.
- Fixed Ledger filters, grouping, amounts, dates, and rows.
- Fixed transaction naming, validation, and tag restoration.
- Fixed Dashboard layout issues and crashes.
- Fixed settings not persisting.
- Improved installer launch reliability.

### Hotfixes
- Fixed dialog focus loss.
- Fixed swipe-reveal tapping.
- Fixed account-list scrolling.
- Fixed notification popup placement.
- Fixed initial transaction validation.
- Fixed search bar/date selector overlap.
- Fixed saving-goal panel interactions.
- Preserved transaction popup state.
- Fixed popup closing and dismissal.

---

## [1.0.4]

- **Hotfix:** Cleaned up redundant files created by build 1.0.3.
- The installer now supports runtime installation.
- Improved Quick Setup, Account, and Dashboard for a better user experience.
- Fixed incorrect behavior during Quick Setup where accounts were not found.
- General UI improvements.

---

## [1.0.3]

- **Hotfix:** Installer not running when .NET 10 is not installed.
- Incomes are now included in search.
- Added a UI and feature lock for insufficient funds.

---

## [1.0.2]

- Introduced recurring transactions for recurring planning and tracking.
- Expanded spending and saving to support richer financial records.
- Overdue cards now process directly through Add New Transaction.

---

## [1.0.1]

- Implemented `Check for updates`.

---

## [1.0.0]

- Initial release.
