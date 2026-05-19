using Fluxo.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fluxo.Data.Context;

public sealed class FluxoDbContext(DbContextOptions<FluxoDbContext> options) : DbContext(options)
{
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<ExpenseLog> ExpenseLogs => Set<ExpenseLog>();
    public DbSet<IncomeLog> IncomeLogs => Set<IncomeLog>();
    public DbSet<ExpenseTag> ExpenseTags => Set<ExpenseTag>();
    public DbSet<SavingGoal> SavingGoals => Set<SavingGoal>();
    public DbSet<SpendingSource> SpendingSources => Set<SpendingSource>();
    public DbSet<RecurringTransaction> RecurringTransactions => Set<RecurringTransaction>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<UserSettings> UserSettings => Set<UserSettings>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureExpense(modelBuilder.Entity<Expense>());
        ConfigureExpenseLog(modelBuilder.Entity<ExpenseLog>());
        ConfigureIncomeLog(modelBuilder.Entity<IncomeLog>());
        ConfigureExpenseTag(modelBuilder.Entity<ExpenseTag>());
        ConfigureSavingGoal(modelBuilder.Entity<SavingGoal>());
        ConfigureSpendingSource(modelBuilder.Entity<SpendingSource>());
        ConfigureRecurringTransaction(modelBuilder.Entity<RecurringTransaction>());
        ConfigureNotification(modelBuilder.Entity<Notification>());
        ConfigureUserSettings(modelBuilder.Entity<UserSettings>());
        ConfigureReferenceAutoIncludes(modelBuilder);
    }

    private static void ConfigureExpense(EntityTypeBuilder<Expense> entity)
    {
        entity.ToTable("Expenses");
        entity.Property(expense => expense.Id).ValueGeneratedOnAdd();
        entity.HasKey(expense => expense.Id);

        entity.Property(expense => expense.Name).IsRequired();
        entity.Property(expense => expense.Amount).HasColumnType("NUMERIC");

        entity.HasOne(expense => expense.SpendingSource)
            .WithMany()
            .HasForeignKey(expense => expense.SpendingSourceId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(expense => expense.ExpenseTag)
            .WithMany()
            .HasForeignKey(expense => expense.ExpenseTagId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureExpenseLog(EntityTypeBuilder<ExpenseLog> entity)
    {
        entity.ToTable("ExpenseLogs");
        entity.Property(log => log.Id).ValueGeneratedOnAdd();
        entity.HasKey(log => log.Id);

        entity.Property(log => log.Amount).HasColumnType("NUMERIC");
        entity.Property(log => log.IsForDeletion);
        entity.Property(log => log.Notes).IsRequired();

        entity.HasOne(log => log.Expense)
            .WithMany()
            .HasForeignKey(log => log.ExpenseId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(log => log.SpendingSource)
            .WithMany()
            .HasForeignKey(log => log.SpendingSourceId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureIncomeLog(EntityTypeBuilder<IncomeLog> entity)
    {
        entity.ToTable("IncomeLogs");
        entity.Property(log => log.Id).ValueGeneratedOnAdd();
        entity.HasKey(log => log.Id);

        entity.Property(log => log.Name).IsRequired();
        entity.Property(log => log.Amount).HasColumnType("NUMERIC");
        entity.Property(log => log.Notes).IsRequired();

        entity.HasOne(log => log.SpendingSource)
            .WithMany()
            .HasForeignKey(log => log.SpendingSourceId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureExpenseTag(EntityTypeBuilder<ExpenseTag> entity)
    {
        entity.ToTable("ExpenseTags");
        entity.Property(tag => tag.Id).ValueGeneratedOnAdd();
        entity.HasKey(tag => tag.Id);

        entity.Property(tag => tag.Name).IsRequired();
        entity.Property(tag => tag.HexCode).IsRequired();
        entity.Property(tag => tag.IsSystemTag).HasDefaultValue(false);
    }

    private static void ConfigureSavingGoal(EntityTypeBuilder<SavingGoal> entity)
    {
        entity.ToTable("SavingGoals");
        entity.Property(goal => goal.Id).ValueGeneratedOnAdd();
        entity.HasKey(goal => goal.Id);

        entity.Property(goal => goal.Name).IsRequired();
        entity.Property(goal => goal.TargetAmount).HasColumnType("NUMERIC");
        entity.Property(goal => goal.CurrentAmount).HasColumnType("NUMERIC");
        entity.Property(goal => goal.CreatedOn);
    }

    private static void ConfigureSpendingSource(EntityTypeBuilder<SpendingSource> entity)
    {
        entity.ToTable("SpendingSources");
        entity.Property(source => source.Id).ValueGeneratedOnAdd();
        entity.HasKey(source => source.Id);

        entity.Property(source => source.Name).IsRequired();
        entity.Property(source => source.AccountLimit).HasColumnType("NUMERIC");
        entity.Property(source => source.SpentAmount).HasColumnType("NUMERIC");
        entity.Property(source => source.Balance).HasColumnType("NUMERIC");
        entity.Property(source => source.MonthlyDueDate);
        entity.Property(source => source.DeductSource);
        entity.Property(source => source.IsEnabled);
        entity.Property(source => source.ShowOnUI);
        entity.Property(source => source.InterestRate).HasColumnType("REAL");
    }

    private static void ConfigureNotification(EntityTypeBuilder<Notification> entity)
    {
        entity.ToTable("Notifications");
        entity.Property(notification => notification.Id).ValueGeneratedOnAdd();
        entity.HasKey(notification => notification.Id);

        entity.Property(notification => notification.Type).IsRequired();
        entity.Property(notification => notification.Header).IsRequired();
        entity.Property(notification => notification.Message).IsRequired();
        entity.Property(notification => notification.CreatedOn);
        entity.Property(notification => notification.IsCleared);
        entity.Property(notification => notification.IsForDeletion);
    }

    private static void ConfigureRecurringTransaction(EntityTypeBuilder<RecurringTransaction> entity)
    {
        entity.ToTable("RecurringTransactions");
        entity.Property(transaction => transaction.Id).ValueGeneratedOnAdd();
        entity.HasKey(transaction => transaction.Id);

        entity.Property(transaction => transaction.Name).IsRequired();
        entity.Property(transaction => transaction.Amount).HasColumnType("NUMERIC");
        entity.Property(transaction => transaction.RecurringDate);
        entity.Property(transaction => transaction.Type);
        entity.Property(transaction => transaction.IsEnabled);

        entity.HasOne(transaction => transaction.Source)
            .WithMany()
            .HasForeignKey(transaction => transaction.SourceId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(transaction => transaction.Tag)
            .WithMany()
            .HasForeignKey(transaction => transaction.TagId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(transaction => transaction.Goal)
            .WithMany()
            .HasForeignKey(transaction => transaction.GoalId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureUserSettings(EntityTypeBuilder<UserSettings> entity)
    {
        entity.ToTable("UserSettings");
        entity.HasKey(settings => settings.Name);

        entity.Property(settings => settings.Name).IsRequired();
        entity.Property(settings => settings.Value).IsRequired();
    }

    private static void ConfigureReferenceAutoIncludes(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (entityType.ClrType is null)
                continue;

            foreach (var navigation in entityType.GetNavigations())
            {
                if (navigation.IsCollection)
                    continue;

                modelBuilder.Entity(entityType.ClrType)
                    .Navigation(navigation.Name)
                    .AutoInclude();
            }
        }
    }
}
