using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataManager.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMigrationConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

            migrationBuilder.CreateIndex(
                name: "UQ_MigrationConfigs_TableId",
                table: "MigrationConfigs",
                column: "TableId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MigrationConfigs");
        }
    }
}
