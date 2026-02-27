using DataManager.Core.Models.Entities;
using DataManager.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DataManager.Infrastructure.Data;

public class DataManagerDbContext : DbContext
{
    public DataManagerDbContext(
        DbContextOptions<DataManagerDbContext> options)
        : base(options)
    {
    }

    public DbSet<Source> Sources => Set<Source>();
    public DbSet<SourceDatabase> SourceDatabases => Set<SourceDatabase>();
    public DbSet<SourceTable> SourceTables => Set<SourceTable>();
    public DbSet<SourceColumn> SourceColumns => Set<SourceColumn>();
    public DbSet<InScopeRelationalColumn> InScopeRelationalColumns => Set<InScopeRelationalColumn>();

    // ── Schema entities ─────────────────────────────────────────────────────
    public DbSet<SourceIndex> SourceIndexes => Set<SourceIndex>();
    public DbSet<SourceIndexColumn> SourceIndexColumns => Set<SourceIndexColumn>();
    public DbSet<SourceForeignKey> SourceForeignKeys => Set<SourceForeignKey>();
    public DbSet<SourceForeignKeyColumn> SourceForeignKeyColumns => Set<SourceForeignKeyColumn>();
    public DbSet<SourceCheckConstraint> SourceCheckConstraints => Set<SourceCheckConstraint>();
    public DbSet<SourceUniqueConstraint> SourceUniqueConstraints => Set<SourceUniqueConstraint>();
    public DbSet<SourceUniqueConstraintColumn> SourceUniqueConstraintColumns => Set<SourceUniqueConstraintColumn>();
    public DbSet<SourceView> SourceViews => Set<SourceView>();
    public DbSet<SourceViewColumn> SourceViewColumns => Set<SourceViewColumn>();
    public DbSet<SourceStoredProcedure> SourceStoredProcedures => Set<SourceStoredProcedure>();
    public DbSet<SourceStoredProcedureParameter> SourceStoredProcedureParameters => Set<SourceStoredProcedureParameter>();
    public DbSet<SourceFunction> SourceFunctions => Set<SourceFunction>();
    public DbSet<SourceTrigger> SourceTriggers => Set<SourceTrigger>();

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
            e.Property(x => x.LastImportedModelHash).HasMaxLength(64);
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
            e.Property(x => x.SqlType).HasMaxLength(128);
            e.Property(x => x.DefaultValue).HasMaxLength(2000);
            e.Property(x => x.ComputedExpression).HasMaxLength(2000);
            e.Property(x => x.Collation).HasMaxLength(128);
            e.Property(x => x.Description).HasMaxLength(1000);
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

        // ── SourceIndex ──────────────────────────────────────────────────────
        modelBuilder.Entity<SourceIndex>(e =>
        {
            e.ToTable("SourceIndexes");
            e.HasKey(x => x.SourceIndexId);
            e.Property(x => x.Name).IsRequired().HasMaxLength(255);
            e.Property(x => x.FilterDefinition).HasMaxLength(2000);
            e.Property(x => x.IsActive).HasDefaultValue(true);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            e.HasOne(x => x.Table)
                .WithMany(t => t.Indexes)
                .HasForeignKey(x => x.TableId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SourceIndexColumn>(e =>
        {
            e.ToTable("SourceIndexColumns");
            e.HasKey(x => x.SourceIndexColumnId);
            e.Property(x => x.ColumnName).IsRequired().HasMaxLength(255);
            e.Property(x => x.SortOrder).HasMaxLength(4).HasDefaultValue("ASC");
            e.HasOne(x => x.Index)
                .WithMany(i => i.Columns)
                .HasForeignKey(x => x.SourceIndexId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ── SourceForeignKey ─────────────────────────────────────────────────
        modelBuilder.Entity<SourceForeignKey>(e =>
        {
            e.ToTable("SourceForeignKeys");
            e.HasKey(x => x.SourceForeignKeyId);
            e.Property(x => x.Name).IsRequired().HasMaxLength(255);
            e.Property(x => x.ToSchema).IsRequired().HasMaxLength(128);
            e.Property(x => x.ToTable).IsRequired().HasMaxLength(255);
            e.Property(x => x.Cardinality).HasMaxLength(50);
            e.Property(x => x.IsActive).HasDefaultValue(true);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            e.HasOne(x => x.Table)
                .WithMany(t => t.ForeignKeys)
                .HasForeignKey(x => x.TableId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SourceForeignKeyColumn>(e =>
        {
            e.ToTable("SourceForeignKeyColumns");
            e.HasKey(x => x.SourceForeignKeyColumnId);
            e.Property(x => x.FromColumn).IsRequired().HasMaxLength(255);
            e.Property(x => x.ToColumn).IsRequired().HasMaxLength(255);
            e.HasOne(x => x.ForeignKey)
                .WithMany(fk => fk.Columns)
                .HasForeignKey(x => x.SourceForeignKeyId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ── SourceCheckConstraint ────────────────────────────────────────────
        modelBuilder.Entity<SourceCheckConstraint>(e =>
        {
            e.ToTable("SourceCheckConstraints");
            e.HasKey(x => x.SourceCheckConstraintId);
            e.Property(x => x.Name).IsRequired().HasMaxLength(255);
            e.Property(x => x.Expression).IsRequired().HasMaxLength(4000);
            e.Property(x => x.IsActive).HasDefaultValue(true);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            e.HasOne(x => x.Table)
                .WithMany(t => t.CheckConstraints)
                .HasForeignKey(x => x.TableId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ── SourceUniqueConstraint ───────────────────────────────────────────
        modelBuilder.Entity<SourceUniqueConstraint>(e =>
        {
            e.ToTable("SourceUniqueConstraints");
            e.HasKey(x => x.SourceUniqueConstraintId);
            e.Property(x => x.Name).IsRequired().HasMaxLength(255);
            e.Property(x => x.IsActive).HasDefaultValue(true);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            e.HasOne(x => x.Table)
                .WithMany(t => t.UniqueConstraints)
                .HasForeignKey(x => x.TableId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SourceUniqueConstraintColumn>(e =>
        {
            e.ToTable("SourceUniqueConstraintColumns");
            e.HasKey(x => x.SourceUniqueConstraintColumnId);
            e.Property(x => x.ColumnName).IsRequired().HasMaxLength(255);
            e.HasOne(x => x.UniqueConstraint)
                .WithMany(u => u.Columns)
                .HasForeignKey(x => x.SourceUniqueConstraintId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ── SourceView ───────────────────────────────────────────────────────
        modelBuilder.Entity<SourceView>(e =>
        {
            e.ToTable("SourceViews");
            e.HasKey(x => x.SourceViewId);
            e.Property(x => x.SchemaName).IsRequired().HasMaxLength(128).HasDefaultValue("dbo");
            e.Property(x => x.ViewName).IsRequired().HasMaxLength(255);
            e.Property(x => x.SqlBody).HasColumnType("nvarchar(max)");
            e.Property(x => x.IsActive).HasDefaultValue(true);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            e.HasIndex(x => new { x.DatabaseId, x.SchemaName, x.ViewName }).IsUnique()
                .HasDatabaseName("UQ_SourceViews_DbSchemaView");
            e.HasOne(x => x.Database)
                .WithMany(d => d.Views)
                .HasForeignKey(x => x.DatabaseId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SourceViewColumn>(e =>
        {
            e.ToTable("SourceViewColumns");
            e.HasKey(x => x.SourceViewColumnId);
            e.Property(x => x.ColumnName).IsRequired().HasMaxLength(255);
            e.Property(x => x.SqlType).HasMaxLength(128);
            e.HasOne(x => x.View)
                .WithMany(v => v.Columns)
                .HasForeignKey(x => x.SourceViewId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ── SourceStoredProcedure ────────────────────────────────────────────
        modelBuilder.Entity<SourceStoredProcedure>(e =>
        {
            e.ToTable("SourceStoredProcedures");
            e.HasKey(x => x.SourceStoredProcedureId);
            e.Property(x => x.SchemaName).IsRequired().HasMaxLength(128).HasDefaultValue("dbo");
            e.Property(x => x.ProcedureName).IsRequired().HasMaxLength(255);
            e.Property(x => x.SqlBody).HasColumnType("nvarchar(max)");
            e.Property(x => x.IsActive).HasDefaultValue(true);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            e.HasIndex(x => new { x.DatabaseId, x.SchemaName, x.ProcedureName }).IsUnique()
                .HasDatabaseName("UQ_SourceStoredProcedures_DbSchemaProc");
            e.HasOne(x => x.Database)
                .WithMany(d => d.StoredProcedures)
                .HasForeignKey(x => x.DatabaseId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SourceStoredProcedureParameter>(e =>
        {
            e.ToTable("SourceStoredProcedureParameters");
            e.HasKey(x => x.SourceStoredProcedureParameterId);
            e.Property(x => x.Name).IsRequired().HasMaxLength(255);
            e.Property(x => x.SqlType).HasMaxLength(128);
            e.Property(x => x.DefaultValue).HasMaxLength(1000);
            e.HasOne(x => x.StoredProcedure)
                .WithMany(p => p.Parameters)
                .HasForeignKey(x => x.SourceStoredProcedureId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ── SourceFunction ───────────────────────────────────────────────────
        modelBuilder.Entity<SourceFunction>(e =>
        {
            e.ToTable("SourceFunctions");
            e.HasKey(x => x.SourceFunctionId);
            e.Property(x => x.SchemaName).IsRequired().HasMaxLength(128).HasDefaultValue("dbo");
            e.Property(x => x.FunctionName).IsRequired().HasMaxLength(255);
            e.Property(x => x.FunctionType).HasMaxLength(50);
            e.Property(x => x.ReturnType).HasMaxLength(255);
            e.Property(x => x.SqlBody).HasColumnType("nvarchar(max)");
            e.Property(x => x.IsActive).HasDefaultValue(true);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            e.HasIndex(x => new { x.DatabaseId, x.SchemaName, x.FunctionName }).IsUnique()
                .HasDatabaseName("UQ_SourceFunctions_DbSchemaFunc");
            e.HasOne(x => x.Database)
                .WithMany(d => d.Functions)
                .HasForeignKey(x => x.DatabaseId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ── SourceTrigger ────────────────────────────────────────────────────
        modelBuilder.Entity<SourceTrigger>(e =>
        {
            e.ToTable("SourceTriggers");
            e.HasKey(x => x.SourceTriggerId);
            e.Property(x => x.SchemaName).IsRequired().HasMaxLength(128).HasDefaultValue("dbo");
            e.Property(x => x.TriggerName).IsRequired().HasMaxLength(255);
            e.Property(x => x.SqlBody).HasColumnType("nvarchar(max)");
            e.Property(x => x.IsActive).HasDefaultValue(true);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            e.HasOne(x => x.Table)
                .WithMany(t => t.Triggers)
                .HasForeignKey(x => x.TableId)
                .OnDelete(DeleteBehavior.Cascade);
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
            // Schema entities with audit fields
            if (entry.Entity is SourceIndex idx)
            {
                if (entry.State == EntityState.Added) { idx.CreatedAt = now; idx.CreatedBy = user; }
                if (entry.State is EntityState.Modified or EntityState.Added) { idx.ModifiedAt = now; idx.ModifiedBy = user; }
            }
            if (entry.Entity is SourceForeignKey fk)
            {
                if (entry.State == EntityState.Added) { fk.CreatedAt = now; fk.CreatedBy = user; }
                if (entry.State is EntityState.Modified or EntityState.Added) { fk.ModifiedAt = now; fk.ModifiedBy = user; }
            }
            if (entry.Entity is SourceCheckConstraint chk)
            {
                if (entry.State == EntityState.Added) { chk.CreatedAt = now; chk.CreatedBy = user; }
                if (entry.State is EntityState.Modified or EntityState.Added) { chk.ModifiedAt = now; chk.ModifiedBy = user; }
            }
            if (entry.Entity is SourceUniqueConstraint uq)
            {
                if (entry.State == EntityState.Added) { uq.CreatedAt = now; uq.CreatedBy = user; }
                if (entry.State is EntityState.Modified or EntityState.Added) { uq.ModifiedAt = now; uq.ModifiedBy = user; }
            }
            if (entry.Entity is SourceView vw)
            {
                if (entry.State == EntityState.Added) { vw.CreatedAt = now; vw.CreatedBy = user; }
                if (entry.State is EntityState.Modified or EntityState.Added) { vw.ModifiedAt = now; vw.ModifiedBy = user; }
            }
            if (entry.Entity is SourceStoredProcedure sp)
            {
                if (entry.State == EntityState.Added) { sp.CreatedAt = now; sp.CreatedBy = user; }
                if (entry.State is EntityState.Modified or EntityState.Added) { sp.ModifiedAt = now; sp.ModifiedBy = user; }
            }
            if (entry.Entity is SourceFunction fn)
            {
                if (entry.State == EntityState.Added) { fn.CreatedAt = now; fn.CreatedBy = user; }
                if (entry.State is EntityState.Modified or EntityState.Added) { fn.ModifiedAt = now; fn.ModifiedBy = user; }
            }
            if (entry.Entity is SourceTrigger tr)
            {
                if (entry.State == EntityState.Added) { tr.CreatedAt = now; tr.CreatedBy = user; }
                if (entry.State is EntityState.Modified or EntityState.Added) { tr.ModifiedAt = now; tr.ModifiedBy = user; }
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
