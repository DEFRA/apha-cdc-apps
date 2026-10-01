using CDC.Api.Domain.Entities;
using CDC.Api.Features.ProfileQuestions.Dtos;

namespace CDC.Api.Tests.ProfileQuestions;

/// <summary>
/// Builders for profile question domain objects and DTOs. The entities use <c>required</c>
/// members, which AutoFixture cannot populate, so they are constructed explicitly here.
/// </summary>
internal static class ProfileQuestionTestData
{
    public static readonly Guid QuestionId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid ProfileSectionId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    public static byte[] RowVersion => [0, 0, 0, 0, 0, 0, 7, 209];

    public static byte[] NewRowVersion => [0, 0, 0, 0, 0, 0, 7, 210];

    public static ProfileQuestion ProfileQuestion() => new()
    {
        Id = QuestionId,
        Name = "Is it endemic?",
        ShortName = "Endemic",
        NonTechnicalName = "Is the disease already present?",
        QuestionNumber = 1,
        UserGuidance = "Consider current surveillance data.",
        LastUpdated = RowVersion
    };

    public static ProfileQuestionDto ProfileQuestionDto() => new()
    {
        Id = QuestionId,
        Name = "Is it endemic?",
        ShortName = "Endemic",
        NonTechnicalName = "Is the disease already present?",
        QuestionNumber = 1,
        UserGuidance = "Consider current surveillance data.",
        LastUpdated = RowVersion
    };
}
