using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataManager.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDacpacModelHash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastImportedAt",
                table: "SourceDatabases",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastImportedModelHash",
                table: "SourceDatabases",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastImportedAt",
                table: "SourceDatabases");

            migrationBuilder.DropColumn(
                name: "LastImportedModelHash",
                table: "SourceDatabases");
        }
    }
}
