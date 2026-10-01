namespace CDC.Common.Contracts;

/// <summary>
/// All recorded answers for a single species. Shared by CDC.Api's response DTO and CDC.Web's
/// view model so the wire shape can never drift between the two. Generic over the concrete
/// section/field-value types so each project's nested collections are typed as its own concrete
/// records rather than this base contract.
/// </summary>
public abstract record SpeciesAnswerDataContract<TSection, TFieldValue>
    where TSection : SpeciesSectionContract<TFieldValue>
    where TFieldValue : SpeciesFieldValueContract
{
    /// <summary>Gets the species the answers belong to.</summary>
    public Guid SpeciesId { get; init; }

    /// <summary>Gets the species display name.</summary>
    public string SpeciesName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the SQL Server <c>timestamp</c>/<c>rowversion</c> of the species row. It must be
    /// echoed back on update, which fails if another user has saved in the meantime.
    /// </summary>
    public byte[] LastUpdated { get; init; } = [];

    /// <summary>Gets the answers grouped by questionnaire section.</summary>
    public IReadOnlyList<TSection> Sections { get; init; } = [];
}

/// <summary>
/// The answered field values for one questionnaire section. Generic over the concrete
/// field-value type so each project's nested collection is typed as its own concrete record
/// rather than this base contract.
/// </summary>
public abstract record SpeciesSectionContract<TFieldValue>
    where TFieldValue : SpeciesFieldValueContract
{
    /// <summary>Gets the identifier of the section these values belong to.</summary>
    public Guid SectionId { get; init; }

    /// <summary>Gets the recorded field values.</summary>
    public IReadOnlyList<TFieldValue> FieldValues { get; init; } = [];
}

/// <summary>One stored answer. Exactly one value property is populated, per the field's data type.</summary>
public abstract record SpeciesFieldValueContract
{
    /// <summary>Gets the field value identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the identifier of the question the field belongs to.</summary>
    public Guid QuestionId { get; init; }

    /// <summary>Gets the position of the field within its question.</summary>
    public int FieldNumber { get; init; }

    /// <summary>Gets the answer for a boolean field.</summary>
    public bool? BooleanValue { get; init; }

    /// <summary>Gets the selected reference data item for a list field.</summary>
    public Guid? ListValue { get; init; }

    /// <summary>Gets the answer for a text or long-text field.</summary>
    public string? TextValue { get; init; }
}

