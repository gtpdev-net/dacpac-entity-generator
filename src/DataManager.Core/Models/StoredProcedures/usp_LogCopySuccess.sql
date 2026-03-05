/*
   This stored procedure is deployed by the Entity Framework Core migrations and should be included in the database before any copy operations are attempted.
   The following snippet needs to be added to the EF migration that creates the CopyActivityLog table to ensure the stored procedure is available for logging successes.

   protected override void Up(MigrationBuilder migrationBuilder)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Models/StoredProcedures/usp_LogCopySuccess.sql");
        var sql = File.ReadAllText(path);
        migrationBuilder.Sql(sql);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP PROCEDURE dbo.usp_LogCopySuccess");
    }
*/
CREATE OR ALTER PROCEDURE dbo.usp_LogCopySuccess
    @PipelineRunId       NVARCHAR(100),
    @MigrationConfigId   INT,
    @SourceServer        NVARCHAR(255),
    @SourceDatabase      NVARCHAR(255),
    @SourceSchema        NVARCHAR(255),
    @SourceTable         NVARCHAR(255),
    @DestinationServer   NVARCHAR(255),
    @DestinationDatabase NVARCHAR(255),
    @DestinationSchema   NVARCHAR(255),
    @DestinationTable    NVARCHAR(255),
    @RowsCopied          BIGINT,
    @StartTime           DATETIME2,
    @EndTime             DATETIME2
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
END