using CDC.Common.Contracts;
using CDC.Web.Infrastructure;
using CDC.Web.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace CDC.Web.Tests.Infrastructure;

public class ProfileRevisionDatesFormTests
{
    private static readonly Guid SectionId = Guid.NewGuid();
    private static readonly Guid YearlyId = Guid.NewGuid();
    private static readonly Guid ProposedId = Guid.NewGuid();
    private const string RowVersion = "AQIDBAUGBwg=";

    private static ProfileVersionAllRevisionDatesDto Source() => new()
    {
        ProfileVersionId = Guid.NewGuid(),
        Sections =
        [
            new ProfileSectionRevisionDatesDto
            {
                ProfileSectionId = SectionId,
                SectionName = "Summary",
                SectionNumber = 1,
                AuthorReviewFrequencyId = YearlyId,
                NextPolicyReview = new DateTime(2027, 6, 1),
                VaReviewStatusId = ProposedId,
                LastUpdated = [1, 2, 3, 4, 5, 6, 7, 8]
            }
        ],
        ReviewFrequencies = [new ReviewLookupItemDto { Id = YearlyId, Name = "Every year" }],
        ReviewStatuses = [new ReviewLookupItemDto { Id = ProposedId, Name = "Proposed" }]
    };

    private static FormCollection Form(params (string Field, string Value)[] values)
    {
        var fields = new Dictionary<string, StringValues>
        {
            [ProfileRevisionDatesForm.FieldName(SectionId, ProfileRevisionDatesForm.FieldLastUpdated)] = RowVersion
        };

        foreach (var (field, value) in values)
        {
            fields[ProfileRevisionDatesForm.FieldName(SectionId, field)] = value;
        }

        return new FormCollection(fields);
    }

    [Fact]
    public void BuildRows_FormatsIdsAndDates_AndEncodesTheRowVersion()
    {
        var row = Assert.Single(ProfileRevisionDatesForm.BuildRows(Source()));

        Assert.Equal(SectionId, row.ProfileSectionId);
        Assert.Equal("1. Summary", row.SectionDisplay);
        Assert.Equal(YearlyId.ToString(), row.AuthorFrequency);
        Assert.Equal(string.Empty, row.VaFrequency);
        Assert.Equal("1 June 2027", row.NextPolicyReview);
        Assert.Equal(string.Empty, row.NextAuthorReview);
        Assert.Equal(ProposedId.ToString(), row.VaStatus);
        Assert.Equal(RowVersion, row.LastUpdated);
    }

    [Fact]
    public void BuildOptions_PutsABlankOptionFirst()
    {
        var options = ProfileRevisionDatesForm.BuildOptions(Source().ReviewFrequencies);

        Assert.Equal(2, options.Count);
        Assert.Equal(string.Empty, options[0].Value);
        Assert.Equal(YearlyId.ToString(), options[1].Value);
        Assert.Equal("Every year", options[1].Text);
    }

    [Fact]
    public void Parse_ReadsEveryValue_WhenTheFormIsValid()
    {
        var result = ProfileRevisionDatesForm.Parse(
            Form(
                (ProfileRevisionDatesForm.FieldAuthorFrequency, YearlyId.ToString()),
                (ProfileRevisionDatesForm.FieldNextVa, "3 March 2028"),
                (ProfileRevisionDatesForm.FieldPolicyStatus, ProposedId.ToString())),
            Source());

        Assert.True(result.IsValid);
        var input = Assert.Single(result.Inputs);
        Assert.Equal(SectionId, input.ProfileSectionId);
        Assert.Equal(YearlyId, input.AuthorReviewFrequencyId);
        Assert.Null(input.VaReviewFrequencyId);
        Assert.Equal(new DateTime(2028, 3, 3), input.NextVaReview);
        Assert.Null(input.NextAuthorReview);
        Assert.Equal(ProposedId, input.PolicyReviewStatusId);
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }, input.LastUpdated);
    }

    [Fact]
    public void Parse_TreatsBlankSelectionsAndDates_AsClearingTheValue()
    {
        var result = ProfileRevisionDatesForm.Parse(Form(), Source());

        Assert.True(result.IsValid);
        var input = Assert.Single(result.Inputs);
        Assert.Null(input.AuthorReviewFrequencyId);
        Assert.Null(input.NextPolicyReview);
        Assert.Null(input.VaReviewStatusId);
    }

    [Theory]
    [InlineData("1 January 2027", 2027, 1, 1)]
    [InlineData("1 Jan 2027", 2027, 1, 1)]
    [InlineData("01/02/2027", 2027, 2, 1)]
    [InlineData("1/2/2027", 2027, 2, 1)]
    [InlineData("1-2-2027", 2027, 2, 1)]
    [InlineData("2027-03-04", 2027, 3, 4)]
    [InlineData("  5 May 2030  ", 2030, 5, 5)]
    public void TryParseDate_ReadsTheSupportedDateFormats(string text, int year, int month, int day)
    {
        Assert.True(ProfileRevisionDatesForm.TryParseDate(text, out var date));
        Assert.Equal(new DateTime(year, month, day), date);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("soon")]
    [InlineData("31 February 2027")]
    [InlineData("2027/01/01")]
    public void TryParseDate_RefusesTextThatIsNotADateInASupportedFormat(string? text) =>
        Assert.False(ProfileRevisionDatesForm.TryParseDate(text, out _));

    [Theory]
    [InlineData("1 January 2027", 2027, 1, 1)]
    [InlineData("1 Jan 2027", 2027, 1, 1)]
    [InlineData("01/02/2027", 2027, 2, 1)]
    [InlineData("1/2/2027", 2027, 2, 1)]
    [InlineData("2027-03-04", 2027, 3, 4)]
    [InlineData("  5 May 2030  ", 2030, 5, 5)]
    public void Parse_AcceptsTheSupportedDateFormats(string text, int year, int month, int day)
    {
        var result = ProfileRevisionDatesForm.Parse(Form((ProfileRevisionDatesForm.FieldNextAuthor, text)), Source());

        Assert.True(result.IsValid);
        Assert.Equal(new DateTime(year, month, day), Assert.Single(result.Inputs).NextAuthorReview);
    }

    [Theory]
    [InlineData(ProfileRevisionDatesForm.FieldNextAuthor, "Next author review date must be a valid date")]
    [InlineData(ProfileRevisionDatesForm.FieldNextVa, "Next VA review date must be a valid date")]
    [InlineData(ProfileRevisionDatesForm.FieldNextPolicy, "Next policy review date must be a valid date")]
    public void Parse_ReportsAnUnparseableDate_WithTheLegacyMessage(string field, string expectedMessage)
    {
        var result = ProfileRevisionDatesForm.Parse(Form((field, "31 February 2027")), Source());

        Assert.False(result.IsValid);
        Assert.Equal($"1. Summary: {expectedMessage}", Assert.Single(result.Errors));
        Assert.Empty(result.Inputs);
        Assert.Single(Assert.Single(result.Rows).Errors);
    }

    [Theory]
    [InlineData("31 December 1899")]
    [InlineData("7 June 2079")]
    public void Parse_ReportsADateOutsideTheSupportedRange(string text)
    {
        var result = ProfileRevisionDatesForm.Parse(Form((ProfileRevisionDatesForm.FieldNextAuthor, text)), Source());

        Assert.Equal(
            "1. Summary: Next author review date must be between 1 January 1900 and 6 June 2079",
            Assert.Single(result.Errors));
    }

    [Theory]
    [InlineData("1 January 1900")]
    [InlineData("6 June 2079")]
    public void Parse_AcceptsDatesAtTheRangeBoundaries(string text)
    {
        var result = ProfileRevisionDatesForm.Parse(Form((ProfileRevisionDatesForm.FieldNextAuthor, text)), Source());

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Parse_RejectsAFrequencyOrStatusThatWasNotOffered()
    {
        var result = ProfileRevisionDatesForm.Parse(
            Form(
                (ProfileRevisionDatesForm.FieldVaFrequency, Guid.NewGuid().ToString()),
                (ProfileRevisionDatesForm.FieldAuthorStatus, "not-a-guid")),
            Source());

        Assert.Equal(2, result.Errors.Count);
        Assert.Contains("1. Summary: VA review frequency is not a valid option.", result.Errors);
        Assert.Contains("1. Summary: Author review status is not a valid option.", result.Errors);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not base64!")]
    [InlineData("AQID")]
    public void Parse_ReportsAMissingOrMalformedRowVersion(string rowVersion)
    {
        var form = new FormCollection(new Dictionary<string, StringValues>
        {
            [ProfileRevisionDatesForm.FieldName(SectionId, ProfileRevisionDatesForm.FieldLastUpdated)] = rowVersion
        });

        var result = ProfileRevisionDatesForm.Parse(form, Source());

        Assert.Contains(result.Errors, error => error.Contains("could not be read", StringComparison.Ordinal));
        Assert.Empty(result.Inputs);
    }

    [Fact]
    public void Parse_ReportsEverySectionsErrors_AndKeepsEveryRow()
    {
        var secondSectionId = Guid.NewGuid();
        var source = Source() with
        {
            Sections =
            [
                .. Source().Sections,
                new ProfileSectionRevisionDatesDto { ProfileSectionId = secondSectionId, SectionName = "Epidemiology", SectionNumber = 2, LastUpdated = [1, 2, 3, 4, 5, 6, 7, 8] }
            ]
        };
        var form = new FormCollection(new Dictionary<string, StringValues>
        {
            [ProfileRevisionDatesForm.FieldName(SectionId, ProfileRevisionDatesForm.FieldLastUpdated)] = RowVersion,
            [ProfileRevisionDatesForm.FieldName(SectionId, ProfileRevisionDatesForm.FieldNextAuthor)] = "nope",
            [ProfileRevisionDatesForm.FieldName(secondSectionId, ProfileRevisionDatesForm.FieldLastUpdated)] = RowVersion,
            [ProfileRevisionDatesForm.FieldName(secondSectionId, ProfileRevisionDatesForm.FieldNextPolicy)] = "also nope"
        });

        var result = ProfileRevisionDatesForm.Parse(form, source);

        Assert.Equal(2, result.Rows.Count);
        Assert.Equal(2, result.Errors.Count);
        Assert.StartsWith("1. Summary:", result.Errors[0], StringComparison.Ordinal);
        Assert.StartsWith("2. Epidemiology:", result.Errors[1], StringComparison.Ordinal);
        Assert.Single(result.Rows[0].Errors);
        Assert.Single(result.Rows[1].Errors);
    }

    [Fact]
    public void ReadSetForAll_ReadsEachPostedControl()
    {
        var form = new FormCollection(new Dictionary<string, StringValues>
        {
            [ProfileRevisionDatesForm.SetForAllFieldName(ProfileRevisionDatesForm.FieldAuthorStatus)] = ProposedId.ToString(),
            [ProfileRevisionDatesForm.SetForAllFieldName(ProfileRevisionDatesForm.FieldNextVa)] = " 1 May 2027 "
        });

        var setForAll = ProfileRevisionDatesForm.ReadSetForAll(form);

        Assert.Equal(ProposedId.ToString(), setForAll.AuthorStatus);
        Assert.Equal("1 May 2027", setForAll.NextVaReview);
        Assert.Equal(string.Empty, setForAll.PolicyStatus);
    }

    [Fact]
    public void FieldName_UsesTheCompactSectionId()
    {
        Assert.Equal($"rev_{SectionId:N}_AuthorFreq", ProfileRevisionDatesForm.FieldName(SectionId, ProfileRevisionDatesForm.FieldAuthorFrequency));
    }

    // An author may change only the review their role owns; the other two keep what CDC.Api holds, whatever the form posts.
    [Fact]
    public void Parse_KeepsTheStoredValuesOfAReviewTheUserMayNotChange_WhateverTheFormPosts()
    {
        var form = Form(
            (ProfileRevisionDatesForm.FieldAuthorFrequency, Guid.NewGuid().ToString()),
            (ProfileRevisionDatesForm.FieldNextPolicy, "1 January 2030"),
            (ProfileRevisionDatesForm.FieldVaStatus, string.Empty),
            (ProfileRevisionDatesForm.FieldNextVa, "3 March 2028"),
            (ProfileRevisionDatesForm.FieldVaFrequency, YearlyId.ToString()));

        var result = ProfileRevisionDatesForm.Parse(form, Source(), new EditableReviews(Author: false, Va: true, Policy: false));

        Assert.True(result.IsValid);
        var input = Assert.Single(result.Inputs);
        Assert.Equal(YearlyId, input.AuthorReviewFrequencyId);
        Assert.Equal(new DateTime(2027, 6, 1), input.NextPolicyReview);
        Assert.Null(input.VaReviewStatusId);
        Assert.Equal(new DateTime(2028, 3, 3), input.NextVaReview);
        Assert.Equal(YearlyId, input.VaReviewFrequencyId);
    }

    [Fact]
    public void Parse_TakesTheStoredStatusOfAReviewTheUserMayNotChange()
    {
        var result = ProfileRevisionDatesForm.Parse(Form(), Source(), new EditableReviews(Author: true, Va: false, Policy: true));

        var input = Assert.Single(result.Inputs);
        Assert.Equal(ProposedId, input.VaReviewStatusId);
        Assert.Equal(ProposedId.ToString(), Assert.Single(result.Rows).VaStatus);
    }

    [Fact]
    public void Parse_DoesNotValidateWhatAReviewTheUserMayNotChangeHolds()
    {
        var source = Source();
        var section = source.Sections[0] with { NextVaReview = new DateTime(1800, 1, 1) };

        var result = ProfileRevisionDatesForm.Parse(Form(), source with { Sections = [section] }, new EditableReviews(Author: true, Va: false, Policy: true));

        Assert.True(result.IsValid);
        Assert.Equal(new DateTime(1800, 1, 1), Assert.Single(result.Inputs).NextVaReview);
    }

    [Fact]
    public void EditableReviews_AreEditableAsAWhole_ByDefault_AndNoneWhenTheTabIsReadOnly()
    {
        Assert.True(EditableReviews.All.Any);
        Assert.False(default(EditableReviews).Any);
        Assert.True(new EditableReviews(Author: false, Va: true, Policy: false).Any);
    }
}
