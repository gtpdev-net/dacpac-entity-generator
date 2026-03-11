using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataManager.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGenerationFilterIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_SourceColumns_GenerationFilter",
                table: "SourceColumns",
                columns: new[] { "TableId", "SortOrder" },
                filter: "[IsActive] = 1 AND [IsSelectedForLoad] = 1 AND [PersistenceType] != 'D'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SourceColumns_GenerationFilter",
                table: "SourceColumns");
        }
    }
}
