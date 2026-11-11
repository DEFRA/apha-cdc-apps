using CDC.Api.Domain.Common;
using CDC.Api.Domain.Exceptions;
using CDC.Api.Features.ReferenceData.Dtos;
using CDC.Api.Features.ReferenceData.Interfaces;
using FluentValidation;
using MediatR;

namespace CDC.Api.Features.ReferenceData.Commands;

/// <summary>Edits, adds, inactivates or deletes one value of a maintainable reference table, with its audit entry.</summary>
public sealed record ChangeReferenceValueCommand : IRequest<Result<ChangeReferenceValueResultDto>>
{
    /// <summary>Gets the table the value belongs to.</summary>
    public Guid ReferenceTableId { get; init; }

    /// <summary>Gets the kind of change.</summary>
    public ReferenceValueChangeAction Action { get; init; }

    /// <summary>Gets the value being changed. Not used when adding.</summary>
    public Guid? ValueId { get; init; }

    /// <summary>Gets the new lookup text, for an edit or an addition.</summary>
    public string? NewLookupValue { get; init; }

    /// <summary>Gets the reason for the change, recorded in the audit trail.</summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>Gets the row version read with the value. Required unless adding.</summary>
    public byte[]? LastUpdated { get; init; }

    /// <summary>Gets the <c>[User]</c> recorded against the change. Set by the controller, never taken from client input.</summary>
    public Guid UserId { get; init; }
}

/// <summary>Validates <see cref="ChangeReferenceValueCommand"/> before it reaches the database.</summary>
public sealed class ChangeReferenceValueCommandValidator : AbstractValidator<ChangeReferenceValueCommand>
{
    /// <summary>The procedures declare <c>varchar(255)</c> for both the lookup value and the reason.</summary>
    private const int TextMaxLength = 255;

    /// <summary>SQL Server <c>rowversion</c> values are always 8 bytes.</summary>
    private const int RowVersionLength = 8;

    /// <summary>Initialises a new instance of the <see cref="ChangeReferenceValueCommandValidator"/> class.</summary>
    public ChangeReferenceValueCommandValidator()
    {
        RuleFor(command => command.ReferenceTableId).NotEmpty().WithMessage("A reference table id is required.");
        RuleFor(command => command.UserId).NotEmpty().WithMessage("A user id is required to record the audit entry.");
        RuleFor(command => command.Action).IsInEnum().WithMessage("The change action is not recognised.");

        RuleFor(command => command.Reason)
            .NotEmpty().WithMessage("You need to provide a reason for this change")
            .MaximumLength(TextMaxLength).WithMessage($"The reason for change must be no longer than {TextMaxLength} characters.");

        When(command => command.Action is ReferenceValueChangeAction.Edit or ReferenceValueChangeAction.Add, () =>
            RuleFor(command => command.NewLookupValue)
                .NotEmpty().WithMessage("You need to provide a new value")
                .MaximumLength(TextMaxLength).WithMessage($"The new value must be no longer than {TextMaxLength} characters."));

        When(command => command.Action != ReferenceValueChangeAction.Add, () =>
        {
            RuleFor(command => command.ValueId).NotEmpty().WithMessage("A reference value id is required.");
            RuleFor(command => command.LastUpdated)
                .NotNull().WithMessage("The row version read with the reference value is required.")
                .Must(lastUpdated => lastUpdated!.Length == RowVersionLength)
                    .WithMessage($"The row version must be {RowVersionLength} bytes.")
                    .When(command => command.LastUpdated is not null);
        });
    }
}

/// <summary>Handles <see cref="ChangeReferenceValueCommand"/>, applying the legacy <c>ReferenceValueChange</c> rules.</summary>
/// <param name="repository">Reference data maintenance access.</param>
public sealed class ChangeReferenceValueCommandHandler(IReferenceDataMaintenanceRepository repository)
    : IRequestHandler<ChangeReferenceValueCommand, Result<ChangeReferenceValueResultDto>>
{
    /// <summary>Executes the command.</summary>
    /// <param name="request">The change to apply.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The value changed; not found, a duplicate, or a conflict otherwise.</returns>
    public async Task<Result<ChangeReferenceValueResultDto>> Handle(
        ChangeReferenceValueCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var values = await repository.GetMaintainableValuesAsync(request.ReferenceTableId, cancellationToken);

        if (values is null)
        {
            return Result.NotFound<ChangeReferenceValueResultDto>("The reference table does not exist or is not maintainable.");
        }

        try
        {
            return request.Action == ReferenceValueChangeAction.Add
                ? await AddAsync(request, values, cancellationToken)
                : await ChangeAsync(request, values, cancellationToken);
        }
        catch (ConcurrencyException exception)
        {
            return Result.Conflict<ChangeReferenceValueResultDto>(exception.Message);
        }
        catch (DuplicateReferenceValueException exception)
        {
            return Result.ValidationFailed<ChangeReferenceValueResultDto>(exception.Message);
        }
    }

    // The table is case-insensitive, and spuReferenceValue's own duplicate check does not stop the update, so it is checked first.
    private static bool IsDuplicate(IEnumerable<MaintainableReferenceValueDto> values, string lookupValue) =>
        values.Any(value => string.Equals(value.LookupValue, lookupValue, StringComparison.OrdinalIgnoreCase));

    private async Task<Result<ChangeReferenceValueResultDto>> AddAsync(
        ChangeReferenceValueCommand request,
        IReadOnlyList<MaintainableReferenceValueDto> values,
        CancellationToken cancellationToken)
    {
        var newLookupValue = request.NewLookupValue!.Trim();

        if (IsDuplicate(values, newLookupValue))
        {
            return Result.ValidationFailed<ChangeReferenceValueResultDto>(DuplicateReferenceValueException.DefaultMessage);
        }

        // spiReferenceValue takes the new key as an input, as the legacy business object allocated it.
        var valueId = Guid.NewGuid();
        await repository.InsertValueAsync(request.ReferenceTableId, valueId, newLookupValue, request.UserId, request.Reason.Trim(), cancellationToken);

        return Result.Success(new ChangeReferenceValueResultDto { ValueId = valueId });
    }

    private async Task<Result<ChangeReferenceValueResultDto>> ChangeAsync(
        ChangeReferenceValueCommand request,
        IReadOnlyList<MaintainableReferenceValueDto> values,
        CancellationToken cancellationToken)
    {
        var existing = values.FirstOrDefault(value => value.Id == request.ValueId);

        // As legacy: inactive values cannot be changed, and only values no profile references can be deleted.
        if (existing is null || !existing.IsActive || (request.Action == ReferenceValueChangeAction.Delete && existing.IsInUse))
        {
            return Result.NotFound<ChangeReferenceValueResultDto>("The reference value cannot be changed in that way.");
        }

        var reason = request.Reason.Trim();
        var lastUpdated = request.LastUpdated!;

        switch (request.Action)
        {
            case ReferenceValueChangeAction.Inactivate:
                await repository.InactivateValueAsync(existing.Id, request.UserId, reason, lastUpdated, cancellationToken);
                break;

            case ReferenceValueChangeAction.Delete:
                await repository.DeleteValueAsync(existing.Id, existing.LookupValue, request.UserId, reason, lastUpdated, cancellationToken);
                break;

            default:
                var newLookupValue = request.NewLookupValue!.Trim();

                if (IsDuplicate(values, newLookupValue))
                {
                    return Result.ValidationFailed<ChangeReferenceValueResultDto>(DuplicateReferenceValueException.DefaultMessage);
                }

                await repository.UpdateValueAsync(existing.Id, newLookupValue, request.UserId, reason, lastUpdated, cancellationToken);
                break;
        }

        return Result.Success(new ChangeReferenceValueResultDto { ValueId = existing.Id });
    }
}
