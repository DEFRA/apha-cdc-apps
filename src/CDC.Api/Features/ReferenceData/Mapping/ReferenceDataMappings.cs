using CDC.Api.Domain.Entities;
using CDC.Api.Features.ReferenceData.Dtos;

namespace CDC.Api.Features.ReferenceData.Mapping;

/// <summary>Projects reference data entities onto the DTOs returned by the API.</summary>
public static class ReferenceDataMappings
{
    /// <summary>Projects a reference value entity.</summary>
    /// <param name="referenceValue">The entity to project.</param>
    /// <returns>The equivalent DTO.</returns>
    public static ReferenceValueDto ToDto(this ReferenceValue referenceValue) => new()
    {
        Id = referenceValue.Id,
        Value = referenceValue.Value
    };
}
