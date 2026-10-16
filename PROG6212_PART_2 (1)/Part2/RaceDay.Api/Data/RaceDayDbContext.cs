using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Models;

namespace RaceDay.Api.Data;

public class RaceDayDbContext : DbContext
{
    public RaceDayDbContext(DbContextOptions<RaceDayDbContext> options) : base(options) { }

    public DbSet<Role> Roles => Set<Role>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Enrolment> Enrolments => Set<Enrolment>();
    public DbSet<Result> Results => Set<Result>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<Role>(e =>
        {
            e.ToTable("Roles");
            e.HasKey(r => r.RoleId);
            e.Property(r => r.RoleName).HasMaxLength(20).IsRequired();
            e.HasIndex(r => r.RoleName).IsUnique();
            // The two roles are fixed reference data, so users can only ever pick one of these.
            e.HasData(
                new Role { RoleId = 1, RoleName = RoleNames.Organiser },
                new Role { RoleId = 2, RoleName = RoleNames.Participant });
        });

        mb.Entity<User>(e =>
        {
            e.ToTable("Users");
            e.HasKey(u => u.UserId);
            e.Property(u => u.FirstName).HasMaxLength(50).IsRequired();
            e.Property(u => u.LastName).HasMaxLength(50).IsRequired();
            e.Property(u => u.Email).HasMaxLength(100).IsRequired();
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.PasswordHash).HasMaxLength(255).IsRequired();
            e.Property(u => u.ContactNumber).HasMaxLength(20);
            e.HasOne(u => u.Role).WithMany(r => r.Users).HasForeignKey(u => u.RoleId).OnDelete(DeleteBehavior.Restrict);
        });

        mb.Entity<Event>(e =>
        {
            e.ToTable("Events");
            e.HasKey(x => x.EventId);
            e.Property(x => x.EventName).HasMaxLength(100).IsRequired();
            e.Property(x => x.EventDate).HasColumnType("date");
            e.Property(x => x.Location).HasMaxLength(150).IsRequired();
            e.Property(x => x.Description).HasMaxLength(500);
            e.Property(x => x.EventType).HasMaxLength(10).IsRequired();
            e.Property(x => x.DistanceKm).HasPrecision(6, 2);
            e.HasOne(x => x.Organiser).WithMany(u => u.Events).HasForeignKey(x => x.OrganiserId).OnDelete(DeleteBehavior.Restrict);
        });

        mb.Entity<Category>(e =>
        {
            e.ToTable("Categories");
            e.HasKey(c => c.CategoryId);
            e.Property(c => c.CategoryName).HasMaxLength(50).IsRequired();
            e.Property(c => c.DistanceKm).HasPrecision(6, 2);
            e.Property(c => c.EntryFee).HasPrecision(10, 2);
            e.HasOne(c => c.Event).WithMany(x => x.Categories).HasForeignKey(c => c.EventId).OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<Enrolment>(e =>
        {
            e.ToTable("Enrolments");
            e.HasKey(x => x.EnrolmentId);
            e.Property(x => x.Status).HasMaxLength(20).IsRequired();
            e.HasIndex(x => new { x.ParticipantId, x.CategoryId }).IsUnique();
            e.HasOne(x => x.Participant).WithMany(u => u.Enrolments).HasForeignKey(x => x.ParticipantId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Category).WithMany(c => c.Enrolments).HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<Result>(e =>
        {
            e.ToTable("Results");
            e.HasKey(r => r.ResultId);
            e.Property(r => r.FinishTime).HasColumnType("time");
            e.Property(r => r.Status).HasMaxLength(20).IsRequired();
            e.HasIndex(r => r.EnrolmentId).IsUnique();
            e.HasOne(r => r.Enrolment).WithOne(x => x.Result).HasForeignKey<Result>(r => r.EnrolmentId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
