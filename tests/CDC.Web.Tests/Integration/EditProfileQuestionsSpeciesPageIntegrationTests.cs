using System.Net;
using System.Text.RegularExpressions;
using CDC.Common.Contracts;
using CDC.Web.Infrastructure;
using CDC.Web.Models;
using CDC.Web.Pages.SurveillanceProfiles;
using CDC.Web.Tests.Pages.SurveillanceProfiles;
using CDC.Web.Tests.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static CDC.Web.Tests.Pages.SurveillanceProfiles.EditProfileQuestionsSpeciesTests;
using static CDC.Web.Tests.Pages.SurveillanceProfiles.ProfileTabTestData;

namespace CDC.Web.Tests.Integration;

// Renders a per-species question of the Questions tab through the real Razor pipeline: the table of the species, the panel that edits one
// species' answers, and the confirmation of a change to an answer that decides whether other questions apply.
public partial class EditProfileQuestionsSpeciesPageIntegrationTests : IClassFixture<CdcWebTestFactory>
{
    private static readonly string PagePath = $"/SurveillanceProfiles/EditProfileQuestions/{ProfileId}";

    private readonly CdcWebTestFactory _factory;

    public EditProfileQuestionsSpeciesPageIntegrationTests(CdcWebTestFactory factory)
    {
        _factory = factory;
    }

    [GeneratedRegex("name=\"__RequestVerificationToken\"[^>]*?value=\"([^\"]+)\"")]
    private static partial Regex AntiforgeryTokenRegex();

    private static FakeProfileSectionsApiService CreateApi(
        ProfileQuestionnaireMetadataDto? metadata = null,
        ProfileSectionAnswersDto? answers = null,
        ProfileVersionValidationStatusDto? validationStatus = null) => new(
            metadata: metadata ?? Metadata(),
            answers: answers ?? Answers(
                Value(WelfareQuestionId, 1, CattleId, flag: true),
                Value(WelfareQuestionId, 2, CattleId, text: "<p>Cattle suffer</p>"),
                Value(WelfareQuestionId, 1, BadgerId, flag: false),
                Value(ImpactQuestionId, 1, CattleId, list: SevereId)),
            referenceValuesByTable: ReferenceValues(),
            validationStatus: validationStatus);

    private async Task WithPageAsync(ManageProfileViewModel profile, FakeProfileSectionsApiService sections, Func<HttpClient, Task> test)
    {
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

        await test(client);
    }

    private async Task<string> GetPageAsync(ManageProfileViewModel profile, FakeProfileSectionsApiService? sections = null)
    {
        var body = string.Empty;

        await WithPageAsync(profile, sections ?? CreateApi(), async client => body = await client.GetStringAsync(PagePath));

        return body;
    }

    private async Task<string> PostAsync(FakeProfileSectionsApiService sections, string handler, params (string Key, string Value)[] fields)
    {
        var body = string.Empty;

        await WithPageAsync(DraftProfile(), sections, async client =>
        {
            var page = await client.GetStringAsync(PagePath);
            var token = AntiforgeryTokenRegex().Match(page).Groups[1].Value;
            List<KeyValuePair<string, string>> form =
            [
                new("__RequestVerificationToken", token),
                new("profileVersionId", ProfileVersionId.ToString()),
                new("Section", SectionId.ToString()),
                new("lastUpdated", string.Empty),
                .. fields.Select(field => new KeyValuePair<string, string>(field.Key, field.Value))
            ];

            using var response = await client.PostAsync($"{PagePath}?handler={handler}", new FormUrlEncodedContent(form));
            body = await response.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        });

        return body;
    }

    // The part of the page of one question, from its table to the end of its content.
    private static string QuestionMarkup(string body, Guid questionId)
    {
        var start = body.IndexOf($"id=\"species-{questionId}\"", StringComparison.Ordinal);
        Assert.True(start >= 0, "The question should have a species table.");

        var end = body.IndexOf("govuk-accordion__section\"", start, StringComparison.Ordinal);

        return body[start..(end < 0 ? body.Length : end)];
    }

    // The Summary section's species fields hold no answer: the page shows the version's own species in a bordered box, as the legacy page did.
    [Fact]
    public async Task VersionDataField_ShowsTheVersionsSpeciesInAReadOnlyBox()
    {
        var questionId = Guid.NewGuid();
        var metadata = new ProfileQuestionnaireMetadataDto
        {
            Sections =
            [
                new ProfileSectionMetadataDto
                {
                    Id = SectionId,
                    Name = "Summary",
                    ShortName = "Summary",
                    SectionNumber = 1,
                    Questions =
                    [
                        new ProfileQuestionMetadataDto
                        {
                            Id = questionId,
                            SectionId = SectionId,
                            ShortName = "Profiled Species",
                            QuestionNumber = 3,
                            Fields =
                            [
                                new ProfileFieldMetadataDto
                                {
                                    Id = Guid.NewGuid(),
                                    QuestionId = questionId,
                                    ShortName = "Profiled Species",
                                    FieldNumber = 1,
                                    DataTypeName = "Profile Version Data",
                                    IsReadOnlyCurrentSituation = true,
                                    IsReadOnlyScenario = true
                                }
                            ]
                        }
                    ]
                }
            ]
        };
        var answers = new ProfileSectionAnswersDto
        {
            ProfileVersionId = ProfileVersionId,
            ProfileSectionId = SectionId,
            ProfiledSpeciesText = "Chickens, <em>Anas platyrhynchos</em> (inactive)"
        };

        var body = await GetPageAsync(DraftProfile(), CreateApi(metadata, answers));

        Assert.Contains("<p class=\"govuk-body app-version-data-box\">", body);
        Assert.Contains("<span class=\"govuk-visually-hidden\">Answer to question 1.3: </span>", body);
        Assert.Contains("Chickens, <em>Anas platyrhynchos</em> (inactive)", body);
        Assert.DoesNotContain("Not answered", body);
    }

    [Fact]
    public async Task Table_HasAColumnForTheSpecies_AndForEachFieldTheSummaryIncludes()
    {
        var body = await GetPageAsync(DraftProfile());

        var welfare = QuestionMarkup(body, WelfareQuestionId);
        var header = welfare[..welfare.IndexOf("</thead>", StringComparison.Ordinal)];

        Assert.Contains("<th scope=\"col\" class=\"govuk-table__header\">Species / species group</th>", header);
        Assert.Contains("<th scope=\"col\" class=\"govuk-table__header\">Compromised welfare?</th>", header);
        Assert.Contains("<th scope=\"col\" class=\"govuk-table__header\">Supporting comments</th>", header);
        Assert.Contains("<span class=\"govuk-visually-hidden\">Action</span>", header);

        // The impact question lists only its first field, so its other fields have no column.
        var impact = QuestionMarkup(body, ImpactQuestionId);
        var impactHeader = impact[..impact.IndexOf("</thead>", StringComparison.Ordinal)];

        Assert.Contains(">Impact</th>", impactHeader);
        Assert.DoesNotContain(">Areas</th>", impactHeader);
        Assert.DoesNotContain(">Rate</th>", impactHeader);
    }

    [Fact]
    public async Task Table_HasARowForEachSpecies_ShowingItsAnswers()
    {
        var body = await GetPageAsync(DraftProfile());

        var welfare = QuestionMarkup(body, WelfareQuestionId);

        Assert.Contains($"<tr class=\"govuk-table__row\" data-species-row=\"{CattleId}\" data-species-name=\"Cattle\">", welfare);
        Assert.Contains("<th scope=\"row\" class=\"govuk-table__header\">Cattle</th>", welfare);
        Assert.Contains($"<td class=\"govuk-table__cell\" data-species-summary=\"{WelfareFieldId}\">Yes</td>", welfare);
        Assert.Contains($"<td class=\"govuk-table__cell\" data-species-summary=\"{CommentsFieldId}\">Cattle suffer</td>", welfare);

        // A species name may carry italics, which the table shows and the plain-text name that titles the panel does not.
        Assert.Contains("<th scope=\"row\" class=\"govuk-table__header\"><em>Meles meles</em> (badger)</th>", welfare);
        Assert.Contains("data-species-name=\"Meles meles (badger)\"", welfare);
        Assert.Contains($"<td class=\"govuk-table__cell\" data-species-summary=\"{WelfareFieldId}\">No</td>", welfare);
    }

    [Fact]
    public async Task Table_OffersAnEditLinkForEachSpecies_ThatNamesTheSpeciesAndTheQuestion()
    {
        var body = await GetPageAsync(DraftProfile());

        Assert.Contains(
            $"<button type=\"button\" class=\"govuk-link app-link-button\" aria-controls=\"species-{WelfareQuestionId}-panel\" aria-expanded=\"false\" data-species-edit=\"{CattleId}\">Edit<span class=\"govuk-visually-hidden\"> Cattle answer to question 5.1</span></button>",
            QuestionMarkup(body, WelfareQuestionId));
    }

    [Fact]
    public async Task Table_OffersAViewLinkInstead_ToAUserWhoCannotEditTheSection()
    {
        var body = await GetPageAsync(ReadOnlyDraftProfile());

        var welfare = QuestionMarkup(body, WelfareQuestionId);

        Assert.Contains($"data-species-edit=\"{CattleId}\">View<span class=\"govuk-visually-hidden\">", welfare);
        Assert.DoesNotContain(">Edit<span", welfare);
        Assert.DoesNotContain("data-species-picker", welfare);
        Assert.DoesNotContain("data-species-cancel", welfare);
        Assert.Contains("data-species-can-edit=\"false\"", welfare);
        Assert.Contains("<button type=\"button\" class=\"govuk-button govuk-button--secondary\" data-module=\"govuk-button\" data-species-ok>Close</button>", welfare);
        Assert.Matches("<div class=\"govuk-button-group app-button-group--right\">\\s*<button type=\"button\" class=\"govuk-button govuk-button--secondary\" data-module=\"govuk-button\" data-species-ok>Close</button>", welfare);
    }

    [Fact]
    public async Task Table_KeepsEachSpeciesAnswersInHiddenInputs_NamedForTheSpeciesAndTheField()
    {
        var body = await GetPageAsync(DraftProfile());

        var welfare = QuestionMarkup(body, WelfareQuestionId);

        Assert.Contains($"<input type=\"hidden\" name=\"species_{CattleId}_field_{WelfareFieldId}\" value=\"true\" data-species-value=\"{WelfareFieldId}\" />", welfare);
        Assert.Contains($"<input type=\"hidden\" name=\"species_{BadgerId}_field_{WelfareFieldId}\" value=\"false\" data-species-value=\"{WelfareFieldId}\" />", welfare);
        Assert.Contains($"name=\"species_{CattleId}_field_{CommentsFieldId}\" value=\"&lt;p&gt;Cattle suffer&lt;/p&gt;\"", welfare);

        var impact = QuestionMarkup(body, ImpactQuestionId);

        Assert.Contains($"name=\"species_{CattleId}_field_{ImpactFieldId}\" value=\"{SevereId}\"", impact);
    }

    [Fact]
    public async Task Table_PostsNoInputForAFieldThatCannotBeChanged_ButKeepsItsValueForThePanelToShow()
    {
        var body = await GetPageAsync(DraftProfile());

        var impact = QuestionMarkup(body, ImpactQuestionId);

        // A date cannot be edited in the panel, so it is shown and never posted.
        Assert.DoesNotContain($"species_{CattleId}_field_{FirstContactFieldId}", impact);
        Assert.Contains($"<input type=\"hidden\" value=\"\" data-species-value=\"{FirstContactFieldId}\" />", impact);
    }

    [Fact]
    public async Task Table_ListsTheFieldsAnswersOfAReadOnlyUser_WithoutPostingThem()
    {
        var body = await GetPageAsync(ReadOnlyDraftProfile());

        var welfare = QuestionMarkup(body, WelfareQuestionId);

        Assert.DoesNotContain($"species_{CattleId}_field_{WelfareFieldId}", welfare);
        Assert.Contains($"<input type=\"hidden\" value=\"true\" data-species-value=\"{WelfareFieldId}\" />", welfare);
    }

    [Fact]
    public async Task Table_LeavesOutTheSpeciesAQuestionIsIrrelevantFor_ButKeepsItsAnswersOnThePage()
    {
        var status = Status(new ProfileQuestionSpeciesDto { QuestionId = ImpactQuestionId, SpeciesId = BadgerId });
        var body = await GetPageAsync(DraftProfile(), CreateApi(validationStatus: status));

        var impact = QuestionMarkup(body, ImpactQuestionId);

        Assert.Contains($"data-species-row=\"{BadgerId}\" data-species-name=\"Meles meles (badger)\" hidden=\"hidden\">", impact);
        Assert.Contains($"name=\"species_{BadgerId}_field_{ImpactFieldId}\"", impact);
        Assert.DoesNotContain($"data-species-row=\"{CattleId}\" data-species-name=\"Cattle\" hidden", impact);
        Assert.DoesNotContain($"data-species-pick-item=\"{BadgerId}\"", impact);
        Assert.Contains($"data-species-pick-item=\"{CattleId}\"", impact);

        // The welfare question applies to both species.
        Assert.Contains($"data-species-pick-item=\"{BadgerId}\"", QuestionMarkup(body, WelfareQuestionId));
    }

    [Fact]
    public async Task Panel_EditsTheAnswersOfOneSpecies_WithAControlForEachField()
    {
        var body = await GetPageAsync(DraftProfile());

        var welfare = QuestionMarkup(body, WelfareQuestionId);
        var panelId = $"species-{WelfareQuestionId}-field-{WelfareFieldId}";

        Assert.Contains($"<fieldset class=\"govuk-fieldset app-answer-panel\" id=\"species-{WelfareQuestionId}-panel\" data-species-panel hidden tabindex=\"-1\">", welfare);
        Assert.Contains("<h3 class=\"govuk-fieldset__heading\" data-species-panel-heading>Edit answer</h3>", welfare);
        Assert.Contains($"<label class=\"govuk-label govuk-label--s\" for=\"{panelId}\">Compromised welfare?</label>", welfare);
        Assert.Contains("<option value=\"\">Please select...</option>", welfare);
        Assert.Contains("<option value=\"true\">Yes</option>", welfare);
        Assert.Contains("<option value=\"false\">No</option>", welfare);
        Assert.Contains($"<textarea class=\"govuk-textarea app-richtext-editor\" id=\"species-{WelfareQuestionId}-field-{CommentsFieldId}\"", welfare);
        Assert.Contains("<button type=\"button\" class=\"govuk-button\" data-module=\"govuk-button\" data-species-ok>OK</button>", welfare);
        Assert.Contains("<button type=\"button\" class=\"govuk-button govuk-button--secondary\" data-module=\"govuk-button\" data-species-cancel>Cancel</button>", welfare);
    }

    [Fact]
    public async Task Panel_NamesNoControl_SoThatOnlyTheHiddenInputsArePosted()
    {
        var body = await GetPageAsync(DraftProfile());

        var panelStart = QuestionMarkup(body, WelfareQuestionId);
        var panel = panelStart[panelStart.IndexOf("data-species-panel", StringComparison.Ordinal)..];

        Assert.DoesNotMatch("<(select|input|textarea)[^>]* name=", panel);
    }

    [Fact]
    public async Task Panel_OffersTheItemsOfAListAndTheCheckboxesOfAMultiValueList_AndADisabledControlForAFieldThatCannotBeChanged()
    {
        var body = await GetPageAsync(DraftProfile());

        var impact = QuestionMarkup(body, ImpactQuestionId);

        Assert.Contains($"<option value=\"{MildId}\">Mild</option>", impact);
        Assert.Contains($"<option value=\"{SevereId}\">Severe</option>", impact);
        Assert.Contains($"id=\"species-{ImpactQuestionId}-field-{AreasFieldId}-0\" type=\"checkbox\" value=\"{NorthId}\"", impact);
        Assert.Contains($"id=\"species-{ImpactQuestionId}-field-{RateFieldId}\" type=\"text\" inputmode=\"decimal\"", impact);
        Assert.Matches($"id=\"species-{ImpactQuestionId}-field-{FirstContactFieldId}\" type=\"text\"[^>]* disabled=\"disabled\"", impact);
    }

    [Fact]
    public async Task Panel_ShowsAReadOnlyUsersLongTextAnswerAsFormattedText_AndTheirBooleanAsBoxesThatCannotBeChanged()
    {
        var body = await GetPageAsync(ReadOnlyDraftProfile());

        var welfare = QuestionMarkup(body, WelfareQuestionId);
        var controlId = $"species-{WelfareQuestionId}-field-{WelfareFieldId}";

        Assert.Contains($"id=\"species-{WelfareQuestionId}-field-{CommentsFieldId}\" aria-labelledby=\"species-{WelfareQuestionId}-field-{CommentsFieldId}-label\" data-answer-text-view", welfare);
        Assert.DoesNotContain("<textarea", welfare);
        Assert.DoesNotContain("data-richtext-command", welfare);

        // As the legacy View link showed a Boolean the user may not change: a Yes and a No box, not a disabled drop-down.
        Assert.DoesNotContain("<select", welfare);
        Assert.Contains($"id=\"{controlId}-yes\" type=\"checkbox\" value=\"true\" disabled data-answer-choice />", welfare);
        Assert.Contains($"id=\"{controlId}-no\" type=\"checkbox\" value=\"false\" disabled data-answer-choice />", welfare);
        Assert.Contains("<legend class=\"govuk-fieldset__legend govuk-fieldset__legend--s\">Compromised welfare?</legend>", welfare);
    }

    // As the legacy View link showed a list the user may not change: a box for each choice, not a disabled drop-down.
    [Fact]
    public async Task Panel_ShowsAListTheUserMayNotChangeAsBoxes_NotAsADropDown()
    {
        var body = await GetPageAsync(ReadOnlyDraftProfile());

        var impact = QuestionMarkup(body, ImpactQuestionId);
        var controlId = $"species-{ImpactQuestionId}-field-{ImpactFieldId}";

        Assert.Contains($"type=\"checkbox\" value=\"{MildId}\" disabled data-answer-choice />", impact);
        Assert.Contains($"type=\"checkbox\" value=\"{SevereId}\" disabled data-answer-choice />", impact);
        Assert.Matches($"<label class=\"govuk-label govuk-checkboxes__label\" for=\"{controlId}-\\d+\">Mild</label>", impact);
        Assert.DoesNotContain($"id=\"{controlId}\"", impact);
        Assert.DoesNotContain($"<option value=\"{MildId}\">", impact);
    }

    // A field with no label of its own is named for the question it answers, which only a screen reader reads.
    [Fact]
    public async Task Panel_NamesAnUnlabelledListTheUserMayNotChangeForTheQuestion_ForAScreenReader()
    {
        var questionId = Guid.NewGuid();
        var fieldId = Guid.NewGuid();
        var tableId = Guid.NewGuid();
        var optionId = Guid.NewGuid();
        var metadata = new ProfileQuestionnaireMetadataDto
        {
            Sections =
            [
                new ProfileSectionMetadataDto
                {
                    Id = SectionId,
                    Name = "International trade",
                    ShortName = "Trade",
                    SectionNumber = 13,
                    Questions =
                    [
                        new ProfileQuestionMetadataDto
                        {
                            Id = questionId,
                            SectionId = SectionId,
                            ShortName = "Level",
                            QuestionNumber = 6,
                            IsPerSpecies = true,
                            Fields =
                            [
                                new ProfileFieldMetadataDto
                                {
                                    Id = fieldId,
                                    QuestionId = questionId,
                                    FieldNumber = 1,
                                    DataTypeName = "List",
                                    ReferenceTableId = tableId,
                                    IncludeInSummary = true
                                }
                            ]
                        }
                    ]
                }
            ]
        };
        var answers = new ProfileSectionAnswersDto
        {
            ProfileVersionId = ProfileVersionId,
            ProfileSectionId = SectionId,
            ProfiledSpecies = [new ProfileSpeciesDto { Id = CattleId, Name = "Cattle" }]
        };
        var sections = new FakeProfileSectionsApiService(
            metadata: metadata,
            answers: answers,
            referenceValuesByTable: new Dictionary<Guid, IReadOnlyList<ReferenceValueDto>>
            {
                [tableId] = [new ReferenceValueDto { Id = optionId, Value = "Some" }]
            });

        var body = await GetPageAsync(ReadOnlyDraftProfile(), sections);

        var panel = QuestionMarkup(body, questionId);

        Assert.Contains("<legend class=\"govuk-fieldset__legend govuk-visually-hidden\">Answer to question 13.6</legend>", panel);
        Assert.Contains($"type=\"checkbox\" value=\"{optionId}\" disabled data-answer-choice />", panel);
    }

    [Fact]
    public async Task Panel_MarksTheAnswerThatDecidesWhetherOtherQuestionsApply_WithTheQuestionsItMayAffect()
    {
        var body = await GetPageAsync(DraftProfile());

        var welfare = QuestionMarkup(body, WelfareQuestionId);

        Assert.Contains("data-answer-editor data-relevance-affects=\"5.2, 5.3\">", welfare);

        // No other control is marked.
        Assert.Equal(1, Regex.Count(welfare, "data-relevance-affects"));
    }

    [Fact]
    public async Task Panel_HasACollapsibleListOfTheOtherSpeciesToApplyTheAnswerTo_WithACircleAndChevronAfterItsText()
    {
        var body = await GetPageAsync(DraftProfile());

        var welfare = QuestionMarkup(body, WelfareQuestionId);

        Assert.Contains("<details class=\"govuk-details app-picker app-species-picker\" data-species-picker>", welfare);
        Assert.Matches(
            "Apply this answer also to: <span data-species-picker-names></span></span>\\s*<span class=\"govuk-accordion-nav__chevron govuk-accordion-nav__chevron--down\" aria-hidden=\"true\"></span>",
            welfare);
        Assert.Contains($"id=\"species-{WelfareQuestionId}-pick-all\" type=\"checkbox\" data-species-pick-all", welfare);
        Assert.Contains("<label class=\"govuk-label govuk-checkboxes__label\" for=\"species-" + WelfareQuestionId + "-pick-all\">All</label>", welfare);
        Assert.Contains($"value=\"{CattleId}\" data-species-pick=\"{CattleId}\" data-species-name=\"Cattle\"", welfare);
        Assert.Contains($"value=\"{BadgerId}\" data-species-pick=\"{BadgerId}\" data-species-name=\"Meles meles (badger)\"", welfare);
    }

    // As the legacy panel's table had them, the picker of the other species and the buttons share a row, the buttons at the right.
    [Fact]
    public async Task Panel_PutsThePickerAndTheButtonsInOneRow_TheButtonsAtTheRight()
    {
        var body = await GetPageAsync(DraftProfile());

        var welfare = QuestionMarkup(body, WelfareQuestionId);

        var footer = welfare.IndexOf("<div class=\"app-answer-panel__footer\">", StringComparison.Ordinal);
        var picker = welfare.IndexOf("data-species-picker>", StringComparison.Ordinal);
        var buttons = welfare.IndexOf("<div class=\"govuk-button-group app-button-group--right\">", StringComparison.Ordinal);

        Assert.True(footer >= 0 && picker > footer && buttons > picker, "The picker, then the buttons, should be inside the footer.");
        Assert.Matches("data-species-ok>OK</button>\\s*<button[^>]* data-species-cancel>Cancel</button>", welfare[buttons..]);
    }

    [Fact]
    public async Task Question_SaysSoWhenTheProfileHasNoSpecies()
    {
        var body = await GetPageAsync(DraftProfile(), CreateApi(answers: new ProfileSectionAnswersDto()));

        Assert.Contains("This question repeats per-species, but the current profile does not have any affected species.", body);
        Assert.DoesNotContain("data-species-question", body);
    }

    [Fact]
    public async Task Page_HasTheDialogThatConfirmsAChangeToAnAnswerThatDecidesWhetherOtherQuestionsApply()
    {
        var body = await GetPageAsync(DraftProfile());

        var panel = body.IndexOf("id=\"profile-questions-panel\"", StringComparison.Ordinal);
        var formEnd = body.IndexOf("</form>", panel, StringComparison.Ordinal);
        var dialog = body.IndexOf("<dialog class=\"app-modal\" id=\"relevance-confirm-dialog\"", StringComparison.Ordinal);

        Assert.True(dialog > formEnd, "The dialog should follow the form that posts the answers.");
        Assert.Contains("<h2 class=\"govuk-heading-m\" id=\"relevance-confirm-dialog-title\">Confirm</h2>", body);
        Assert.Contains("Changing the answer to this question may affect the relevancy of other parts of this section, any questions that become irrelevant will be cleared and hidden. Questions that may be affected are:", body);
        Assert.Contains("<strong data-relevance-questions></strong>.", body);
        Assert.Contains("Do you want to continue?", body);
        Assert.Contains("data-relevance-ok>OK</button>", body[dialog..]);
        Assert.Contains("data-confirm-close>Cancel</button>", body[dialog..]);
    }

    [Fact]
    public async Task Page_HasAHiddenButtonThatSavesTheSectionToRefreshTheQuestions_InsideTheForm()
    {
        var body = await GetPageAsync(DraftProfile());

        var formStart = body.IndexOf("name=\"lastUpdated\"", StringComparison.Ordinal);
        var panel = body.IndexOf("id=\"profile-questions-panel\"", StringComparison.Ordinal);
        var button = body.IndexOf("<button type=\"submit\" hidden data-relevance-refresh", StringComparison.Ordinal);

        Assert.True(formStart >= 0 && button > formStart && button < panel, "The button should be in the form that posts the answers.");
        Assert.Contains("?handler=RefreshQuestions\"", body[button..body.IndexOf("</button>", button, StringComparison.Ordinal)]);
    }

    [Fact]
    public async Task Page_OffersNoConfirmationDialogOrRefreshButton_ToAUserWhoCannotEditTheSection()
    {
        var body = await GetPageAsync(ReadOnlyDraftProfile());

        Assert.DoesNotContain("relevance-confirm-dialog", body);
        Assert.DoesNotContain("data-relevance-refresh", body);
    }

    [Fact]
    public async Task Page_LoadsTheScriptsThatEditTheSpeciesAnswersAndConfirmTheChange_AfterTheRichTextEditor()
    {
        var body = await GetPageAsync(DraftProfile());

        var richText = body.IndexOf("/js/richtext-toolbar.js", StringComparison.Ordinal);
        var species = body.IndexOf("/js/question-species.js", StringComparison.Ordinal);
        var relevance = body.IndexOf("/js/relevance-confirm.js", StringComparison.Ordinal);

        Assert.True(richText >= 0 && species > richText && relevance > species);
    }

    // The scripts that keep the user's place across the refresh and show a field as the answer it depends on changes are on the page.
    [Fact]
    public async Task Page_LoadsTheScriptsThatKeepTheUsersPlaceAcrossTheRefresh_AndShowFieldsAsTheirAnswerChanges()
    {
        var body = await GetPageAsync(DraftProfile());

        Assert.Contains("/js/refresh-scroll.js", body);
        Assert.Contains("/js/field-visibility.js", body);
    }

    [Fact]
    public async Task StandardQuestion_MarksTheDropDownOfAnAnswerThatDecidesWhetherOtherQuestionsApply()
    {
        var metadata = Metadata();
        var standard = metadata.Sections[0].Questions[0] with { IsPerSpecies = false };
        var body = await GetPageAsync(
            DraftProfile(),
            CreateApi(metadata: metadata with { Sections = [metadata.Sections[0] with { Questions = [standard] }] }, answers: new ProfileSectionAnswersDto()));

        Assert.Contains($"<select class=\"govuk-select\" id=\"field_{WelfareFieldId}\" name=\"field_{WelfareFieldId}\" data-relevance-affects=\"5.2, 5.3\">", body);
    }

    [Fact]
    public async Task StandardQuestion_MarksNoDropDown_WhenItsAnswerDecidesNothing()
    {
        var metadata = Metadata();
        var standard = metadata.Sections[0].Questions[0] with
        {
            IsPerSpecies = false,
            Fields = [Welfare() with { AffectsRelevanceOf = [] }]
        };
        var body = await GetPageAsync(
            DraftProfile(),
            CreateApi(metadata: metadata with { Sections = [metadata.Sections[0] with { Questions = [standard] }] }));

        Assert.DoesNotContain("data-relevance-affects", body);
    }

    [Fact]
    public async Task Save_SendsEachSpeciesAnswerPosted_AgainstItsSpecies()
    {
        var sections = CreateApi();

        await PostAsync(
            sections,
            "SaveSection",
            ($"species_{CattleId}_field_{WelfareFieldId}", "false"),
            ($"species_{BadgerId}_field_{WelfareFieldId}", "true"),
            ($"species_{CattleId}_field_{CommentsFieldId}", "<p>Changed</p>"));

        var saved = sections.SavedAnswers!;

        Assert.Equal(3, saved.Count);
        Assert.False(saved.Single(answer => answer.ProfileFieldId == WelfareFieldId && answer.SpeciesId == CattleId).BooleanValue);
        Assert.True(saved.Single(answer => answer.ProfileFieldId == WelfareFieldId && answer.SpeciesId == BadgerId).BooleanValue);
        Assert.Equal("<p>Changed</p>", saved.Single(answer => answer.ProfileFieldId == CommentsFieldId && answer.SpeciesId == CattleId).TextValue);
    }

    [Fact]
    public async Task Refresh_SavesTheAnswers_WithoutAnnouncingTheSave()
    {
        var sections = CreateApi();

        var body = await PostAsync(sections, "RefreshQuestions", ($"species_{CattleId}_field_{WelfareFieldId}", "false"));

        Assert.Equal(1, sections.SaveProfileVersionSectionCallCount);
        Assert.DoesNotContain(EditProfileQuestionsModel.SaveSucceededMessage, body);
        Assert.DoesNotContain("section-action-success-title", body);
    }

    [Fact]
    public async Task Save_AnnouncesTheSave_AsBefore()
    {
        var sections = CreateApi();

        var body = await PostAsync(sections, "SaveSection", ($"species_{CattleId}_field_{WelfareFieldId}", "false"));

        Assert.Contains(EditProfileQuestionsModel.SaveSucceededMessage, body);
    }
}
