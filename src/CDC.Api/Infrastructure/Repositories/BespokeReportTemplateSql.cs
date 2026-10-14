namespace CDC.Api.Infrastructure.Repositories;

/// <summary>The text commands behind <see cref="BespokeReportTemplateRepository"/>.</summary>
public static class BespokeReportTemplateSql
{
    /// <summary>Lists every template's identifier and title, ordered by title.</summary>
    public const string GetAll = "SELECT [Id], [Title] FROM [dbo].[BespokeReportTemplate] ORDER BY [Title]";

    /// <summary>Reads one template's identifier and title.</summary>
    public const string GetById = "SELECT [Id], [Title] FROM [dbo].[BespokeReportTemplate] WHERE [Id] = @Id";

    /// <summary>Finds a template by title.</summary>
    public const string GetByTitle = "SELECT [Id], [Title] FROM [dbo].[BespokeReportTemplate] WHERE [Title] = @Title";

    /// <summary>Reads a template's ticked items.</summary>
    public const string GetCheckedItems = "SELECT [CheckedId] FROM [dbo].[BespokeReportTemplateCheckedItems] WHERE [BespokeReportTemplateId] = @Id";

    /// <summary>Inserts a template.</summary>
    public const string InsertTemplate = "INSERT INTO [dbo].[BespokeReportTemplate] ([Id], [Title]) VALUES (@Id, @Title)";

    /// <summary>Changes a template's title.</summary>
    public const string UpdateTitle = "UPDATE [dbo].[BespokeReportTemplate] SET [Title] = @Title WHERE [Id] = @Id";

    /// <summary>Inserts one ticked item.</summary>
    public const string InsertCheckedItem = "INSERT INTO [dbo].[BespokeReportTemplateCheckedItems] ([Id], [BespokeReportTemplateId], [CheckedId]) VALUES (@Id, @TemplateId, @CheckedId)";

    /// <summary>Deletes a template's ticked items.</summary>
    public const string DeleteCheckedItems = "DELETE FROM [dbo].[BespokeReportTemplateCheckedItems] WHERE [BespokeReportTemplateId] = @Id";

    /// <summary>Deletes a template.</summary>
    public const string DeleteTemplate = "DELETE FROM [dbo].[BespokeReportTemplate] WHERE [Id] = @Id";
}
