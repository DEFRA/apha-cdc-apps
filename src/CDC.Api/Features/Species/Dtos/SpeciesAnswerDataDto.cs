using CDC.Common.Contracts;

namespace CDC.Api.Features.Species.Dtos;

/// <summary>
/// All recorded answers for a single species.
/// </summary>
public sealed record SpeciesAnswerDataDto : SpeciesAnswerDataContract<SpeciesSectionDto, SpeciesFieldValueDto>; // NOSONAR

/// <summary>
/// The answered field values for one questionnaire section.
/// </summary>
public sealed record SpeciesSectionDto : SpeciesSectionContract<SpeciesFieldValueDto>; // NOSONAR

/// <summary>
/// One stored answer. Exactly one value property is populated, per the field's data type.
/// </summary>
public sealed record SpeciesFieldValueDto : SpeciesFieldValueContract; // NOSONAR
