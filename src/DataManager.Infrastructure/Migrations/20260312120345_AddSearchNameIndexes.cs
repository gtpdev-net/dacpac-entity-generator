using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataManager.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSearchNameIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_SourceViews_ViewName",
                table: "SourceViews",
                column: "ViewName")
                .Annotation("SqlServer:Include", new[] { "DatabaseId", "SchemaName" });

            migrationBuilder.CreateIndex(
                name: "IX_SourceTables_TableName",
                table: "SourceTables",
                column: "TableName")
                .Annotation("SqlServer:Include", new[] { "DatabaseId", "SchemaName" });

            migrationBuilder.CreateIndex(
                name: "IX_SourceStoredProcedures_ProcedureName",
                table: "SourceStoredProcedures",
                column: "ProcedureName")
                .Annotation("SqlServer:Include", new[] { "DatabaseId", "SchemaName" });

            migrationBuilder.CreateIndex(
                name: "IX_SourceFunctions_FunctionName",
                table: "SourceFunctions",
                column: "FunctionName")
                .Annotation("SqlServer:Include", new[] { "DatabaseId", "SchemaName" });

            migrationBuilder.CreateIndex(
                name: "IX_SourceDatabases_DatabaseName",
                table: "SourceDatabases",
                column: "DatabaseName")
                .Annotation("SqlServer:Include", new[] { "ServerId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SourceViews_ViewName",
                table: "SourceViews");

            migrationBuilder.DropIndex(
                name: "IX_SourceTables_TableName",
                table: "SourceTables");

            migrationBuilder.DropIndex(
                name: "IX_SourceStoredProcedures_ProcedureName",
                table: "SourceStoredProcedures");

            migrationBuilder.DropIndex(
                name: "IX_SourceFunctions_FunctionName",
                table: "SourceFunctions");

            migrationBuilder.DropIndex(
                name: "IX_SourceDatabases_DatabaseName",
                table: "SourceDatabases");
        }
    }
}
