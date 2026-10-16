/****** Seeds the "D2R2 Quality Statement" static report, which legacy had as an uploaded
        document and which the footer link (QualityStatement page) looks up by this exact title.
        Run with: sqlcmd -S <server> -d <database> -v PdfPath="<full path to DataQualityStatement_D2R2.pdf>" -i 0070-SeedD2R2QualityStatement.sql
        The file is read by SQL Server, so PdfPath must be reachable from the server.
        Does nothing if a current version already exists; publish later updates by uploading a new
        version through the Static Reports page (spiStaticReport then supersedes the old version). ******/
SET NOCOUNT ON;

IF EXISTS (
	SELECT 1
	FROM [dbo].[StaticReportVersion]
	WHERE [Title] = 'D2R2 Quality Statement' AND [EffectiveDateTo] IS NULL)
BEGIN
	PRINT 'A current "D2R2 Quality Statement" already exists - nothing to do.';
END
ELSE
BEGIN
	DECLARE @PdfData varbinary(max);
	DECLARE @Sql nvarchar(max) = N'SELECT @Data = BulkColumn FROM OPENROWSET(BULK ''$(PdfPath)'', SINGLE_BLOB) AS d';

	EXEC sp_executesql @Sql, N'@Data varbinary(max) OUTPUT', @Data = @PdfData OUTPUT;

	EXEC [dbo].[spiStaticReport]
		@Title = 'D2R2 Quality Statement',
		@PdfData = @PdfData,
		@IsUserManual = 1,
		@IsPublic = 0;

	PRINT 'Seeded "D2R2 Quality Statement".';
END

GO
