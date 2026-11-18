using System.Net;
using System.Text.RegularExpressions;
using CDC.Common.Contracts;
using CDC.Web.Infrastructure;
using CDC.Web.Models;
using CDC.Web.Tests.Pages.SurveillanceProfiles;
using CDC.Web.Tests.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CDC.Web.Tests.Integration;

// Renders the controls of the ordinary questions of the Questions tab through the real Razor pipeline. As the legacy page did, a field that
// answers yes or no, and one that picks from a list, is a drop-down headed "Please select...", rather than a group of radio buttons.
public partial class EditProfileQuestionsFieldControlsIntegrationTests : IClassFixture<CdcWebTestFactory>
{
    private static readonly string PagePath = $"/SurveillanceProfiles/EditProfileQuestions/{ProfileTabTestData.ProfileId}";

    private static readonly Guid SectionId = Guid.NewGuid();
    private static readonly Guid QuestionId = Guid.NewGuid();
    private static readonly Guid RiskFieldId = Guid.NewGuid();
    private static readonly Guid NotifiableFieldId = Guid.NewGuid();
    private static readonly Guid SeverityFieldId = Guid.NewGuid();
    private static readonly Guid UnnamedListFieldId = Guid.NewGuid();
    private static readonly Guid RoutesFieldId = Guid.NewGuid();
    private static readonly Guid UnnamedRoutesFieldId = Guid.NewGuid();
    private static readonly Guid SeverityTableId = Guid.NewGuid();
    private static readonly Guid RoutesTableId = Guid.NewGuid();
    private static readonly Guid LowId = Guid.NewGuid();
    private static readonly Guid HighId = Guid.NewGuid();
    private static readonly Guid FoodId = Guid.NewGuid();
    private static readonly Guid ContactId = Guid.NewGuid();

    private readonly CdcWebTestFactory _factory;

    public EditProfileQuestionsFieldControlsIntegrationTests(CdcWebTestFactory factory)
    {
        _factory = factory;
    }

    [GeneratedRegex("type=\"radio\"|govuk-radios")]
    private static partial Regex RadioRegex();

    // One question of section 6 whose fields answer yes or no, pick from a list and pick any of a list, some of them with no name of their own.
    private static ProfileQuestionnaireMetadataDto Metadata() => new()
    {
        Sections =
        [
            new ProfileSectionMetadataDto
            {
                Id = SectionId,
                Name = "Human health implications",
                ShortName = "Human health",
                SectionNumber = 6,
                Questions =
                [
                    new ProfileQuestionMetadataDto
                    {
                        Id = QuestionId,
                        SectionId = SectionId,
                        ShortName = "Human health",
                        QuestionNumber = 1,
                        Fields =
                        [
                            new ProfileFieldMetadataDto { Id = RiskFieldId, QuestionId = QuestionId, Name = "Is there a risk to human health?", FieldNumber = 1, DataTypeName = "Boolean", IsMandatory = true },
                            new ProfileFieldMetadataDto { Id = NotifiableFieldId, QuestionId = QuestionId, Name = "Is it notifiable?", FieldNumber = 2, DataTypeName = "Boolean" },
                            new ProfileFieldMetadataDto { Id = SeverityFieldId, QuestionId = QuestionId, Name = "Severity", FieldNumber = 3, DataTypeName = "List", ReferenceTableId = SeverityTableId, IsMandatory = true },
                            new ProfileFieldMetadataDto { Id = UnnamedListFieldId, QuestionId = QuestionId, Name = string.Empty, FieldNumber = 4, DataTypeName = "List", ReferenceTableId = SeverityTableId },
                            new ProfileFieldMetadataDto { Id = RoutesFieldId, QuestionId = QuestionId, Name = "Routes of infection", FieldNumber = 5, DataTypeName = "MultiValueList", ReferenceTableId = RoutesTableId },
                            new ProfileFieldMetadataDto { Id = UnnamedRoutesFieldId, QuestionId = QuestionId, Name = string.Empty, FieldNumber = 6, DataTypeName = "MultiValueList", ReferenceTableId = RoutesTableId }
                        ]
                    }
                ]
            }
        ]
    };

    private static Dictionary<Guid, IReadOnlyList<ReferenceValueDto>> ReferenceValues() => new()
    {
        [SeverityTableId] = [new ReferenceValueDto { Id = LowId, Value = "Low" }, new ReferenceValueDto { Id = HighId, Value = "High" }],
        [RoutesTableId] = [new ReferenceValueDto { Id = FoodId, Value = "Food" }, new ReferenceValueDto { Id = ContactId, Value = "Contact" }]
    };

    private static ProfileFieldValueDto Value(int fieldNumber, bool? boolean = null, Guid? list = null) => new()
    {
        Id = Guid.NewGuid(),
        QuestionId = QuestionId,
        FieldNumber = fieldNumber,
        BooleanValue = boolean,
        ListValue = list
    };

    // Risk answered yes, severity high, and routes of infection by food.
    private static ProfileSectionAnswersDto Answers() => new()
    {
        FieldValues = [Value(1, boolean: true), Value(3, list: HighId), Value(5, list: FoodId)]
    };

    private async Task<string> GetPageAsync(ManageProfileViewModel profile)
    {
        var sections = new FakeProfileSectionsApiService(metadata: Metadata(), answers: Answers(), referenceValuesByTable: ReferenceValues());

        using var factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IApiClient>();
                services.AddSingleton<IApiClient>(new CDC.Web.Tests.Features.Landing.FakeApiClient(manageProfile: profile));
                services.RemoveAll<IProfileSectionsApiService>();
                services.AddSingleton<IProfileSectionsApiService>(sections);
                services.RemoveAll<IProfileQuestionsApiService>();
                services.AddSingleton<IProfileQuestionsApiService>(new FakeProfileQuestionsApiService());
            }));
        var client = factory.CreateClient();
        var signIn = await client.GetAsync(CdcWebTestFactory.TestSignInPath);
        Assert.Equal(HttpStatusCode.NoContent, signIn.StatusCode);

        return await client.GetStringAsync(PagePath);
    }

    // The group of a field of the page: from the container the control sits in, up to the next field's.
    private static string FieldMarkup(string body, Guid fieldId)
    {
        var anchor = body.IndexOf($"field_{fieldId}", StringComparison.Ordinal);
        Assert.True(anchor >= 0, "The field should be on the page.");

        var start = body.LastIndexOf("<div class=\"govuk-form-group\">", anchor, StringComparison.Ordinal);
        var end = body.IndexOf("<div class=\"govuk-form-group\">", anchor, StringComparison.Ordinal);

        return body[start..(end < 0 ? body.Length : end)];
    }

    [Fact]
    public async Task Boolean_IsADropDown_OfPleaseSelectYesAndNo_WithTheRecordedAnswerSelected()
    {
        var body = await GetPageAsync(ProfileTabTestData.DraftProfile());

        var field = FieldMarkup(body, RiskFieldId);

        Assert.Contains($"<label class=\"govuk-label govuk-label--s\" for=\"field_{RiskFieldId}\">Is there a risk to human health?</label>", field);
        Assert.Matches($"<select class=\"govuk-select\" id=\"field_{RiskFieldId}\" name=\"field_{RiskFieldId}\"\\s*>", field);
        Assert.Contains("<option value=\"\">Please select...</option>", field);
        Assert.Contains("<option value=\"true\" selected=\"selected\">Yes</option>", field);
        Assert.Contains("<option value=\"false\">No</option>", field);
    }

    [Fact]
    public async Task Boolean_WithNoRecordedAnswer_HasNothingSelected()
    {
        var body = await GetPageAsync(ProfileTabTestData.DraftProfile());

        var field = FieldMarkup(body, NotifiableFieldId);

        Assert.DoesNotContain("selected=", field);
    }

    // The legacy page did not mark a field that may be left unanswered as optional, so no control says so.
    [Fact]
    public async Task Fields_AreNotMarkedAsOptional()
    {
        var body = await GetPageAsync(ProfileTabTestData.DraftProfile());

        foreach (var fieldId in new[] { RiskFieldId, NotifiableFieldId, SeverityFieldId, UnnamedListFieldId })
        {
            var field = FieldMarkup(body, fieldId);

            Assert.DoesNotContain("(optional)", field);
            Assert.DoesNotContain("aria-describedby", field);
        }
    }

    [Fact]
    public async Task List_IsADropDown_OfItsReferenceValues_WithTheRecordedValueSelected()
    {
        var body = await GetPageAsync(ProfileTabTestData.DraftProfile());

        var field = FieldMarkup(body, SeverityFieldId);

        Assert.Contains($"<label class=\"govuk-label govuk-label--s\" for=\"field_{SeverityFieldId}\">Severity</label>", field);
        Assert.Matches($"<select class=\"govuk-select\" id=\"field_{SeverityFieldId}\" name=\"field_{SeverityFieldId}\"\\s*>", field);
        Assert.Contains("<option value=\"\">Please select...</option>", field);
        Assert.Contains($"<option value=\"{LowId}\">Low</option>", field);
        Assert.Contains($"<option value=\"{HighId}\" selected=\"selected\">High</option>", field);
    }

    [Fact]
    public async Task Fields_OfTheQuestion_ArePickedFromWithoutRadioButtons()
    {
        var body = await GetPageAsync(ProfileTabTestData.DraftProfile());

        Assert.False(RadioRegex().IsMatch(body), "A yes or no answer, and a choice from a list, should be a drop-down, not radio buttons.");
    }

    [Fact]
    public async Task AFieldWithNoName_HasAVisuallyHiddenLabelThatNamesTheQuestion()
    {
        var body = await GetPageAsync(ProfileTabTestData.DraftProfile());

        var field = FieldMarkup(body, UnnamedListFieldId);

        Assert.Contains($"<label class=\"govuk-label govuk-visually-hidden\" for=\"field_{UnnamedListFieldId}\">Answer to question 6.1</label>", field);
    }

    [Fact]
    public async Task MultiValueList_IsAGroupOfCheckboxes_UnderALegendSayingToSelectAllThatApply()
    {
        var body = await GetPageAsync(ProfileTabTestData.DraftProfile());

        var field = FieldMarkup(body, RoutesFieldId);

        Assert.Contains("Routes of infection (please select all that apply)", field);
        Assert.Contains($"id=\"field_{RoutesFieldId}-0\" name=\"field_{RoutesFieldId}\" type=\"checkbox\" value=\"{FoodId}\" checked=\"checked\"", field);
        Assert.Contains($"id=\"field_{RoutesFieldId}-1\" name=\"field_{RoutesFieldId}\" type=\"checkbox\" value=\"{ContactId}\"", field);
        Assert.DoesNotContain($"value=\"{ContactId}\" checked", field);
        Assert.DoesNotContain("<select", field);
    }

    [Fact]
    public async Task MultiValueList_WithNoName_SaysToSelectAllThatApply_AndNamesItsGroupForAScreenReader()
    {
        var body = await GetPageAsync(ProfileTabTestData.DraftProfile());

        var field = FieldMarkup(body, UnnamedRoutesFieldId);

        Assert.Matches("<legend class=\"govuk-fieldset__legend govuk-fieldset__legend--s\">\\s*<span class=\"govuk-visually-hidden\">Answer to question 6.1:</span>\\s*Please select all that apply\\s*</legend>", field);
        Assert.DoesNotContain("(please select all that apply)", field);
    }

    [Fact]
    public async Task ReadOnlyBoolean_IsAYesAndANoCheckbox_NeitherOfWhichCanBeChanged()
    {
        var body = await GetPageAsync(ProfileTabTestData.ReadOnlyDraftProfile());

        var field = FieldMarkup(body, RiskFieldId);

        Assert.Contains($"id=\"field_{RiskFieldId}-readonly-yes\" type=\"checkbox\" disabled checked=\"checked\"", field);
        Assert.Contains($"id=\"field_{RiskFieldId}-readonly-no\" type=\"checkbox\" disabled", field);
        Assert.DoesNotContain($"id=\"field_{RiskFieldId}-readonly-no\" type=\"checkbox\" disabled checked", field);
        Assert.DoesNotContain("<select", field);
        Assert.DoesNotContain("name=\"field_", field);
    }
}
