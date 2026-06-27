using Fluxo.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fluxo.Data.Context;

public sealed class FluxoDbContext(DbContextOptions<FluxoDbContext> options) : DbContext(options)
{
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<ExpenseLog> ExpenseLogs => Set<ExpenseLog>();
    public DbSet<IncomeLog> IncomeLogs => Set<IncomeLog>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<SavingGoal> SavingGoals => Set<SavingGoal>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<RecurringTransaction> RecurringTransactions => Set<RecurringTransaction>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<UserSettings> UserSettings => Set<UserSettings>();
    public DbSet<BudgetAllocation> BudgetAllocation => Set<BudgetAllocation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureExpense(modelBuilder.Entity<Expense>());
        ConfigureExpenseLog(modelBuilder.Entity<ExpenseLog>());
        ConfigureIncomeLog(modelBuilder.Entity<IncomeLog>());
        ConfigureTransaction(modelBuilder.Entity<Transaction>());
        ConfigureTag(modelBuilder.Entity<Tag>());
        ConfigureSavingGoal(modelBuilder.Entity<SavingGoal>());
        ConfigureAccount(modelBuilder.Entity<Account>());
        ConfigureRecurringTransaction(modelBuilder.Entity<RecurringTransaction>());
        ConfigureNotification(modelBuilder.Entity<Notification>());
        ConfigureUserSettings(modelBuilder.Entity<UserSettings>());
        ConfigureBudgetAllocation(modelBuilder.Entity<BudgetAllocation>());
        ConfigureReferenceAutoIncludes(modelBuilder);
    }

    private static void ConfigureExpense(EntityTypeBuilder<Expense> entity)
    {
        entity.ToTable("Expenses");
        entity.Property(expense => expense.Id).ValueGeneratedOnAdd();
        entity.HasKey(expense => expense.Id);

        entity.Property(expense => expense.Name).IsRequired();
        entity.Property(expense => expense.Amount).HasColumnType("NUMERIC");
        entity.Property(expense => expense.IsIoU).HasDefaultValue(false);

        entity.HasOne(expense => expense.Account)
            .WithMany()
            .HasForeignKey(expense => expense.AccountId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(expense => expense.Tag)
            .WithMany()
            .HasForeignKey(expense => expense.TagId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureExpenseLog(EntityTypeBuilder<ExpenseLog> entity)
    {
        entity.ToTable("ExpenseLogs");
        entity.Property(log => log.Id).ValueGeneratedOnAdd();
        entity.HasKey(log => log.Id);

        entity.Property(log => log.Amount).HasColumnType("NUMERIC");
        entity.Property(log => log.IsForDeletion);
        entity.Property(log => log.IsPinned).HasDefaultValue(false);
        entity.Property(log => log.IsIoU).HasDefaultValue(false);
        entity.Property(log => log.IsExcludedFromBudget).HasDefaultValue(false);
        entity.Property(log => log.Notes).IsRequired();
        entity.Property(log => log.ParentLogId);

        entity.HasOne(log => log.Expense)
            .WithMany()
            .HasForeignKey(log => log.ExpenseId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(log => log.Account)
            .WithMany()
            .HasForeignKey(log => log.AccountId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(log => log.ParentLog)
            .WithMany()
            .HasForeignKey(log => log.ParentLogId)
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
        entity.Property(log => log.IsForDeletion);
        entity.Property(log => log.IsPinned).HasDefaultValue(false);
        entity.Property(log => log.IsIoU).HasDefaultValue(false);
        entity.Property(log => log.IsExcludedFromBudget).HasDefaultValue(false);

        entity.HasOne(log => log.Account)
            .WithMany()
            .HasForeignKey(log => log.AccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureTag(EntityTypeBuilder<Tag> entity)
    {
        entity.ToTable("Tags");
        entity.Property(tag => tag.Id).ValueGeneratedOnAdd();
        entity.HasKey(tag => tag.Id);

        entity.Property(tag => tag.Name).IsRequired();
        entity.Property(tag => tag.HexCode).IsRequired();
        entity.Property(tag => tag.IsSystemTag).HasDefaultValue(false);
        entity.Property(tag => tag.SpendingLimit).HasColumnType("NUMERIC");
    }

    private static void ConfigureSavingGoal(EntityTypeBuilder<SavingGoal> entity)
    {
        entity.ToTable("SavingGoals");
        entity.Property(goal => goal.Id).ValueGeneratedOnAdd();
        entity.HasKey(goal => goal.Id);

        entity.Property(goal => goal.Name).IsRequired();
        entity.Property(goal => goal.TargetAmount).HasColumnType("NUMERIC");
        entity.Property(goal => goal.CurrentAmount).HasColumnType("NUMERIC");
        entity.Property(goal => goal.SavingEndDate);
        entity.Property(goal => goal.CreatedOn);
    }

    private static void ConfigureAccount(EntityTypeBuilder<Account> entity)
    {
        entity.ToTable("Accounts");
        entity.Property(source => source.Id).ValueGeneratedOnAdd();
        entity.HasKey(source => source.Id);

        entity.Property(source => source.Name).IsRequired();
        entity.Property(source => source.AccountLimit).HasColumnType("NUMERIC");
        entity.Property(source => source.MaximumSpending).HasColumnType("NUMERIC");
        entity.Property(source => source.MinimumPayment).HasColumnType("NUMERIC");
        entity.Property(source => source.SpentAmount).HasColumnType("NUMERIC");
        entity.Property(source => source.Balance).HasColumnType("NUMERIC");
        entity.Property(source => source.MonthlyDueDate);
        entity.Property(source => source.DeductSource);
        entity.Property(source => source.IsEnabled);
        entity.Property(source => source.IsDefault).HasDefaultValue(false);
        entity.HasIndex(source => source.IsDefault)
            .IsUnique()
            .HasFilter("IsDefault = 1");
        entity.Property(source => source.PinnedOnUI);
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
        entity.Property(transaction => transaction.RecurringPeriod);
        entity.Property(transaction => transaction.RecurringTime);
        entity.Property(transaction => transaction.Type);
        entity.Property(transaction => transaction.Category);
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

    private static void ConfigureTransaction(EntityTypeBuilder<Transaction> entity)
    {
        entity.ToTable("Transactions", table => table.HasCheckConstraint("CK_Transactions_Amount", "Amount >= 0"));
        entity.HasKey(transaction => transaction.Id);
        entity.Property(transaction => transaction.Id).ValueGeneratedOnAdd();
        entity.Property(transaction => transaction.Name).IsRequired();
        entity.Property(transaction => transaction.Amount).HasColumnType("NUMERIC");
        entity.Property(transaction => transaction.Notes).IsRequired();
        entity.Property(transaction => transaction.IsPinned).HasDefaultValue(false);
        entity.Property(transaction => transaction.IsForDeletion).HasDefaultValue(false);
        entity.Property(transaction => transaction.IsIoU).HasDefaultValue(false);
        entity.Property(transaction => transaction.IsExcludedFromBudget).HasDefaultValue(false);
        entity.HasOne(transaction => transaction.Account).WithMany().HasForeignKey(transaction => transaction.AccountId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(transaction => transaction.Tag).WithMany().HasForeignKey(transaction => transaction.TagId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(transaction => transaction.ParentTransaction).WithMany().HasForeignKey(transaction => transaction.ParentTransactionId).OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureBudgetAllocation(EntityTypeBuilder<BudgetAllocation> entity)
    {
        entity.ToTable("BudgetAllocation");
        entity.Property(allocation => allocation.Id).ValueGeneratedOnAdd();
        entity.HasKey(allocation => allocation.Id);

        entity.Property(allocation => allocation.NeedsThreshold).IsRequired();
        entity.Property(allocation => allocation.WantsThreshold).IsRequired();
        entity.Property(allocation => allocation.InvestThreshold).IsRequired();
        entity.Property(allocation => allocation.AllocationPeriod).IsRequired();
        entity.Property(allocation => allocation.PeriodStart).IsRequired();
        entity.Property(allocation => allocation.CurrentPeriodIndex).IsRequired();
        entity.Property(allocation => allocation.LastRolloverPeriodStart).IsRequired();
        entity.Property(allocation => allocation.AllocationLimit).HasColumnType("NUMERIC");
        entity.Property(allocation => allocation.NeedsDebt).HasColumnType("NUMERIC");
        entity.Property(allocation => allocation.WantsDebt).HasColumnType("NUMERIC");
        entity.Property(allocation => allocation.InvestDebt).HasColumnType("NUMERIC");
        entity.Property(allocation => allocation.RolloverPolicy).IsRequired();
        entity.Property(allocation => allocation.OverspendPolicy).IsRequired();

        entity.Property<int>("SingletonKey")
            .HasDefaultValue(1)
            .IsRequired();
        entity.HasIndex("SingletonKey").IsUnique();
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

                var isOptionalSelfReference =
                    navigation.DeclaringEntityType.ClrType == navigation.TargetEntityType.ClrType &&
                    !navigation.ForeignKey.IsRequired;

                if (isOptionalSelfReference)
                    continue;

                modelBuilder.Entity(entityType.ClrType)
                    .Navigation(navigation.Name)
                    .AutoInclude();
            }
        }
    }
}
