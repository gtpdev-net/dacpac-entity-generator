using System;
using System.IO;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataManager.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CopyActivityLog",
                columns: table => new
                {
                    LogId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PipelineRunId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MigrationConfigId = table.Column<int>(type: "int", nullable: false),
                    SourceServer = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    SourceDatabase = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    SourceSchema = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    SourceTable = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    DestinationServer = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    DestinationDatabase = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    DestinationSchema = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    DestinationTable = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowsCopied = table.Column<long>(type: "bigint", nullable: false),
                    StartTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DurationSeconds = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CopyActivityLog", x => x.LogId);
                });

            migrationBuilder.CreateTable(
                name: "Servers",
                columns: table => new
                {
                    ServerId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ServerName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Role = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Source"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Servers", x => x.ServerId);
                });

            migrationBuilder.CreateTable(
                name: "ServerConnections",
                columns: table => new
                {
                    ServerConnectionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ServerId = table.Column<int>(type: "int", nullable: false),
                    Hostname = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Port = table.Column<int>(type: "int", nullable: true),
                    NamedInstance = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    AuthenticationType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Username = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServerConnections", x => x.ServerConnectionId);
                    table.ForeignKey(
                        name: "FK_ServerConnections_Servers_ServerId",
                        column: x => x.ServerId,
                        principalTable: "Servers",
                        principalColumn: "ServerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SourceDatabases",
                columns: table => new
                {
                    DatabaseId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ServerId = table.Column<int>(type: "int", nullable: false),
                    DatabaseName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastImportedModelHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    LastImportedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SourceDatabases", x => x.DatabaseId);
                    table.ForeignKey(
                        name: "FK_SourceDatabases_Servers_ServerId",
                        column: x => x.ServerId,
                        principalTable: "Servers",
                        principalColumn: "ServerId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TargetDatabases",
                columns: table => new
                {
                    TargetDatabaseId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ServerId = table.Column<int>(type: "int", nullable: false),
                    DatabaseName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TargetDatabases", x => x.TargetDatabaseId);
                    table.ForeignKey(
                        name: "FK_TargetDatabases_Servers_ServerId",
                        column: x => x.ServerId,
                        principalTable: "Servers",
                        principalColumn: "ServerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SourceFunctions",
                columns: table => new
                {
                    SourceFunctionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DatabaseId = table.Column<int>(type: "int", nullable: false),
                    SchemaName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false, defaultValue: "dbo"),
                    FunctionName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    FunctionType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ReturnType = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    SqlBody = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SourceFunctions", x => x.SourceFunctionId);
                    table.ForeignKey(
                        name: "FK_SourceFunctions_SourceDatabases_DatabaseId",
                        column: x => x.DatabaseId,
                        principalTable: "SourceDatabases",
                        principalColumn: "DatabaseId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SourceStoredProcedures",
                columns: table => new
                {
                    SourceStoredProcedureId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DatabaseId = table.Column<int>(type: "int", nullable: false),
                    SchemaName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false, defaultValue: "dbo"),
                    ProcedureName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    SqlBody = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SourceStoredProcedures", x => x.SourceStoredProcedureId);
                    table.ForeignKey(
                        name: "FK_SourceStoredProcedures_SourceDatabases_DatabaseId",
                        column: x => x.DatabaseId,
                        principalTable: "SourceDatabases",
                        principalColumn: "DatabaseId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SourceTables",
                columns: table => new
                {
                    TableId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DatabaseId = table.Column<int>(type: "int", nullable: false),
                    SchemaName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false, defaultValue: "dbo"),
                    TableName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    EstimatedRowCount = table.Column<long>(type: "bigint", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SourceTables", x => x.TableId);
                    table.ForeignKey(
                        name: "FK_SourceTables_SourceDatabases_DatabaseId",
                        column: x => x.DatabaseId,
                        principalTable: "SourceDatabases",
                        principalColumn: "DatabaseId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SourceViews",
                columns: table => new
                {
                    SourceViewId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DatabaseId = table.Column<int>(type: "int", nullable: false),
                    SchemaName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false, defaultValue: "dbo"),
                    ViewName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    SqlBody = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    HasStandardAuditColumns = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SourceViews", x => x.SourceViewId);
                    table.ForeignKey(
                        name: "FK_SourceViews_SourceDatabases_DatabaseId",
                        column: x => x.DatabaseId,
                        principalTable: "SourceDatabases",
                        principalColumn: "DatabaseId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SourceStoredProcedureParameters",
                columns: table => new
                {
                    SourceStoredProcedureParameterId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SourceStoredProcedureId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    SqlType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    IsOutput = table.Column<bool>(type: "bit", nullable: false),
                    DefaultValue = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SourceStoredProcedureParameters", x => x.SourceStoredProcedureParameterId);
                    table.ForeignKey(
                        name: "FK_SourceStoredProcedureParameters_SourceStoredProcedures_SourceStoredProcedureId",
                        column: x => x.SourceStoredProcedureId,
                        principalTable: "SourceStoredProcedures",
                        principalColumn: "SourceStoredProcedureId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MigrationConfigs",
                columns: table => new
                {
                    MigrationConfigId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TableId = table.Column<int>(type: "int", nullable: false),
                    SourceServer = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    SourceDatabase = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    SourceSchema = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    SourceTableName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    DestinationServer = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    DestinationDatabase = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    DestinationSchema = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    DestinationTable = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ColumnList = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FilterCondition = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MigrationConfigs", x => x.MigrationConfigId);
                    table.ForeignKey(
                        name: "FK_MigrationConfigs_SourceTables_TableId",
                        column: x => x.TableId,
                        principalTable: "SourceTables",
                        principalColumn: "TableId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SourceCheckConstraints",
                columns: table => new
                {
                    SourceCheckConstraintId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TableId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Expression = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SourceCheckConstraints", x => x.SourceCheckConstraintId);
                    table.ForeignKey(
                        name: "FK_SourceCheckConstraints_SourceTables_TableId",
                        column: x => x.TableId,
                        principalTable: "SourceTables",
                        principalColumn: "TableId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SourceColumns",
                columns: table => new
                {
                    ColumnId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TableId = table.Column<int>(type: "int", nullable: false),
                    ColumnName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    PersistenceType = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: false, defaultValue: "R"),
                    IsInDaoAnalysis = table.Column<bool>(type: "bit", nullable: false),
                    IsAddedByApi = table.Column<bool>(type: "bit", nullable: false),
                    IsSelectedForLoad = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SqlType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    IsNullable = table.Column<bool>(type: "bit", nullable: false),
                    MaxLength = table.Column<int>(type: "int", nullable: true),
                    IsIdentity = table.Column<bool>(type: "bit", nullable: false),
                    IsPrimaryKey = table.Column<bool>(type: "bit", nullable: false),
                    Precision = table.Column<int>(type: "int", nullable: true),
                    Scale = table.Column<int>(type: "int", nullable: true),
                    DefaultValue = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IsComputed = table.Column<bool>(type: "bit", nullable: false),
                    IsComputedPersisted = table.Column<bool>(type: "bit", nullable: false),
                    ComputedExpression = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IsRowVersion = table.Column<bool>(type: "bit", nullable: false),
                    IsConcurrencyToken = table.Column<bool>(type: "bit", nullable: false),
                    Collation = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SourceColumns", x => x.ColumnId);
                    table.CheckConstraint("CK_SourceColumns_PersistenceType", "[PersistenceType] IN ('R', 'D')");
                    table.ForeignKey(
                        name: "FK_SourceColumns_SourceTables_TableId",
                        column: x => x.TableId,
                        principalTable: "SourceTables",
                        principalColumn: "TableId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SourceForeignKeys",
                columns: table => new
                {
                    SourceForeignKeyId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TableId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ToSchema = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ToTable = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    OnDeleteCascade = table.Column<bool>(type: "bit", nullable: false),
                    OnUpdateCascade = table.Column<bool>(type: "bit", nullable: false),
                    Cardinality = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SourceForeignKeys", x => x.SourceForeignKeyId);
                    table.ForeignKey(
                        name: "FK_SourceForeignKeys_SourceTables_TableId",
                        column: x => x.TableId,
                        principalTable: "SourceTables",
                        principalColumn: "TableId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SourceIndexes",
                columns: table => new
                {
                    SourceIndexId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TableId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    IsUnique = table.Column<bool>(type: "bit", nullable: false),
                    IsClustered = table.Column<bool>(type: "bit", nullable: false),
                    IsPrimaryKeyIndex = table.Column<bool>(type: "bit", nullable: false),
                    FilterDefinition = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SourceIndexes", x => x.SourceIndexId);
                    table.ForeignKey(
                        name: "FK_SourceIndexes_SourceTables_TableId",
                        column: x => x.TableId,
                        principalTable: "SourceTables",
                        principalColumn: "TableId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SourceTriggers",
                columns: table => new
                {
                    SourceTriggerId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TableId = table.Column<int>(type: "int", nullable: false),
                    SchemaName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false, defaultValue: "dbo"),
                    TriggerName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    SqlBody = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SourceTriggers", x => x.SourceTriggerId);
                    table.ForeignKey(
                        name: "FK_SourceTriggers_SourceTables_TableId",
                        column: x => x.TableId,
                        principalTable: "SourceTables",
                        principalColumn: "TableId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SourceUniqueConstraints",
                columns: table => new
                {
                    SourceUniqueConstraintId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TableId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    IsClustered = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SourceUniqueConstraints", x => x.SourceUniqueConstraintId);
                    table.ForeignKey(
                        name: "FK_SourceUniqueConstraints_SourceTables_TableId",
                        column: x => x.TableId,
                        principalTable: "SourceTables",
                        principalColumn: "TableId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SourceViewColumns",
                columns: table => new
                {
                    SourceViewColumnId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SourceViewId = table.Column<int>(type: "int", nullable: false),
                    ColumnName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    SqlType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    IsNullable = table.Column<bool>(type: "bit", nullable: false),
                    MaxLength = table.Column<int>(type: "int", nullable: true),
                    Precision = table.Column<int>(type: "int", nullable: true),
                    Scale = table.Column<int>(type: "int", nullable: true),
                    OrdinalPosition = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SourceViewColumns", x => x.SourceViewColumnId);
                    table.ForeignKey(
                        name: "FK_SourceViewColumns_SourceViews_SourceViewId",
                        column: x => x.SourceViewId,
                        principalTable: "SourceViews",
                        principalColumn: "SourceViewId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SourceForeignKeyColumns",
                columns: table => new
                {
                    SourceForeignKeyColumnId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SourceForeignKeyId = table.Column<int>(type: "int", nullable: false),
                    FromColumn = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ToColumn = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Ordinal = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SourceForeignKeyColumns", x => x.SourceForeignKeyColumnId);
                    table.ForeignKey(
                        name: "FK_SourceForeignKeyColumns_SourceForeignKeys_SourceForeignKeyId",
                        column: x => x.SourceForeignKeyId,
                        principalTable: "SourceForeignKeys",
                        principalColumn: "SourceForeignKeyId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SourceIndexColumns",
                columns: table => new
                {
                    SourceIndexColumnId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SourceIndexId = table.Column<int>(type: "int", nullable: false),
                    ColumnName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    SortOrder = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: false, defaultValue: "ASC"),
                    IsIncludedColumn = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SourceIndexColumns", x => x.SourceIndexColumnId);
                    table.ForeignKey(
                        name: "FK_SourceIndexColumns_SourceIndexes_SourceIndexId",
                        column: x => x.SourceIndexId,
                        principalTable: "SourceIndexes",
                        principalColumn: "SourceIndexId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SourceUniqueConstraintColumns",
                columns: table => new
                {
                    SourceUniqueConstraintColumnId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SourceUniqueConstraintId = table.Column<int>(type: "int", nullable: false),
                    ColumnName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SourceUniqueConstraintColumns", x => x.SourceUniqueConstraintColumnId);
                    table.ForeignKey(
                        name: "FK_SourceUniqueConstraintColumns_SourceUniqueConstraints_SourceUniqueConstraintId",
                        column: x => x.SourceUniqueConstraintId,
                        principalTable: "SourceUniqueConstraints",
                        principalColumn: "SourceUniqueConstraintId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "UQ_MigrationConfigs_TableId",
                table: "MigrationConfigs",
                column: "TableId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_ServerConnections_ServerId",
                table: "ServerConnections",
                column: "ServerId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_Servers_ServerName",
                table: "Servers",
                column: "ServerName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SourceCheckConstraints_TableId",
                table: "SourceCheckConstraints",
                column: "TableId");

            migrationBuilder.CreateIndex(
                name: "UQ_SourceColumns_TableColumn",
                table: "SourceColumns",
                columns: new[] { "TableId", "ColumnName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_SourceDatabases_ServerDb",
                table: "SourceDatabases",
                columns: new[] { "ServerId", "DatabaseName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SourceForeignKeyColumns_SourceForeignKeyId",
                table: "SourceForeignKeyColumns",
                column: "SourceForeignKeyId");

            migrationBuilder.CreateIndex(
                name: "IX_SourceForeignKeys_TableId",
                table: "SourceForeignKeys",
                column: "TableId");

            migrationBuilder.CreateIndex(
                name: "UQ_SourceFunctions_DbSchemaFunc",
                table: "SourceFunctions",
                columns: new[] { "DatabaseId", "SchemaName", "FunctionName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SourceIndexColumns_SourceIndexId",
                table: "SourceIndexColumns",
                column: "SourceIndexId");

            migrationBuilder.CreateIndex(
                name: "IX_SourceIndexes_TableId",
                table: "SourceIndexes",
                column: "TableId");

            migrationBuilder.CreateIndex(
                name: "IX_SourceStoredProcedureParameters_SourceStoredProcedureId",
                table: "SourceStoredProcedureParameters",
                column: "SourceStoredProcedureId");

            migrationBuilder.CreateIndex(
                name: "UQ_SourceStoredProcedures_DbSchemaProc",
                table: "SourceStoredProcedures",
                columns: new[] { "DatabaseId", "SchemaName", "ProcedureName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_SourceTables_SchemaTable",
                table: "SourceTables",
                columns: new[] { "DatabaseId", "SchemaName", "TableName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SourceTriggers_TableId",
                table: "SourceTriggers",
                column: "TableId");

            migrationBuilder.CreateIndex(
                name: "IX_SourceUniqueConstraintColumns_SourceUniqueConstraintId",
                table: "SourceUniqueConstraintColumns",
                column: "SourceUniqueConstraintId");

            migrationBuilder.CreateIndex(
                name: "IX_SourceUniqueConstraints_TableId",
                table: "SourceUniqueConstraints",
                column: "TableId");

            migrationBuilder.CreateIndex(
                name: "IX_SourceViewColumns_SourceViewId",
                table: "SourceViewColumns",
                column: "SourceViewId");

            migrationBuilder.CreateIndex(
                name: "UQ_SourceViews_DbSchemaView",
                table: "SourceViews",
                columns: new[] { "DatabaseId", "SchemaName", "ViewName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_TargetDatabases_ServerDb",
                table: "TargetDatabases",
                columns: new[] { "ServerId", "DatabaseName" },
                unique: true);

            var uspLogCopySuccessPath = Path.Combine(AppContext.BaseDirectory, "Models/StoredProcedures/usp_LogCopySuccess.sql");
            migrationBuilder.Sql(File.ReadAllText(uspLogCopySuccessPath));

            var uspLogCopyFailurePath = Path.Combine(AppContext.BaseDirectory, "Models/StoredProcedures/usp_LogCopyFailure.sql");
            migrationBuilder.Sql(File.ReadAllText(uspLogCopyFailurePath));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.usp_LogCopySuccess");
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.usp_LogCopyFailure");

            migrationBuilder.DropTable(
                name: "CopyActivityLog");

            migrationBuilder.DropTable(
                name: "MigrationConfigs");

            migrationBuilder.DropTable(
                name: "ServerConnections");

            migrationBuilder.DropTable(
                name: "SourceCheckConstraints");

            migrationBuilder.DropTable(
                name: "SourceColumns");

            migrationBuilder.DropTable(
                name: "SourceForeignKeyColumns");

            migrationBuilder.DropTable(
                name: "SourceFunctions");

            migrationBuilder.DropTable(
                name: "SourceIndexColumns");

            migrationBuilder.DropTable(
                name: "SourceStoredProcedureParameters");

            migrationBuilder.DropTable(
                name: "SourceTriggers");

            migrationBuilder.DropTable(
                name: "SourceUniqueConstraintColumns");

            migrationBuilder.DropTable(
                name: "SourceViewColumns");

            migrationBuilder.DropTable(
                name: "TargetDatabases");

            migrationBuilder.DropTable(
                name: "SourceForeignKeys");

            migrationBuilder.DropTable(
                name: "SourceIndexes");

            migrationBuilder.DropTable(
                name: "SourceStoredProcedures");

            migrationBuilder.DropTable(
                name: "SourceUniqueConstraints");

            migrationBuilder.DropTable(
                name: "SourceViews");

            migrationBuilder.DropTable(
                name: "SourceTables");

            migrationBuilder.DropTable(
                name: "SourceDatabases");

            migrationBuilder.DropTable(
                name: "Servers");
        }
    }
}
