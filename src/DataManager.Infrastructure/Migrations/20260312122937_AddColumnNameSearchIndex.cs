using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataManager.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddColumnNameSearchIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_SourceColumns_ColumnName",
                table: "SourceColumns",
                column: "ColumnName")
                .Annotation("SqlServer:Include", new[] { "TableId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SourceColumns_ColumnName",
                table: "SourceColumns");
        }
    }
}
