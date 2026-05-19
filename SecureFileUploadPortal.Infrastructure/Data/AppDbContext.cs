using Microsoft.EntityFrameworkCore;
using SecureFileUploadPortal.Domain.Entities;

namespace SecureFileUploadPortal.Infrastructure.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<AuthSession> AuthSessions => Set<AuthSession>();
    public DbSet<UploadedFile> UploadedFiles => Set<UploadedFile>();
    public DbSet<ShareLink> ShareLinks => Set<ShareLink>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.HasIndex(u => u.NormalizedEmail).IsUnique();
            entity.Property(u => u.Id).ValueGeneratedNever();
        });

        modelBuilder.Entity<AuthSession>(entity =>
        {
            entity.HasIndex(s => s.UserId);
            entity.HasIndex(s => s.CsrfTokenHash).IsUnique();
            entity.HasOne(s => s.User)
                .WithMany(u => u.Sessions)
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UploadedFile>(entity =>
        {
            entity.HasIndex(f => f.OwnerUserId);
            entity.HasOne(f => f.Owner)
                .WithMany(u => u.UploadedFiles)
                .HasForeignKey(f => f.OwnerUserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ShareLink>(entity =>
        {
            entity.HasIndex(s => s.TokenHash).IsUnique();
            entity.HasIndex(s => s.FileId);
            entity.HasIndex(s => s.CreatedByUserId);
            entity.HasOne(s => s.UploadedFile)
                .WithMany(f => f.ShareLinks)
                .HasForeignKey(s => s.FileId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(s => s.CreatedByUser)
                .WithMany()
                .HasForeignKey(s => s.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AuditEvent>(entity =>
        {
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.CreatedAtUtc);
        });
    }
}