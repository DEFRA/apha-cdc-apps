using CDC.Common.Contracts;

namespace CDC.Api.Features.Species.Dtos;

/// <summary>
/// The species questionnaire structure.
/// </summary>
public sealed record SpeciesMetadataDto : SpeciesMetadataContract<SpeciesSectionMetadataDto>; // NOSONAR

/// <summary>
/// A section of the species questionnaire.
/// </summary>
public sealed record SpeciesSectionMetadataDto : SpeciesSectionMetadataContract<SpeciesQuestionMetadataDto, SpeciesFieldMetadataDto>; // NOSONAR

/// <summary>
/// A question within a questionnaire section.
/// </summary>
public sealed record SpeciesQuestionMetadataDto : SpeciesQuestionMetadataContract<SpeciesFieldMetadataDto>; // NOSONAR

/// <summary>
/// A single answerable field within a question.
/// </summary>
public sealed record SpeciesFieldMetadataDto : SpeciesFieldMetadataContract; // NOSONAR

