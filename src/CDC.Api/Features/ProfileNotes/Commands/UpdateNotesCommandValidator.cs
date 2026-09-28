using CDC.Api.Features.ProfileNotes.Dtos;
using FluentValidation;

namespace CDC.Api.Features.ProfileNotes.Commands;

/// <summary>Validates <see cref="UpdateNotesCommand"/>.</summary>
public sealed class UpdateNotesCommandValidator : AbstractValidator<UpdateNotesCommand>
{
    /// <summary>Initialises a new instance of the <see cref="UpdateNotesCommandValidator"/> class.</summary>
    public UpdateNotesCommandValidator()
    {
        RuleFor(command => command.ProfileVersionId)
            .NotEmpty().WithMessage("A profile version id is required.");

        RuleFor(command => command.NoteTypeId)
            .NotEmpty().WithMessage("A note type id is required.");

        RuleFor(command => command)
            .Must(command => command.Inserts.Count > 0 || command.Updates.Count > 0 || command.Deletes.Count > 0)
            .WithMessage("At least one insert, update or delete is required.");

        RuleFor(command => command)
            .Must(HaveNoDuplicateChanges)
            .WithMessage("Each note may only appear once across the inserts, updates and deletes.");

        RuleForEach(command => command.Inserts).SetValidator(new ProfileNoteInsertDtoValidator());
        RuleForEach(command => command.Updates).SetValidator(new ProfileNoteUpdateDtoValidator());
        RuleForEach(command => command.Deletes).SetValidator(new ProfileNoteDeleteDtoValidator());
    }

    private static bool HaveNoDuplicateChanges(UpdateNotesCommand command)
    {
        var ids = command.Inserts.Select(item => item.Id)
            .Concat(command.Updates.Select(item => item.Id))
            .Concat(command.Deletes.Select(item => item.Id))
            .ToArray();

        return ids.Length == ids.Distinct().Count();
    }
}

/// <summary>Validates a <see cref="ProfileNoteInsertDto"/> within a changeset.</summary>
public sealed class ProfileNoteInsertDtoValidator : AbstractValidator<ProfileNoteInsertDto>
{
    /// <summary>Initialises a new instance of the <see cref="ProfileNoteInsertDtoValidator"/> class.</summary>
    public ProfileNoteInsertDtoValidator()
    {
        RuleFor(note => note.Id)
            .NotEmpty().WithMessage("An id is required for each new note.");

        RuleFor(note => note.NoteText)
            .NotEmpty().WithMessage("Note text is required.");

        RuleForEach(note => note.QuestionReferenceAdds).SetValidator(new QuestionReferenceDtoValidator());
    }
}

/// <summary>Validates a <see cref="ProfileNoteUpdateDto"/> within a changeset.</summary>
public sealed class ProfileNoteUpdateDtoValidator : AbstractValidator<ProfileNoteUpdateDto>
{
    private const int RowVersionLength = 8;

    /// <summary>Initialises a new instance of the <see cref="ProfileNoteUpdateDtoValidator"/> class.</summary>
    public ProfileNoteUpdateDtoValidator()
    {
        RuleFor(note => note.Id)
            .NotEmpty().WithMessage("An id is required for each updated note.");

        RuleFor(note => note.NoteText)
            .NotEmpty().WithMessage("Note text is required.");

        RuleFor(note => note.LastUpdated)
            .NotNull().WithMessage("The row version read with the note is required.")
            .Must(lastUpdated => lastUpdated.Length == RowVersionLength)
                .WithMessage($"The row version must be {RowVersionLength} bytes.");

        RuleForEach(note => note.QuestionReferenceAdds).SetValidator(new QuestionReferenceDtoValidator());
        RuleForEach(note => note.QuestionReferenceRemoves).SetValidator(new QuestionReferenceDtoValidator());
    }
}

/// <summary>Validates a <see cref="ProfileNoteDeleteDto"/> within a changeset.</summary>
public sealed class ProfileNoteDeleteDtoValidator : AbstractValidator<ProfileNoteDeleteDto>
{
    private const int RowVersionLength = 8;

    /// <summary>Initialises a new instance of the <see cref="ProfileNoteDeleteDtoValidator"/> class.</summary>
    public ProfileNoteDeleteDtoValidator()
    {
        RuleFor(note => note.Id)
            .NotEmpty().WithMessage("An id is required for each deleted note.");

        RuleFor(note => note.LastUpdated)
            .NotNull().WithMessage("The row version read with the note is required.")
            .Must(lastUpdated => lastUpdated.Length == RowVersionLength)
                .WithMessage($"The row version must be {RowVersionLength} bytes.");
    }
}

/// <summary>Validates a <see cref="QuestionReferenceDto"/> within a changeset.</summary>
public sealed class QuestionReferenceDtoValidator : AbstractValidator<QuestionReferenceDto>
{
    /// <summary>Initialises a new instance of the <see cref="QuestionReferenceDtoValidator"/> class.</summary>
    public QuestionReferenceDtoValidator()
    {
        RuleFor(reference => reference.ProfileSectionId)
            .NotEmpty().WithMessage("A profile section id is required for each question reference.");

        RuleFor(reference => reference.ProfileQuestionId)
            .NotEmpty().WithMessage("A profile question id is required for each question reference.");
    }
}
