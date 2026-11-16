using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ELearningPlatform.Infrastructure.Persistence.Configurations;

// Money: decimal(18,2) + a 3-letter currency. Enums are stored as text so the tables stay readable
// for reports and other consumers of the database. Financial rows are never cascade-deleted.

internal static class FinanceColumns
{
    public static PropertyBuilder<decimal> IsMoney(this PropertyBuilder<decimal> property)
        => property.HasPrecision(18, 2).IsRequired();

    public static PropertyBuilder<string> IsCurrency(this PropertyBuilder<string> property)
        => property.HasMaxLength(Money.CurrencyLength).IsFixedLength().IsRequired();
}

public class WalletConfiguration : IEntityTypeConfiguration<Wallet>
{
    public void Configure(EntityTypeBuilder<Wallet> builder)
    {
        builder.ToTable("Wallets");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Type).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(x => x.Currency).IsCurrency();
        builder.Property(x => x.Balance).IsMoney();
        builder.Property(x => x.RowVersion).IsConcurrencyToken();
        builder.Ignore(x => x.CanGoNegative);

        builder.HasOne(x => x.Owner).WithMany()
            .HasForeignKey(x => x.OwnerUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // One wallet per user per currency, one system wallet per type per currency
        builder.HasIndex(x => new { x.OwnerUserId, x.Currency }).IsUnique().HasFilter("\"OwnerUserId\" IS NOT NULL");
        builder.HasIndex(x => new { x.Type, x.Currency }).IsUnique().HasFilter("\"OwnerUserId\" IS NULL");
    }
}

public class LedgerTransactionConfiguration : IEntityTypeConfiguration<LedgerTransaction>
{
    public void Configure(EntityTypeBuilder<LedgerTransaction> builder)
    {
        builder.ToTable("LedgerTransactions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Type).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(x => x.Currency).IsCurrency();
        builder.Property(x => x.Description).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Reference).HasMaxLength(100);
        builder.Property(x => x.CreatedByUserId).HasMaxLength(450);

        builder.HasOne(x => x.ReversalOf).WithMany()
            .HasForeignKey(x => x.ReversalOfId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.ReversalOfId);
        builder.HasIndex(x => x.Reference);
        builder.HasIndex(x => x.CreatedAt);
    }
}

public class LedgerEntryConfiguration : IEntityTypeConfiguration<LedgerEntry>
{
    public void Configure(EntityTypeBuilder<LedgerEntry> builder)
    {
        builder.ToTable("LedgerEntries");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Direction).HasConversion<string>().HasMaxLength(8).IsRequired();
        builder.Property(x => x.Amount).IsMoney();
        builder.Property(x => x.BalanceAfter).IsMoney();
        builder.Property(x => x.Currency).IsCurrency();

        builder.HasOne(x => x.Transaction).WithMany(x => x.Entries)
            .HasForeignKey(x => x.TransactionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Wallet).WithMany(x => x.Entries)
            .HasForeignKey(x => x.WalletId)
            .OnDelete(DeleteBehavior.Restrict);

        // Statement: a wallet's entries newest first
        builder.HasIndex(x => new { x.WalletId, x.Id });
    }
}

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("Payments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Amount).IsMoney();
        builder.Property(x => x.Currency).IsCurrency();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(x => x.Provider).HasMaxLength(32).IsRequired();
        builder.Property(x => x.ProviderPaymentId).HasMaxLength(255);
        builder.Property(x => x.FailureReason).HasMaxLength(500);

        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Wallet).WithMany().HasForeignKey(x => x.WalletId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<LedgerTransaction>().WithMany().HasForeignKey(x => x.LedgerTransactionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<LedgerTransaction>().WithMany().HasForeignKey(x => x.RefundTransactionId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.ProviderPaymentId).IsUnique().HasFilter("\"ProviderPaymentId\" IS NOT NULL");
        builder.HasIndex(x => new { x.UserId, x.CreatedAt });
        builder.HasIndex(x => x.Status);
    }
}

public class PayoutConfiguration : IEntityTypeConfiguration<Payout>
{
    public void Configure(EntityTypeBuilder<Payout> builder)
    {
        builder.ToTable("Payouts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Amount).IsMoney();
        builder.Property(x => x.Currency).IsCurrency();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(x => x.RejectionReason).HasMaxLength(500);
        builder.Property(x => x.ProcessedByUserId).HasMaxLength(450);

        builder.HasOne(x => x.TeacherUser).WithMany().HasForeignKey(x => x.TeacherUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Wallet).WithMany().HasForeignKey(x => x.WalletId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<LedgerTransaction>().WithMany().HasForeignKey(x => x.RequestTransactionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<LedgerTransaction>().WithMany().HasForeignKey(x => x.SettlementTransactionId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.TeacherUserId, x.CreatedAt });
        builder.HasIndex(x => x.Status);
    }
}

public class CoursePurchaseConfiguration : IEntityTypeConfiguration<CoursePurchase>
{
    public void Configure(EntityTypeBuilder<CoursePurchase> builder)
    {
        builder.ToTable("CoursePurchases");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Price).IsMoney();
        builder.Property(x => x.PlatformFee).IsMoney();
        builder.Property(x => x.TeacherAmount).IsMoney();
        builder.Property(x => x.Currency).IsCurrency();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(16).IsRequired();

        builder.HasOne(x => x.BuyerUser).WithMany().HasForeignKey(x => x.BuyerUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Course).WithMany().HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<LedgerTransaction>().WithMany().HasForeignKey(x => x.LedgerTransactionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<LedgerTransaction>().WithMany().HasForeignKey(x => x.RefundTransactionId).OnDelete(DeleteBehavior.Restrict);

        // Last line of defence against buying the same course twice concurrently
        builder.HasIndex(x => new { x.BuyerUserId, x.CourseId }).IsUnique().HasFilter("\"Status\" = 'Completed'");
        builder.HasIndex(x => x.CourseId);
    }
}

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.UserId).HasMaxLength(450);
        builder.Property(x => x.Action).HasMaxLength(16).IsRequired();
        builder.Property(x => x.EntityType).HasMaxLength(100).IsRequired();
        builder.Property(x => x.EntityId).HasMaxLength(100).IsRequired();
        builder.Property(x => x.IpAddress).HasMaxLength(64);
        builder.Property(x => x.TraceId).HasMaxLength(64);

        builder.HasIndex(x => new { x.EntityType, x.EntityId });
        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.Timestamp);
    }
}

public class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public const int MaxKeyLength = 100;

    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        builder.ToTable("IdempotencyRecords");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.UserId).HasMaxLength(450).IsRequired();
        builder.Property(x => x.Key).HasMaxLength(MaxKeyLength).IsRequired();
        builder.Property(x => x.RequestHash).HasMaxLength(64).IsRequired();
        builder.Property(x => x.ResponseContentType).HasMaxLength(100);

        builder.HasIndex(x => new { x.UserId, x.Key }).IsUnique();
        builder.HasIndex(x => x.CreatedAt);
    }
}

public class ProcessedWebhookEventConfiguration : IEntityTypeConfiguration<ProcessedWebhookEvent>
{
    public void Configure(EntityTypeBuilder<ProcessedWebhookEvent> builder)
    {
        builder.ToTable("ProcessedWebhookEvents");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasMaxLength(255);
        builder.Property(x => x.Provider).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Type).HasMaxLength(100).IsRequired();
    }
}
