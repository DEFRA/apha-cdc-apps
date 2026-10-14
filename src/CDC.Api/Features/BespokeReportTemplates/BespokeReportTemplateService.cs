using CDC.Api.Application;
using CDC.Api.Domain.Common;
using CDC.Api.Domain.Entities;
using CDC.Api.Features.BespokeReportTemplates.Interfaces;
using CDC.Common.Contracts;
using MediatR;

namespace CDC.Api.Features.BespokeReportTemplates;

/// <summary>Default <see cref="IBespokeReportTemplateService"/>.</summary>
/// <param name="repository">Reads and writes the template tables.</param>
/// <param name="userContext">The current user's profile-authoring role flags.</param>
/// <param name="logger">Structured logger.</param>
public sealed class BespokeReportTemplateService(
    IBespokeReportTemplateRepository repository,
    IUserContext userContext,
    ILogger<BespokeReportTemplateService> logger)
    : IBespokeReportTemplateService
{
    /// <summary>The message returned when a title is already in use, as the legacy page showed it.</summary>
    public const string TitleInUseMessage = "Template with that title already exists.";

    private const string ViewPermissionMessage = "You do not have permission to generate bespoke reports.";
    private const string EditPermissionMessage = "You do not have permission to change bespoke report templates.";
    private const string NotFoundMessage = "The bespoke report template does not exist.";

    // Legacy BespokeReportTemplateService.CanView() and CanEdit().
    private bool CanView => (userContext.IsProfileEditor || userContext.IsPolicyProfileUser) && !userContext.IsUserManagementSystem;

    private bool CanEdit => userContext.IsProfileEditor && !userContext.IsUserManagementSystem;

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<BespokeReportTemplateSummaryDto>>> GetTemplatesAsync(CancellationToken cancellationToken)
    {
        if (!CanView)
        {
            logger.TemplateRequestRefused("view");

            return Result.Forbidden<IReadOnlyList<BespokeReportTemplateSummaryDto>>(ViewPermissionMessage);
        }

        var templates = await repository.GetAllAsync(cancellationToken);
        logger.RetrievedTemplates(templates.Count);

        return Result.Success<IReadOnlyList<BespokeReportTemplateSummaryDto>>(
            [.. templates.Select(template => new BespokeReportTemplateSummaryDto { Id = template.Id, Title = template.Title })]);
    }

    /// <inheritdoc />
    public async Task<Result<BespokeReportTemplateDto>> GetTemplateAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!CanView)
        {
            logger.TemplateRequestRefused("view");

            return Result.Forbidden<BespokeReportTemplateDto>(ViewPermissionMessage);
        }

        var template = await repository.GetAsync(id, cancellationToken);

        return template is null
            ? Result.NotFound<BespokeReportTemplateDto>(NotFoundMessage)
            : Result.Success(ToDto(template));
    }

    /// <inheritdoc />
    public async Task<Result<BespokeReportTemplateDto>> SaveAsNewAsync(string title, IReadOnlyList<string> checkedList, CancellationToken cancellationToken)
    {
        if (!CanEdit)
        {
            logger.TemplateRequestRefused("change");

            return Result.Forbidden<BespokeReportTemplateDto>(EditPermissionMessage);
        }

        var trimmed = title.Trim();

        if (await repository.GetByTitleAsync(trimmed, cancellationToken) is not null)
        {
            logger.TemplateTitleInUse();

            return Result.Conflict<BespokeReportTemplateDto>(TitleInUseMessage);
        }

        var checkedItems = Distinct(checkedList);
        var id = await repository.InsertAsync(trimmed, checkedItems, cancellationToken);
        logger.SavedTemplate(id, checkedItems.Count);

        return Result.Success(new BespokeReportTemplateDto { Id = id, Title = trimmed, CheckedList = checkedItems });
    }

    /// <inheritdoc />
    public async Task<Result<BespokeReportTemplateDto>> UpdateCheckedListAsync(Guid id, IReadOnlyList<string> checkedList, CancellationToken cancellationToken)
    {
        if (!CanEdit)
        {
            logger.TemplateRequestRefused("change");

            return Result.Forbidden<BespokeReportTemplateDto>(EditPermissionMessage);
        }

        var template = await repository.GetAsync(id, cancellationToken);

        if (template is null)
        {
            return Result.NotFound<BespokeReportTemplateDto>(NotFoundMessage);
        }

        var checkedItems = Distinct(checkedList);
        await repository.ReplaceCheckedItemsAsync(id, checkedItems, cancellationToken);
        logger.UpdatedTemplate(id, checkedItems.Count);

        return Result.Success(ToDto(template with { CheckedItems = checkedItems }));
    }

    /// <inheritdoc />
    public async Task<Result<BespokeReportTemplateDto>> RenameAsync(Guid id, string title, CancellationToken cancellationToken)
    {
        if (!CanEdit)
        {
            logger.TemplateRequestRefused("change");

            return Result.Forbidden<BespokeReportTemplateDto>(EditPermissionMessage);
        }

        var template = await repository.GetAsync(id, cancellationToken);

        if (template is null)
        {
            return Result.NotFound<BespokeReportTemplateDto>(NotFoundMessage);
        }

        var trimmed = title.Trim();

        if (await repository.GetByTitleAsync(trimmed, cancellationToken) is { } sameTitle && sameTitle.Id != id)
        {
            logger.TemplateTitleInUse();

            return Result.Conflict<BespokeReportTemplateDto>(TitleInUseMessage);
        }

        await repository.UpdateTitleAsync(id, trimmed, cancellationToken);
        logger.RenamedTemplate(id);

        return Result.Success(ToDto(template with { Title = trimmed }));
    }

    /// <inheritdoc />
    public async Task<Result<Unit>> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!CanEdit)
        {
            logger.TemplateRequestRefused("change");

            return Result.Forbidden<Unit>(EditPermissionMessage);
        }

        if (await repository.GetAsync(id, cancellationToken) is null)
        {
            return Result.NotFound<Unit>(NotFoundMessage);
        }

        await repository.DeleteAsync(id, cancellationToken);
        logger.DeletedTemplate(id);

        return Result.Success(Unit.Value);
    }

    private static BespokeReportTemplateDto ToDto(BespokeReportTemplate template) =>
        new() { Id = template.Id, Title = template.Title, CheckedList = template.CheckedItems };

    private static List<string> Distinct(IReadOnlyList<string> checkedList) => [.. checkedList.Distinct(StringComparer.Ordinal)];
}
