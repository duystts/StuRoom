using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using StuRoom.Models;

namespace StuRoom.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Building> Buildings => Set<Building>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<RoomImage> RoomImages => Set<RoomImage>();
    public DbSet<Amenity> Amenities => Set<Amenity>();
    public DbSet<RoomAmenity> RoomAmenities => Set<RoomAmenity>();
    public DbSet<FeeConfig> FeeConfigs => Set<FeeConfig>();
    public DbSet<ViewingRequest> ViewingRequests => Set<ViewingRequest>();
    public DbSet<BookingRequest> BookingRequests => Set<BookingRequest>();
    public DbSet<Contract> Contracts => Set<Contract>();
    public DbSet<ContractMember> ContractMembers => Set<ContractMember>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceItem> InvoiceItems => Set<InvoiceItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<RoomReview> RoomReviews => Set<RoomReview>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<RoomReport> RoomReports => Set<RoomReport>();
    public DbSet<FavoriteRoom> FavoriteRooms => Set<FavoriteRoom>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // AuditLog - restrict delete
        builder.Entity<AuditLog>()
            .HasOne(a => a.User)
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // RoomAmenity — composite PK
        builder.Entity<RoomAmenity>()
            .HasKey(ra => new { ra.RoomId, ra.AmenityId });

        // FeeConfig — tắt cascade delete để tránh xung đột nhiều FK path
        builder.Entity<FeeConfig>()
            .HasOne(f => f.Building)
            .WithMany(b => b.FeeConfigs)
            .HasForeignKey(f => f.BuildingId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<FeeConfig>()
            .HasOne(f => f.Room)
            .WithMany(r => r.FeeConfigs)
            .HasForeignKey(f => f.RoomId)
            .OnDelete(DeleteBehavior.Restrict);

        // Contract — tắt cascade vì có nhiều FK tới ApplicationUser
        builder.Entity<Contract>()
            .HasOne(c => c.Tenant)
            .WithMany()
            .HasForeignKey(c => c.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        // Payment — tránh multiple cascade paths
        builder.Entity<Payment>()
            .HasOne(p => p.RecordedBy)
            .WithMany()
            .HasForeignKey(p => p.RecordedById)
            .OnDelete(DeleteBehavior.Restrict);

        // RoomReview — tránh multiple cascade paths
        builder.Entity<RoomReview>()
            .HasOne(r => r.Reviewer)
            .WithMany()
            .HasForeignKey(r => r.ReviewerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<RoomReview>()
            .HasOne(r => r.Contract)
            .WithMany(c => c.Reviews)
            .HasForeignKey(r => r.ContractId)
            .OnDelete(DeleteBehavior.Restrict);

        // BookingRequest
        builder.Entity<BookingRequest>()
            .HasOne(b => b.Tenant)
            .WithMany()
            .HasForeignKey(b => b.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<BookingRequest>()
            .HasOne(b => b.Contract)
            .WithMany()
            .HasForeignKey(b => b.ContractId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<BookingRequest>()
            .HasOne(b => b.ViewingRequest)
            .WithMany()
            .HasForeignKey(b => b.ViewingRequestId)
            .OnDelete(DeleteBehavior.Restrict);

        // ContractMember
        builder.Entity<ContractMember>()
            .HasOne(cm => cm.Tenant)
            .WithMany()
            .HasForeignKey(cm => cm.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        // ViewingRequest
        builder.Entity<ViewingRequest>()
            .HasOne(v => v.Tenant)
            .WithMany()
            .HasForeignKey(v => v.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        // Notification
        builder.Entity<Notification>()
            .HasOne(n => n.User)
            .WithMany()
            .HasForeignKey(n => n.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Decimal precision
        builder.Entity<FeeConfig>().Property(f => f.UnitPrice).HasPrecision(18, 2);
        builder.Entity<Contract>().Property(c => c.DepositAmount).HasPrecision(18, 2);
        builder.Entity<Contract>().Property(c => c.MonthlyRent).HasPrecision(18, 2);
        builder.Entity<Invoice>().Property(i => i.TotalAmount).HasPrecision(18, 2);
        builder.Entity<InvoiceItem>().Property(i => i.UnitPrice).HasPrecision(18, 2);
        builder.Entity<InvoiceItem>().Property(i => i.Amount).HasPrecision(18, 2);
        builder.Entity<InvoiceItem>().Property(i => i.Quantity).HasPrecision(18, 3);
        builder.Entity<InvoiceItem>().Property(i => i.PreviousReading).HasPrecision(18, 3);
        builder.Entity<InvoiceItem>().Property(i => i.CurrentReading).HasPrecision(18, 3);
        builder.Entity<Payment>().Property(p => p.Amount).HasPrecision(18, 2);
        builder.Entity<Room>().Property(r => r.Area).HasPrecision(10, 2);

        // FavoriteRoom — composite PK and FK constraints
        builder.Entity<FavoriteRoom>()
            .HasKey(fr => new { fr.TenantId, fr.RoomId });

        builder.Entity<FavoriteRoom>()
            .HasOne(fr => fr.Tenant)
            .WithMany()
            .HasForeignKey(fr => fr.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        // RoomReport — FK constraints
        builder.Entity<RoomReport>()
            .HasOne(rr => rr.Reporter)
            .WithMany()
            .HasForeignKey(rr => rr.ReporterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<RoomReport>()
            .HasOne(rr => rr.Room)
            .WithMany()
            .HasForeignKey(rr => rr.RoomId)
            .OnDelete(DeleteBehavior.Cascade);

        // ChatMessage - disable cascade
        builder.Entity<ChatMessage>()
            .HasOne(m => m.Sender)
            .WithMany()
            .HasForeignKey(m => m.SenderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<ChatMessage>()
            .HasOne(m => m.Receiver)
            .WithMany()
            .HasForeignKey(m => m.ReceiverId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

