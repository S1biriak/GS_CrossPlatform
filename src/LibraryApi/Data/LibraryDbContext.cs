using LibraryApi.Auth;
using LibraryApi.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace LibraryApi.Data;

public sealed class LibraryDbContext(DbContextOptions<LibraryDbContext> options) : DbContext(options)
{
    public DbSet<Book> Books => Set<Book>();
    public DbSet<Reader> Readers => Set<Reader>();
    public DbSet<Loan> Loans => Set<Loan>();
    public DbSet<AppUser> Users => Set<AppUser>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.HasDbFunction(typeof(LibraryDbFunctions).GetMethod(nameof(LibraryDbFunctions.Contains))!)
            .HasName("dotnet_contains");
        model.Entity<Book>(b =>
        {
            b.HasIndex(x => x.InventoryNumber).IsUnique();
            b.Property(x => x.InventoryNumber).HasMaxLength(40).UseCollation("NOCASE");
            b.Property(x => x.Title).HasMaxLength(200);
            b.Property(x => x.Author).HasMaxLength(150);
            b.Property(x => x.Genre).HasMaxLength(80);
            b.ToTable(t => t.HasCheckConstraint("CK_Books_Year", "PublicationYear BETWEEN 1 AND 9999"));
        });
        model.Entity<Reader>(r =>
        {
            r.HasIndex(x => x.CardNumber).IsUnique();
            r.Property(x => x.CardNumber).HasMaxLength(40).UseCollation("NOCASE");
            r.Property(x => x.FullName).HasMaxLength(150);
            r.Property(x => x.Email).HasMaxLength(254);
        });
        model.Entity<Loan>(l =>
        {
            l.HasOne<Book>().WithMany().HasForeignKey(x => x.BookId).OnDelete(DeleteBehavior.Restrict);
            l.HasOne<Reader>().WithMany().HasForeignKey(x => x.ReaderId).OnDelete(DeleteBehavior.Restrict);
            l.HasIndex(x => x.BookId).IsUnique().HasFilter("ReturnedAtUtc IS NULL").HasDatabaseName("IX_Loans_OneActivePerBook");
            l.HasIndex(x => new { x.ReaderId, x.ReturnedAtUtc });
            l.HasIndex(x => x.IssuedAtUtc);
            l.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Loans_Due", "DueAtUtc > IssuedAtUtc");
                t.HasCheckConstraint("CK_Loans_Return", "ReturnedAtUtc IS NULL OR ReturnedAtUtc >= IssuedAtUtc");
            });
            // SQLite stores DateTime without Kind. Restore UTC on materialization.
            var utc = new ValueConverter<DateTime, DateTime>(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
            l.Property(x => x.IssuedAtUtc).HasConversion(utc);
            l.Property(x => x.DueAtUtc).HasConversion(utc);
            l.Property(x => x.ReturnedAtUtc).HasConversion(utc);
        });
        model.Entity<AppUser>(u =>
        {
            u.HasIndex(x => x.Login).IsUnique();
            u.Property(x => x.Login).HasMaxLength(80).UseCollation("NOCASE");
            u.Property(x => x.Role).HasMaxLength(20);
        });
    }
}
