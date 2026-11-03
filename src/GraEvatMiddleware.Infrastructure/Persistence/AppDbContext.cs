using GraEvatMiddleware.Domain.Auditing;
using GraEvatMiddleware.Domain.Integration;
using GraEvatMiddleware.Domain.Tenants;
using GraEvatMiddleware.Domain.Transactions;
using Microsoft.EntityFrameworkCore;

namespace GraEvatMiddleware.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<ApiCredential> ApiCredentials => Set<ApiCredential>();
    public DbSet<EVatTransaction> Transactions => Set<EVatTransaction>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<IntegrationExchange> IntegrationExchanges => Set<IntegrationExchange>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.ToTable("tenants");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Name).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Slug).HasMaxLength(64).IsRequired();
            entity.Property(item => item.TaxpayerReference).HasMaxLength(64).IsRequired();
            entity.Property(item => item.GraSecurityKeySecretName).HasMaxLength(200).IsRequired();
            entity.HasIndex(item => item.Slug).IsUnique();
            entity.HasIndex(item => item.TaxpayerReference).IsUnique();
        });

        modelBuilder.Entity<ApiCredential>(entity =>
        {
            entity.ToTable("api_credentials");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.KeyPrefix).HasMaxLength(16).IsRequired();
            entity.Property(item => item.KeyHash).HasMaxLength(128).IsRequired();
            entity.HasIndex(item => item.KeyPrefix).IsUnique();
            entity.HasOne(item => item.Tenant)
                .WithMany(item => item.Credentials)
                .HasForeignKey(item => item.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<EVatTransaction>(entity =>
        {
            entity.ToTable("evat_transactions");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.InvoiceNumber).HasMaxLength(100).IsRequired();
            entity.Property(item => item.IdempotencyKey).HasMaxLength(128).IsRequired();
            entity.Property(item => item.RequestHash).HasMaxLength(128).IsRequired();
            entity.Property(item => item.CorrelationId).HasMaxLength(100).IsRequired();
            entity.Property(item => item.ExternalReference).HasMaxLength(200);
            entity.Property(item => item.ErrorCode).HasMaxLength(100);
            entity.HasIndex(item => new { item.TenantId, item.Operation, item.IdempotencyKey }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.Id });
            entity.HasOne(item => item.Tenant)
                .WithMany(item => item.Transactions)
                .HasForeignKey(item => item.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AuditEvent>(entity =>
        {
            entity.ToTable("audit_events");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Action).HasMaxLength(100).IsRequired();
            entity.Property(item => item.ResourceType).HasMaxLength(100).IsRequired();
            entity.Property(item => item.ResourceId).HasMaxLength(100).IsRequired();
            entity.Property(item => item.CorrelationId).HasMaxLength(100).IsRequired();
            entity.Property(item => item.MetadataJson).HasColumnType("jsonb").IsRequired();
            entity.HasIndex(item => new { item.TenantId, item.CreatedAt });
        });

        modelBuilder.Entity<IntegrationExchange>(entity =>
        {
            entity.ToTable("integration_exchange_logs");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.CorrelationId).HasMaxLength(100).IsRequired();
            entity.Property(item => item.IdempotencyKey).HasMaxLength(128);
            entity.Property(item => item.SourceSystem).HasMaxLength(50).IsRequired();
            entity.Property(item => item.DestinationSystem).HasMaxLength(50).IsRequired();
            entity.Property(item => item.Operation).HasMaxLength(100).IsRequired();
            entity.Property(item => item.HttpMethod).HasMaxLength(16).IsRequired();
            entity.Property(item => item.Url).HasMaxLength(2_048).IsRequired();
            entity.Property(item => item.RequestHeadersJson).HasColumnType("jsonb");
            entity.Property(item => item.RequestBody).HasColumnType("text");
            entity.Property(item => item.ResponseHeadersJson).HasColumnType("jsonb");
            entity.Property(item => item.ResponseBody).HasColumnType("text");
            entity.Property(item => item.ErrorCode).HasMaxLength(100);
            entity.HasIndex(item => new { item.TenantId, item.CreatedAt });
            entity.HasIndex(item => item.TransactionId);
            entity.HasIndex(item => item.CorrelationId);
            entity.HasIndex(item => new { item.Operation, item.CreatedAt });
            entity.HasIndex(item => new { item.Outcome, item.CreatedAt });
        });
    }
}
