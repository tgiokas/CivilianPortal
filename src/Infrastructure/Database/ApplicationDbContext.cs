using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

using CitizenPortal.Application.Interfaces;
using CitizenPortal.Domain.Entities;
using CitizenPortal.Domain.Enums;

namespace CitizenPortal.Infrastructure.Database;

public class ApplicationDbContext : DbContext, IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options) { }

    public required DbSet<CitizenUser> CitizenUsers { get; set; }
    public required DbSet<Domain.Entities.Application> Applications { get; set; }
    public required DbSet<ApplicationDocument> ApplicationDocuments { get; set; }
    public required DbSet<OutboxMessage> OutboxMessages { get; set; }
    public required DbSet<AuthenticationAuditLog> AuthenticationAuditLogs { get; set; }
    public required DbSet<UploadJob> UploadJobs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // CitizenUser
        modelBuilder.Entity<CitizenUser>(entity =>
        {
            entity.ToTable("CitizenUsers");
            entity.HasKey(u => u.Id);
            entity.Property(u => u.Email).IsRequired().HasMaxLength(320);
            entity.Property(u => u.FirstName).HasMaxLength(200);
            entity.Property(u => u.LastName).HasMaxLength(200);
            entity.Property(u => u.VatId).HasMaxLength(50);            
            entity.HasIndex(u => u.KeycloakUserId).IsUnique();
            entity.HasIndex(u => u.Email);
        });

        // Application
        modelBuilder.Entity<Domain.Entities.Application>(entity =>
        {
            entity.ToTable("Applications");
            entity.HasKey(a => a.Id);
            entity.Property(a => a.PublicId).IsRequired();
            entity.Property(a => a.Subject).IsRequired().HasMaxLength(500);
            entity.Property(a => a.Body).IsRequired();
            entity.Property(a => a.Email).IsRequired().HasMaxLength(320);
            entity.Property(a => a.Status).HasConversion<int>().HasDefaultValue(ApplicationStatus.Submitted);
            entity.Property(a => a.ProtocolNumber).HasMaxLength(50);
            entity.Property(a => a.ProtocolYear).HasMaxLength(4);
            
            entity.HasIndex(a => a.PublicId).IsUnique();
            entity.HasIndex(a => a.UserId);
            entity.HasIndex(a => a.Status);

            entity.HasOne(a => a.CitizenUser)
                  .WithMany(u => u.Applications)
                  .HasForeignKey(a => a.UserId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ApplicationDocument
        modelBuilder.Entity<ApplicationDocument>(entity =>
        {
            entity.ToTable("ApplicationDocuments");
            entity.HasKey(d => d.Id);
            entity.Property(d => d.StorageBucket).IsRequired().HasMaxLength(200);
            entity.Property(d => d.StorageKey).IsRequired().HasMaxLength(500);
            entity.Property(d => d.FileName).IsRequired().HasMaxLength(500);
            entity.Property(d => d.ContentType).IsRequired().HasMaxLength(100);
            entity.Property(d => d.Kind)
                  .HasConversion<int>()
                  .IsRequired();                  
            
            entity.HasIndex(d => new { d.ApplicationId, d.Kind });

            entity.HasOne(d => d.Application)
                  .WithMany(a => a.Documents)
                  .HasForeignKey(d => d.ApplicationId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // OutboxMessage
        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.ToTable("OutboxMessages");
            entity.HasKey(o => o.Id);
            entity.Property(o => o.EventId).IsRequired();
            entity.Property(o => o.EventType).IsRequired().HasMaxLength(200);
            entity.Property(o => o.Payload).IsRequired();
            entity.Property(o => o.Key).HasMaxLength(200);
            
            entity.HasIndex(o => o.ProcessedAt).HasFilter("processed_at IS NULL");  // Fast lookup for pending
        });

        // AuthenticationAuditLog
        modelBuilder.Entity<AuthenticationAuditLog>(entity =>
        {
            entity.ToTable("AuthenticationAuditLogs");
            entity.HasKey(a => a.Id);
            entity.Property(a => a.Provider).IsRequired().HasMaxLength(64);
            entity.Property(a => a.Reason).IsRequired().HasMaxLength(200);
            entity.Property(a => a.IpAddress).HasMaxLength(64);   // IPv6 + optional port
            entity.Property(a => a.Username).HasMaxLength(320);
            entity.Property(a => a.MachineName).HasMaxLength(256);
            entity.Property(a => a.FailureReason).HasMaxLength(500);

            entity.HasIndex(a => a.CreatedAt);
            entity.HasIndex(a => a.Provider);
            entity.HasIndex(a => a.Success);
        });

        // UploadJob
        modelBuilder.Entity<UploadJob>(entity =>
        {
            entity.ToTable("UploadJobs");
            entity.HasKey(j => j.Id);
            entity.Property(j => j.JobId).IsRequired();
            entity.Property(j => j.CallerSystemId).IsRequired().HasMaxLength(100);
            entity.Property(j => j.Status).HasConversion<int>().HasDefaultValue(UploadJobStatus.Pending);
            entity.Property(j => j.Subject).HasMaxLength(500);
            entity.Property(j => j.ProtocolNumber).HasMaxLength(50);
            entity.Property(j => j.ProtocolYear).HasMaxLength(4);
            entity.Property(j => j.ErrorCode).HasMaxLength(50);
            entity.Property(j => j.ErrorMessage).HasMaxLength(1000);

            entity.HasIndex(j => j.JobId).IsUnique();
            entity.HasIndex(j => j.Status);
        });
    }

    public async Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        return await Database.BeginTransactionAsync(cancellationToken);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await base.SaveChangesAsync(cancellationToken);
    }
}