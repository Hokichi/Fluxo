using Fluxo.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fluxo.Data.Context;

public sealed class FluxoDbContext(DbContextOptions<FluxoDbContext> options) : DbContext(options)
{
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<ExpenseLog> ExpenseLogs => Set<ExpenseLog>();
    public DbSet<IncomeLog> IncomeLogs => Set<IncomeLog>();
    public DbSet<ExpenseTag> ExpenseTags => Set<ExpenseTag>();
    public DbSet<SavingGoal> SavingGoals => Set<SavingGoal>();
    public DbSet<SpendingSource> SpendingSources => Set<SpendingSource>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureExpense(modelBuilder.Entity<Expense>());
        ConfigureExpenseLog(modelBuilder.Entity<ExpenseLog>());
        ConfigureIncomeLog(modelBuilder.Entity<IncomeLog>());
        ConfigureExpenseTag(modelBuilder.Entity<ExpenseTag>());
        ConfigureSavingGoal(modelBuilder.Entity<SavingGoal>());
        ConfigureSpendingSource(modelBuilder.Entity<SpendingSource>());
    }

    private static void ConfigureExpense(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<Expense> entity)
    {
        entity.ToTable("Expenses");
        entity.Property<int>("Id").ValueGeneratedOnAdd();
        entity.HasKey("Id");

        entity.Property(expense => expense.Name).IsRequired();
        entity.Property(expense => expense.Amount).HasColumnType("TEXT");

        entity.HasOne(expense => expense.SpendingSource)
            .WithMany()
            .HasForeignKey("SpendingSourceId")
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(expense => expense.ExpenseTag)
            .WithMany()
            .HasForeignKey("ExpenseTagId")
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureExpenseLog(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<ExpenseLog> entity)
    {
        entity.ToTable("ExpenseLogs");
        entity.Property<int>("Id").ValueGeneratedOnAdd();
        entity.HasKey("Id");

        entity.Property(log => log.Amount).HasColumnType("TEXT");
        entity.Property(log => log.Notes).IsRequired();

        entity.HasOne(log => log.Expense)
            .WithMany()
            .HasForeignKey("ExpenseId")
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(log => log.SpendingSource)
            .WithMany()
            .HasForeignKey("SpendingSourceId")
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureIncomeLog(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<IncomeLog> entity)
    {
        entity.ToTable("IncomeLogs");
        entity.Property<int>("Id").ValueGeneratedOnAdd();
        entity.HasKey("Id");

        entity.Property(log => log.Amount).HasColumnType("TEXT");
        entity.Property(log => log.Notes).IsRequired();

        entity.HasOne(log => log.SpendingSource)
            .WithMany()
            .HasForeignKey("SpendingSourceId")
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureExpenseTag(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<ExpenseTag> entity)
    {
        entity.ToTable("ExpenseTags");
        entity.Property<int>("Id").ValueGeneratedOnAdd();
        entity.HasKey("Id");

        entity.Property(tag => tag.Name).IsRequired();
        entity.Property(tag => tag.HexCode).IsRequired();
    }

    private static void ConfigureSavingGoal(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<SavingGoal> entity)
    {
        entity.ToTable("SavingGoals");
        entity.Property<int>("Id").ValueGeneratedOnAdd();
        entity.HasKey("Id");

        entity.Property(goal => goal.Name).IsRequired();
        entity.Property(goal => goal.TargetAmount).HasColumnType("TEXT");
        entity.Property(goal => goal.CurrentAmount).HasColumnType("TEXT");
    }

    private static void ConfigureSpendingSource(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<SpendingSource> entity)
    {
        entity.ToTable("SpendingSources");
        entity.Property(source => source.Id).ValueGeneratedOnAdd();
        entity.HasKey(source => source.Id);

        entity.Property(source => source.Name).IsRequired();
        entity.Property(source => source.AccountLimit).HasColumnType("TEXT");
        entity.Property(source => source.SpentAmount).HasColumnType("TEXT");
        entity.Property(source => source.Balance).HasColumnType("TEXT");
        entity.Property(source => source.ShowOnUI);
        entity.Property(source => source.InterestRate).HasColumnType("TEXT");
    }
}
