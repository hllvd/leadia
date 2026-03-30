using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User>             Users             => Set<User>();
    public DbSet<Bot>              Bots              => Set<Bot>();
    public DbSet<RealStateAgency>   RealStateAgencies  => Set<RealStateAgency>();
    public DbSet<RealStateBroker>   RealStateBrokers   => Set<RealStateBroker>();
    public DbSet<BrokerData>        BrokersData        => Set<BrokerData>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ── Bot ──
        modelBuilder.Entity<Bot>(b =>
        {
            b.HasKey(e => e.Id);
            b.HasIndex(e => e.BotNumber).IsUnique();
            b.Property(e => e.CreatedAt).HasConversion(
                v => v.ToString("O"), v => DateTimeOffset.Parse(v));
            b.Property(e => e.UpdatedAt).HasConversion(
                v => v.ToString("O"), v => DateTimeOffset.Parse(v));
        });

        // ── User ──
        modelBuilder.Entity<User>(b =>
        {
            b.HasKey(e => e.Id);
            b.HasIndex(e => e.Email).IsUnique();
            b.HasIndex(e => e.WhatsAppNumber).IsUnique();
            b.Property(e => e.Role).HasConversion<string>();
            b.Property(e => e.CreatedAt).HasConversion(
                v => v.ToString("O"), v => DateTimeOffset.Parse(v));
            b.Property(e => e.UpdatedAt).HasConversion(
                v => v.ToString("O"), v => DateTimeOffset.Parse(v));
            b.HasOne(e => e.Bot)
             .WithMany(b => b.Users)
             .HasForeignKey(e => e.BotId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── RealStateAgency ──
        modelBuilder.Entity<RealStateAgency>(b =>
        {
            b.HasKey(e => e.Id);
            b.Property(e => e.NudgeTimeoutMinutes).HasDefaultValue(10);
            b.Property(e => e.NudgeBrokerAfterMessages).HasDefaultValue(3);
            b.HasMany(e => e.BrokerAssignments)
             .WithOne(a => a.RealStateAgency)
             .HasForeignKey(a => a.RealStateAgencyId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ── RealStateBroker ──
        modelBuilder.Entity<RealStateBroker>(b =>
        {
            b.HasKey(e => e.Id);
            b.HasOne(e => e.Broker)
             .WithMany()
             .HasForeignKey(e => e.BrokerId)
             .OnDelete(DeleteBehavior.Restrict);
            b.Property(e => e.Mode).HasConversion<int>();
        });

        // ── BrokerData ──
        modelBuilder.Entity<BrokerData>(b =>
        {
            b.HasKey(e => e.Id);
            b.Property(e => e.CreatedAt).HasConversion(
                v => v.ToString("O"), v => DateTimeOffset.Parse(v));
            b.Property(e => e.UpdatedAt).HasConversion(
                v => v.ToString("O"), v => DateTimeOffset.Parse(v));
            b.HasOne(e => e.Broker)
             .WithMany()
             .HasForeignKey(e => e.BrokerId)
             .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
