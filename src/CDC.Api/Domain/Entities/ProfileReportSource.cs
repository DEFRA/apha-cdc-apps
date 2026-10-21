namespace CDC.Api.Domain.Entities;

/// <summary>The profile questionnaire as the report builder needs it, including section notes and relevance rules.</summary>
public sealed record ReportQuestionnaire
{
    /// <summary>Gets the sections in section-number order.</summary>
    public IReadOnlyList<ReportSection> Sections { get; init; } = [];
}

/// <summary>One section of the questionnaire.</summary>
public sealed record ReportSection
{
    /// <summary>Gets the section identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the section's number.</summary>
    public int SectionNumber { get; init; }

    /// <summary>Gets the section's name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Gets the section's abbreviated name.</summary>
    public string ShortName { get; init; } = string.Empty;

    /// <summary>Gets the section's standing note, or empty.</summary>
    public string Note { get; init; } = string.Empty;

    /// <summary>Gets the section's questions in question-number order.</summary>
    public IReadOnlyList<ReportQuestion> Questions { get; init; } = [];
}

/// <summary>One question of the questionnaire, with the answer that makes it irrelevant.</summary>
public sealed record ReportQuestion
{
    /// <summary>Gets the question identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the question's number within its section.</summary>
    public int QuestionNumber { get; init; }

    /// <summary>Gets the question's short name, which the page's question anchors are built from.</summary>
    public string ShortName { get; init; } = string.Empty;

    /// <summary>Gets a value indicating whether the question is answered once per profiled species.</summary>
    public bool IsPerSpecies { get; init; }

    /// <summary>Gets a value indicating whether the question allows repeating answer rows.</summary>
    public bool IsRepeating { get; init; }

    /// <summary>Gets the field whose answer controls the question's relevance, or <see cref="Guid.Empty"/> when it is always relevant.</summary>
    public Guid IrrelevanceFieldId { get; init; }

    /// <summary>Gets the list value that controls relevance when the controlling field is a list, otherwise <see cref="Guid.Empty"/>.</summary>
    public Guid IrrelevanceValueId { get; init; }

    /// <summary>Gets the flag that controls relevance: for a boolean field the answer that makes the question irrelevant, for a list field whether the value marks the question relevant.</summary>
    public bool IrrelevanceFlag { get; init; }

    /// <summary>Gets the question's fields in field-number order.</summary>
    public IReadOnlyList<ReportField> Fields { get; init; } = [];
}

/// <summary>One field of a question.</summary>
public sealed record ReportField
{
    /// <summary>The data type name of a field that holds a group of child fields.</summary>
    public const string FieldGroupDataType = "Field Group";

    /// <summary>Gets the field identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the position of the field within its question.</summary>
    public int FieldNumber { get; init; }

    /// <summary>Gets the field label.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Gets the field's short name, which a few validation rules single fields out by.</summary>
    public string ShortName { get; init; } = string.Empty;

    /// <summary>Gets the data type name, for example <c>Boolean</c>, <c>List</c>, <c>MultiValueList</c>, <c>Text</c>, <c>Long Text</c>, <c>Decimal</c>, <c>Date</c>, <c>Profile Version Data</c> or <c>Field Group</c>.</summary>
    public string DataTypeName { get; init; } = string.Empty;

    /// <summary>Gets a value indicating whether the field must be answered.</summary>
    public bool IsMandatory { get; init; }

    /// <summary>Gets a value indicating whether the field's answer decides whether another question is relevant.</summary>
    public bool AffectsRelevance { get; init; }

    /// <summary>Gets a value indicating whether the field is read-only for the current situation of a profile.</summary>
    public bool IsReadOnlyCurrentSituation { get; init; }

    /// <summary>Gets a value indicating whether the field is read-only for a "what-if" scenario.</summary>
    public bool IsReadOnlyScenario { get; init; }

    /// <summary>Gets the list field whose answer decides whether this field is visible, or <see cref="Guid.Empty"/> when it is always visible.</summary>
    public Guid RelevanceFieldId { get; init; }

    /// <summary>Gets the answer of <see cref="RelevanceFieldId"/> that makes this field visible.</summary>
    public Guid RelevanceValueId { get; init; }

    /// <summary>Gets the reference table backing a list field.</summary>
    public Guid ReferenceTableId { get; init; }

    /// <summary>Gets a value indicating whether the backing reference table is user-maintainable (so it has inactive values).</summary>
    public bool ReferenceTableIsMaintainable { get; init; }

    /// <summary>Gets the child fields of a field group, in field-number order.</summary>
    public IReadOnlyList<ReportField> Children { get; init; } = [];
}

/// <summary>Everything recorded for one profile version's section.</summary>
public sealed record ReportSectionAnswers
{
    /// <summary>Gets the species the profile version profiles, in the order the database returns them.</summary>
    public IReadOnlyList<ReportSpecies> ProfiledSpecies { get; init; } = [];

    /// <summary>Gets the section's review frequencies and statuses, or <see langword="null"/> when the profile version has no record of the section.</summary>
    public ReportSectionRevision? Revision { get; init; }

    /// <summary>Gets the names of each question in the section.</summary>
    public IReadOnlyList<ProfileQuestionName> QuestionNames { get; init; } = [];

    /// <summary>Gets the answer rows of the section's repeating questions.</summary>
    public IReadOnlyList<ReportQuestionRow> Rows { get; init; } = [];

    /// <summary>Gets every recorded field value in the section, including those of field-group child fields.</summary>
    public IReadOnlyList<ReportFieldValue> Values { get; init; } = [];
}

/// <summary>The review frequencies, statuses and next review dates of one section of a profile version, which the validation checks.</summary>
public sealed record ReportSectionRevision
{
    /// <summary>Gets the VA (the database's "technical") review frequency.</summary>
    public Guid? VaReviewFrequencyId { get; init; }

    /// <summary>Gets the policy review frequency.</summary>
    public Guid? PolicyReviewFrequencyId { get; init; }

    /// <summary>Gets the author review frequency.</summary>
    public Guid? AuthorReviewFrequencyId { get; init; }

    /// <summary>Gets the VA review status.</summary>
    public Guid? VaReviewStatusId { get; init; }

    /// <summary>Gets the policy review status.</summary>
    public Guid? PolicyReviewStatusId { get; init; }

    /// <summary>Gets the author review status.</summary>
    public Guid? AuthorReviewStatusId { get; init; }

    /// <summary>Gets the date of the next VA (the database's "technical") review, or <see langword="null"/> when none is set.</summary>
    public DateTime? NextVaReview { get; init; }

    /// <summary>Gets the date of the next policy review, or <see langword="null"/> when none is set.</summary>
    public DateTime? NextPolicyReview { get; init; }

    /// <summary>Gets the date of the next author review, or <see langword="null"/> when none is set.</summary>
    public DateTime? NextAuthorReview { get; init; }
}

/// <summary>What the validation needs to know about a profile version as a whole.</summary>
public sealed record ReportVersionContext
{
    /// <summary>Gets a value indicating whether the profile version belongs to a "what-if" scenario rather than the current situation.</summary>
    public bool IsWhatIfScenario { get; init; }

    /// <summary>Gets a value indicating whether any of the version's profiled species is inactive.</summary>
    public bool HasInactiveProfiledSpecies { get; init; }

    /// <summary>Gets a value indicating whether any of the version's other affected species is inactive.</summary>
    public bool HasInactiveOtherAffectedSpecies { get; init; }
}

/// <summary>A species the profile version profiles.</summary>
/// <param name="Id">The species identifier.</param>
/// <param name="Name">The species name; may contain HTML.</param>
/// <param name="IsActive">Whether the species is still active (has no end date).</param>
public sealed record ReportSpecies(Guid Id, string Name, bool IsActive = true);

/// <summary>One answer row of a repeating question.</summary>
/// <param name="Id">The row identifier.</param>
/// <param name="QuestionId">The question the row belongs to.</param>
/// <param name="SequenceNumber">The zero-based position of the row.</param>
public sealed record ReportQuestionRow(Guid Id, Guid QuestionId, int SequenceNumber);

/// <summary>One recorded answer for a field. Exactly one value property is populated, per the field's data type.</summary>
public sealed record ReportFieldValue
{
    /// <summary>Gets the question the field belongs to (for a child field, the question owning its group).</summary>
    public Guid QuestionId { get; init; }

    /// <summary>Gets the field the value answers.</summary>
    public Guid FieldId { get; init; }

    /// <summary>Gets the field group the field belongs to, or <see cref="Guid.Empty"/> for a direct field.</summary>
    public Guid FieldGroupId { get; init; }

    /// <summary>Gets the species the value is for, or <see langword="null"/> when it is not per species.</summary>
    public Guid? SpeciesId { get; init; }

    /// <summary>Gets the repeating row the value is for, or <see langword="null"/> when it is not in a row.</summary>
    public Guid? RowId { get; init; }

    /// <summary>Gets the answer for a boolean field.</summary>
    public bool? BooleanValue { get; init; }

    /// <summary>Gets the selected reference value for a list field.</summary>
    public Guid? ListValue { get; init; }

    /// <summary>Gets the answer for a decimal field.</summary>
    public decimal? DecimalValue { get; init; }

    /// <summary>Gets the answer for a date field.</summary>
    public DateTime? DateValue { get; init; }

    /// <summary>Gets the answer for a text or long-text field.</summary>
    public string? TextValue { get; init; }
}

/// <summary>One option of a reference or lookup table.</summary>
/// <param name="Id">The value identifier.</param>
/// <param name="Text">The display text.</param>
/// <param name="IsActive">Whether the value can still be chosen.</param>
public sealed record ReportReferenceItem(Guid Id, string Text, bool IsActive);
