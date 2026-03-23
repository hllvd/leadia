using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User>             Users             => Set<User>();
    public DbSet<Bot>              Bots              => Set<Bot>();
    public DbSet<Message>          Messages          => Set<Message>();
    public DbSet<ConversationState> ConversationStates => Set<ConversationState>();
    public DbSet<ConversationFact>  ConversationFacts  => Set<ConversationFact>();
    public DbSet<ConversationTask>  ConversationTasks  => Set<ConversationTask>();
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

        // ── Message ──
        modelBuilder.Entity<Message>(b =>
        {
            b.HasKey(e => e.Id);
            b.HasIndex(e => e.UserId);
            b.HasIndex(e => e.BotId);
            b.HasIndex(e => e.Timestamp);
            b.Property(e => e.Timestamp).HasConversion(
                v => v.ToString("O"), v => DateTimeOffset.Parse(v));
            b.Property(e => e.Sender).HasConversion<string>();
            b.HasOne(e => e.User)
             .WithMany(u => u.Messages)
             .HasForeignKey(e => e.UserId)
             .OnDelete(DeleteBehavior.Cascade);
            b.HasOne(e => e.Bot)
             .WithMany(b => b.Messages)
             .HasForeignKey(e => e.BotId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── ConversationState ──
        modelBuilder.Entity<ConversationState>(b =>
        {
            b.HasKey(e => e.ConversationId);
            b.Property(e => e.LastMessageTimestamp).HasConversion(
                v => v.ToString("O"), v => DateTimeOffset.Parse(v));
            b.Property(e => e.LastActivityTimestamp);
            b.Property(e => e.CreatedAt).HasConversion(
                v => v.ToString("O"), v => DateTimeOffset.Parse(v));
            b.Property(e => e.Mode).HasConversion<int>();
            b.HasMany(e => e.Facts)
             .WithOne(f => f.Conversation)
             .HasForeignKey(f => f.ConversationId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ── ConversationFact ──
        modelBuilder.Entity<ConversationFact>(b =>
        {
            b.HasKey(e => e.Id);
            b.HasIndex(e => new { e.ConversationId, e.FactName }).IsUnique();
            b.Property(e => e.UpdatedAt).HasConversion(
                v => v.ToString("O"), v => DateTimeOffset.Parse(v));
        });

        // ── RealStateAgency ──
        modelBuilder.Entity<RealStateAgency>(b =>
        {
            b.HasKey(e => e.Id);
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

        // ── ConversationTask ──
        modelBuilder.Entity<ConversationTask>(b =>
        {
            b.HasKey(e => e.Id);
            b.HasIndex(e => new { e.ConversationId, e.Type }).IsUnique();
            b.Property(e => e.CreatedAt).HasConversion(
                v => v.ToString("O"), v => DateTimeOffset.Parse(v));
            b.Property(e => e.UpdatedAt).HasConversion(
                v => v.ToString("O"), v => DateTimeOffset.Parse(v));
        });
    }
}
