using Catalogue.Core.Models;
using Catalogue.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Catalogue.Infrastructure.Data;

public class CatalogueDbContext : DbContext
{
    public CatalogueDbContext(
        DbContextOptions<CatalogueDbContext> options)
        : base(options)
    {
    }

    public DbSet<Source> Sources => Set<Source>();
    public DbSet<SourceDatabase> SourceDatabases => Set<SourceDatabase>();
    public DbSet<SourceTable> SourceTables => Set<SourceTable>();
    public DbSet<SourceColumn> SourceColumns => Set<SourceColumn>();
    public DbSet<InScopeRelationalColumn> InScopeRelationalColumns => Set<InScopeRelationalColumn>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // --- Source ---
        modelBuilder.Entity<Source>(e =>
        {
            e.ToTable("Sources");
            e.HasKey(x => x.SourceId);
            e.Property(x => x.ServerName).IsRequired().HasMaxLength(255);
            e.Property(x => x.Description).HasMaxLength(1000);
            e.Property(x => x.IsActive).HasDefaultValue(true);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            e.HasIndex(x => x.ServerName).IsUnique().HasDatabaseName("UQ_Sources_ServerName");
        });

        // --- SourceDatabase ---
        modelBuilder.Entity<SourceDatabase>(e =>
        {
            e.ToTable("SourceDatabases");
            e.HasKey(x => x.DatabaseId);
            e.Property(x => x.DatabaseName).IsRequired().HasMaxLength(255);
            e.Property(x => x.Description).HasMaxLength(1000);
            e.Property(x => x.IsActive).HasDefaultValue(true);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            e.HasIndex(x => new { x.SourceId, x.DatabaseName }).IsUnique()
                .HasDatabaseName("UQ_SourceDatabases_ServerDb");
            e.HasOne(x => x.Source)
                .WithMany(s => s.Databases)
                .HasForeignKey(x => x.SourceId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // --- SourceTable ---
        modelBuilder.Entity<SourceTable>(e =>
        {
            e.ToTable("SourceTables");
            e.HasKey(x => x.TableId);
            e.Property(x => x.SchemaName).IsRequired().HasMaxLength(128).HasDefaultValue("dbo");
            e.Property(x => x.TableName).IsRequired().HasMaxLength(255);
            e.Property(x => x.Notes).HasMaxLength(4000);
            e.Property(x => x.IsActive).HasDefaultValue(true);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            e.HasIndex(x => new { x.DatabaseId, x.SchemaName, x.TableName }).IsUnique()
                .HasDatabaseName("UQ_SourceTables_SchemaTable");
            e.HasOne(x => x.Database)
                .WithMany(d => d.Tables)
                .HasForeignKey(x => x.DatabaseId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // --- SourceColumn ---
        modelBuilder.Entity<SourceColumn>(e =>
        {
            e.ToTable("SourceColumns");
            e.HasKey(x => x.ColumnId);
            e.Property(x => x.ColumnName).IsRequired().HasMaxLength(255);
            e.Property(x => x.PersistenceType)
                .IsRequired()
                .HasMaxLength(1)
                .HasDefaultValue('R')
                .HasConversion(
                    c => c.ToString(),
                    s => string.IsNullOrEmpty(s) ? 'R' : s[0]);
            e.ToTable(t => t.HasCheckConstraint("CK_SourceColumns_PersistenceType",
                "[PersistenceType] IN ('R', 'D')"));
            e.Property(x => x.IsActive).HasDefaultValue(true);
            e.Property(x => x.SortOrder).HasDefaultValue(0);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            e.HasIndex(x => new { x.TableId, x.ColumnName }).IsUnique()
                .HasDatabaseName("UQ_SourceColumns_TableColumn");
            e.HasOne(x => x.Table)
                .WithMany(t => t.Columns)
                .HasForeignKey(x => x.TableId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // --- vw_InScopeRelationalColumns (keyless) ---
        modelBuilder.Entity<InScopeRelationalColumn>(e =>
        {
            e.HasNoKey();
            e.ToView("vw_InScopeRelationalColumns");
            e.Property(x => x.PersistenceType)
                .HasMaxLength(1)
                .HasConversion(
                    c => c.ToString(),
                    s => string.IsNullOrEmpty(s) ? 'R' : s[0]);
        });
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var user = "system";

        foreach (var entry in ChangeTracker.Entries())
        {
            // Sources
            if (entry.Entity is Source src)
            {
                if (entry.State == EntityState.Added)
                {
                    src.CreatedAt = now;
                    src.CreatedBy = user;
                }
                if (entry.State is EntityState.Modified or EntityState.Added)
                {
                    src.ModifiedAt = now;
                    src.ModifiedBy = user;
                }
            }
            // SourceDatabases
            if (entry.Entity is SourceDatabase db)
            {
                if (entry.State == EntityState.Added)
                {
                    db.CreatedAt = now;
                    db.CreatedBy = user;
                }
                if (entry.State is EntityState.Modified or EntityState.Added)
                {
                    db.ModifiedAt = now;
                    db.ModifiedBy = user;
                }
            }
            // SourceTables
            if (entry.Entity is SourceTable tbl)
            {
                if (entry.State == EntityState.Added)
                {
                    tbl.CreatedAt = now;
                    tbl.CreatedBy = user;
                }
                if (entry.State is EntityState.Modified or EntityState.Added)
                {
                    tbl.ModifiedAt = now;
                    tbl.ModifiedBy = user;
                }
            }
            // SourceColumns
            if (entry.Entity is SourceColumn col)
            {
                if (entry.State == EntityState.Added)
                {
                    col.CreatedAt = now;
                    col.CreatedBy = user;
                }
                if (entry.State is EntityState.Modified or EntityState.Added)
                {
                    col.ModifiedAt = now;
                    col.ModifiedBy = user;
                }
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
