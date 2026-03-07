using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Text;
using Fluxo.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fluxo.Data.Context;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    // ── Tables ─────────────────────────────────────────────────────────────────
    public DbSet<IncomeSource> IncomeSources => Set<IncomeSource>();

    public DbSet<IncomeEntry> IncomeEntries => Set<IncomeEntry>();
    public DbSet<BnplSource> BnplSources => Set<BnplSource>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<ExpenseTag> ExpenseTags => Set<ExpenseTag>();
    public DbSet<FixedExpense> FixedExpenses => Set<FixedExpense>();
    public DbSet<FixedExpenseTag> FixedExpenseTags => Set<FixedExpenseTag>();
    public DbSet<FixedExpenseHistory> FixedExpenseHistory => Set<FixedExpenseHistory>();
    public DbSet<SavingsAccount> SavingsAccounts => Set<SavingsAccount>();
    public DbSet<SavingsGoal> SavingsGoals => Set<SavingsGoal>();
    public DbSet<BudgetConfig> BudgetConfigs => Set<BudgetConfig>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ── Composite PKs for junction tables ─────────────────────────────────
        modelBuilder.Entity<ExpenseTag>()
            .HasKey(et => new { et.ExpenseId, et.TagId });

        modelBuilder.Entity<FixedExpenseTag>()
            .HasKey(ft => new { ft.FixedExpenseId, ft.TagId });

        // ── BudgetConfig: one row per (Month, Year) ────────────────────────────
        modelBuilder.Entity<BudgetConfig>()
            .HasIndex(bc => new { bc.Month, bc.Year })
            .IsUnique();

        // ── Enum columns stored as integers (SQLite default) ──────────────────
        modelBuilder.Entity<IncomeSource>()
            .Property(e => e.Type)
            .HasConversion<int>();

        modelBuilder.Entity<BnplSource>()
            .Property(e => e.Type)
            .HasConversion<int>();

        modelBuilder.Entity<Expense>()
            .Property(e => e.Category)
            .HasConversion<int>();

        modelBuilder.Entity<FixedExpense>()
            .Property(e => e.AmountMode)
            .HasConversion<int>();

        modelBuilder.Entity<FixedExpense>()
            .Property(e => e.Category)
            .HasConversion<int>();

        modelBuilder.Entity<SavingsGoal>()
            .Property(e => e.ContributionFrequency)
            .HasConversion<int>();

        // ── Cascade behaviour ─────────────────────────────────────────────────
        // Deleting an Expense cascades to its tags but NOT to the Tag itself.
        modelBuilder.Entity<ExpenseTag>()
            .HasOne(et => et.Expense)
            .WithMany(e => e.ExpenseTags)
            .HasForeignKey(et => et.ExpenseId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ExpenseTag>()
            .HasOne(et => et.Tag)
            .WithMany(t => t.ExpenseTags)
            .HasForeignKey(et => et.TagId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<FixedExpenseTag>()
            .HasOne(ft => ft.FixedExpense)
            .WithMany(fe => fe.FixedExpenseTags)
            .HasForeignKey(ft => ft.FixedExpenseId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<FixedExpenseTag>()
            .HasOne(ft => ft.Tag)
            .WithMany(t => t.FixedExpenseTags)
            .HasForeignKey(ft => ft.TagId)
            .OnDelete(DeleteBehavior.Restrict);

        // Deleting a FixedExpense cascades to its history rows.
        modelBuilder.Entity<FixedExpenseHistory>()
            .HasOne(h => h.FixedExpense)
            .WithMany(fe => fe.History)
            .HasForeignKey(h => h.FixedExpenseId)
            .OnDelete(DeleteBehavior.Cascade);

        // Deleting a BnplSource nullifies the reference on Expenses (keep the record).
        modelBuilder.Entity<Expense>()
            .HasOne(e => e.BnplSource)
            .WithMany(b => b.Expenses)
            .HasForeignKey(e => e.BnplSourceId)
            .OnDelete(DeleteBehavior.SetNull);

        // ── Seed default AppSettings ──────────────────────────────────────────
        modelBuilder.Entity<AppSetting>().HasData(
            new AppSetting
            {
                Key = AppSetting.Keys.Currency,
                Value = "USD",
                UpdatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new AppSetting
            {
                Key = AppSetting.Keys.NotificationLeadDays,
                Value = "3",
                UpdatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new AppSetting
            {
                Key = AppSetting.Keys.DefaultEntryDay,
                Value = "1",
                UpdatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new AppSetting
            {
                Key = AppSetting.Keys.Theme,
                Value = "system",
                UpdatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            }
        );
    }
}