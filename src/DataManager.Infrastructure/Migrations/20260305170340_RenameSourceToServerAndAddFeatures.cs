using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataManager.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameSourceToServerAndAddFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ServerConnections_Sources_SourceId",
                table: "ServerConnections");

            migrationBuilder.DropForeignKey(
                name: "FK_SourceDatabases_Sources_SourceId",
                table: "SourceDatabases");

            migrationBuilder.DropForeignKey(
                name: "FK_TargetDatabases_Sources_SourceId",
                table: "TargetDatabases");

            // Rename Sources table and its primary-key column
            migrationBuilder.RenameTable(
                name: "Sources",
                newName: "Servers");

            migrationBuilder.RenameColumn(
                name: "SourceId",
                table: "Servers",
                newName: "ServerId");

            migrationBuilder.RenameIndex(
                name: "UQ_Sources_ServerName",
                table: "Servers",
                newName: "UQ_Servers_ServerName");

            // Rename FK columns on child tables
            migrationBuilder.RenameColumn(
                name: "SourceId",
                table: "TargetDatabases",
                newName: "ServerId");

            migrationBuilder.RenameIndex(
                name: "UQ_TargetDatabases_SourceDb",
                table: "TargetDatabases",
                newName: "UQ_TargetDatabases_ServerDb");

            migrationBuilder.RenameColumn(
                name: "SourceId",
                table: "SourceDatabases",
                newName: "ServerId");

            migrationBuilder.RenameColumn(
                name: "SourceId",
                table: "ServerConnections",
                newName: "ServerId");

            migrationBuilder.RenameIndex(
                name: "UQ_ServerConnections_SourceId",
                table: "ServerConnections",
                newName: "UQ_ServerConnections_ServerId");

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

            migrationBuilder.AddForeignKey(
                name: "FK_ServerConnections_Servers_ServerId",
                table: "ServerConnections",
                column: "ServerId",
                principalTable: "Servers",
                principalColumn: "ServerId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SourceDatabases_Servers_ServerId",
                table: "SourceDatabases",
                column: "ServerId",
                principalTable: "Servers",
                principalColumn: "ServerId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TargetDatabases_Servers_ServerId",
                table: "TargetDatabases",
                column: "ServerId",
                principalTable: "Servers",
                principalColumn: "ServerId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.Sql(@"
CREATE OR ALTER PROCEDURE dbo.usp_LogCopySuccess
    @PipelineRunId      NVARCHAR(100),
    @MigrationConfigId  INT,
    @SourceServer       NVARCHAR(255),
    @SourceDatabase     NVARCHAR(255),
    @SourceSchema       NVARCHAR(255),
    @SourceTable        NVARCHAR(255),
    @DestinationServer  NVARCHAR(255),
    @DestinationDatabase NVARCHAR(255),
    @DestinationSchema  NVARCHAR(255),
    @DestinationTable   NVARCHAR(255),
    @RowsCopied         BIGINT,
    @StartTime          DATETIME2,
    @EndTime            DATETIME2
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.CopyActivityLog
        (PipelineRunId, MigrationConfigId,
         SourceServer, SourceDatabase, SourceSchema, SourceTable,
         DestinationServer, DestinationDatabase, DestinationSchema, DestinationTable,
         Status, ErrorMessage, RowsCopied, StartTime, EndTime, DurationSeconds)
    VALUES
        (@PipelineRunId, @MigrationConfigId,
         @SourceServer, @SourceDatabase, @SourceSchema, @SourceTable,
         @DestinationServer, @DestinationDatabase, @DestinationSchema, @DestinationTable,
         'Success', NULL, @RowsCopied, @StartTime, @EndTime,
         DATEDIFF(SECOND, @StartTime, @EndTime));
END");

            migrationBuilder.Sql(@"
CREATE OR ALTER PROCEDURE dbo.usp_LogCopyFailure
    @PipelineRunId      NVARCHAR(100),
    @MigrationConfigId  INT,
    @SourceServer       NVARCHAR(255),
    @SourceDatabase     NVARCHAR(255),
    @SourceSchema       NVARCHAR(255),
    @SourceTable        NVARCHAR(255),
    @DestinationServer  NVARCHAR(255),
    @DestinationDatabase NVARCHAR(255),
    @DestinationSchema  NVARCHAR(255),
    @DestinationTable   NVARCHAR(255),
    @ErrorMessage       NVARCHAR(MAX),
    @StartTime          DATETIME2,
    @EndTime            DATETIME2
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.CopyActivityLog
        (PipelineRunId, MigrationConfigId,
         SourceServer, SourceDatabase, SourceSchema, SourceTable,
         DestinationServer, DestinationDatabase, DestinationSchema, DestinationTable,
         Status, ErrorMessage, RowsCopied, StartTime, EndTime, DurationSeconds)
    VALUES
        (@PipelineRunId, @MigrationConfigId,
         @SourceServer, @SourceDatabase, @SourceSchema, @SourceTable,
         @DestinationServer, @DestinationDatabase, @DestinationSchema, @DestinationTable,
         'Failure', @ErrorMessage, 0, @StartTime, @EndTime,
         DATEDIFF(SECOND, @StartTime, @EndTime));
END");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.usp_LogCopySuccess;");
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.usp_LogCopyFailure;");

            migrationBuilder.DropForeignKey(
                name: "FK_ServerConnections_Servers_ServerId",
                table: "ServerConnections");

            migrationBuilder.DropForeignKey(
                name: "FK_SourceDatabases_Servers_ServerId",
                table: "SourceDatabases");

            migrationBuilder.DropForeignKey(
                name: "FK_TargetDatabases_Servers_ServerId",
                table: "TargetDatabases");

            migrationBuilder.DropTable(
                name: "CopyActivityLog");

            // Rename Servers back to Sources
            migrationBuilder.RenameColumn(
                name: "ServerId",
                table: "Servers",
                newName: "SourceId");

            migrationBuilder.RenameIndex(
                name: "UQ_Servers_ServerName",
                table: "Servers",
                newName: "UQ_Sources_ServerName");

            migrationBuilder.RenameTable(
                name: "Servers",
                newName: "Sources");

            migrationBuilder.RenameColumn(
                name: "ServerId",
                table: "TargetDatabases",
                newName: "SourceId");

            migrationBuilder.RenameIndex(
                name: "UQ_TargetDatabases_ServerDb",
                table: "TargetDatabases",
                newName: "UQ_TargetDatabases_SourceDb");

            migrationBuilder.RenameColumn(
                name: "ServerId",
                table: "SourceDatabases",
                newName: "SourceId");

            migrationBuilder.RenameColumn(
                name: "ServerId",
                table: "ServerConnections",
                newName: "SourceId");

            migrationBuilder.RenameIndex(
                name: "UQ_ServerConnections_ServerId",
                table: "ServerConnections",
                newName: "UQ_ServerConnections_SourceId");

            migrationBuilder.AddForeignKey(
                name: "FK_ServerConnections_Sources_SourceId",
                table: "ServerConnections",
                column: "SourceId",
                principalTable: "Sources",
                principalColumn: "SourceId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SourceDatabases_Sources_SourceId",
                table: "SourceDatabases",
                column: "SourceId",
                principalTable: "Sources",
                principalColumn: "SourceId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TargetDatabases_Sources_SourceId",
                table: "TargetDatabases",
                column: "SourceId",
                principalTable: "Sources",
                principalColumn: "SourceId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
