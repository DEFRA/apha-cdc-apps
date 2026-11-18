using CDC.Common.Contracts;
using CDC.Web.Infrastructure;
using CDC.Web.Models;

namespace CDC.Web.Tests.Infrastructure;

public class ProfileAnswerFormatterTests
{
    private static readonly Guid FirstOption = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid SecondOption = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ThirdOption = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static readonly IReadOnlyList<ReferenceValueDto> Options =
    [
        new() { Id = FirstOption, Value = "Mild" },
        new() { Id = SecondOption, Value = "Moderate" },
        new() { Id = ThirdOption, Value = "Severe" }
    ];

    private static ProfileFieldMetadataDto Field(string dataType) => new() { Id = Guid.NewGuid(), Name = "Field", DataTypeName = dataType };

    private static ProfileFieldValueDto[] Values(params ProfileFieldValueDto[] values) => values;

    [Theory]
    [InlineData(true, "true")]
    [InlineData(false, "false")]
    [InlineData(null, "")]
    public void FormValue_PostsABooleanAsTrueFalseOrNothing(bool? answer, string expected)
    {
        var values = answer is null ? [] : Values(new ProfileFieldValueDto { BooleanValue = answer });

        Assert.Equal(expected, ProfileAnswerFormatter.FormValue(Field("Boolean"), values));
    }

    [Fact]
    public void FormValue_PostsTheSelectedItemOfAList_OrNothingWhenNoneIsSelected()
    {
        Assert.Equal(SecondOption.ToString(), ProfileAnswerFormatter.FormValue(Field("List"), Values(new ProfileFieldValueDto { ListValue = SecondOption })));
        Assert.Equal(string.Empty, ProfileAnswerFormatter.FormValue(Field("List"), []));
        Assert.Equal(string.Empty, ProfileAnswerFormatter.FormValue(Field("List"), Values(new ProfileFieldValueDto { ListValue = Guid.Empty })));
    }

    [Fact]
    public void FormValue_PostsEveryItemOfAMultiValueListSeparatedByCommas()
    {
        var values = Values(
            new ProfileFieldValueDto { ListValue = FirstOption },
            new ProfileFieldValueDto { ListValue = null },
            new ProfileFieldValueDto { ListValue = ThirdOption });

        Assert.Equal($"{FirstOption},{ThirdOption}", ProfileAnswerFormatter.FormValue(Field("MultiValueList"), values));
        Assert.Equal(string.Empty, ProfileAnswerFormatter.FormValue(Field("MultiValueList"), []));
    }

    [Theory]
    [InlineData("1.5000", "1.5")]
    [InlineData("14.0000", "14")]
    [InlineData("0.1234", "0.1234")]
    public void FormValue_PostsADecimalWithoutLosingItsPrecisionOrAddingTrailingZeros(string stored, string expected)
    {
        var values = Values(new ProfileFieldValueDto { DecimalValue = decimal.Parse(stored, System.Globalization.CultureInfo.InvariantCulture) });

        Assert.Equal(expected, ProfileAnswerFormatter.FormValue(Field("Decimal"), values));
    }

    [Theory]
    [InlineData("Text")]
    [InlineData("Long Text")]
    public void FormValue_PostsTheTextAsStored(string dataType)
    {
        Assert.Equal("<p>Some <em>text</em></p>", ProfileAnswerFormatter.FormValue(Field(dataType), Values(new ProfileFieldValueDto { TextValue = "<p>Some <em>text</em></p>" })));
        Assert.Equal(string.Empty, ProfileAnswerFormatter.FormValue(Field(dataType), []));
    }

    [Theory]
    [InlineData(true, "Yes")]
    [InlineData(false, "No")]
    [InlineData(null, "")]
    public void SummaryText_ShowsABooleanAsYesNoOrNothing(bool? answer, string expected)
    {
        var values = answer is null ? [] : Values(new ProfileFieldValueDto { BooleanValue = answer });

        Assert.Equal(expected, ProfileAnswerFormatter.SummaryText(Field("Boolean"), values, []));
    }

    [Fact]
    public void SummaryText_ShowsTheTextOfTheSelectedItemOfAList_OrNothingWhenItIsNotAmongTheOptions()
    {
        Assert.Equal("Moderate", ProfileAnswerFormatter.SummaryText(Field("List"), Values(new ProfileFieldValueDto { ListValue = SecondOption }), Options));
        Assert.Equal(string.Empty, ProfileAnswerFormatter.SummaryText(Field("List"), Values(new ProfileFieldValueDto { ListValue = Guid.NewGuid() }), Options));
        Assert.Equal(string.Empty, ProfileAnswerFormatter.SummaryText(Field("List"), [], Options));
    }

    [Fact]
    public void SummaryText_ListsTheSelectedItemsOfAMultiValueListInTheOrderOfTheOptions()
    {
        var values = Values(new ProfileFieldValueDto { ListValue = ThirdOption }, new ProfileFieldValueDto { ListValue = FirstOption });

        Assert.Equal("Mild, Severe", ProfileAnswerFormatter.SummaryText(Field("MultiValueList"), values, Options));
    }

    [Fact]
    public void SummaryText_ShowsADecimalToTwoPlacesAndADateAsDayMonthYear()
    {
        Assert.Equal("1.50", ProfileAnswerFormatter.SummaryText(Field("Decimal"), Values(new ProfileFieldValueDto { DecimalValue = 1.5m }), []));
        Assert.Equal("02/03/2026", ProfileAnswerFormatter.SummaryText(Field("Date"), Values(new ProfileFieldValueDto { DateValue = new DateTime(2026, 3, 2) }), []));
        Assert.Equal(string.Empty, ProfileAnswerFormatter.SummaryText(Field("Decimal"), [], []));
    }

    [Fact]
    public void SummaryText_ShowsTheFirstFiftyCharactersOfTheTextWithItsMarkupRemoved()
    {
        var values = Values(new ProfileFieldValueDto { TextValue = "<p>Only the impact &amp; associated with <em>any</em> severe disease would be considered here, which is rare.</p>" });

        var summary = ProfileAnswerFormatter.SummaryText(Field("Long Text"), values, []);

        Assert.Equal("Only the impact & associated with any severe disea", summary);
        Assert.Equal(ProfileAnswerFormatter.SummaryLength, summary.Length);
    }

    [Fact]
    public void SummaryText_ShowsAShortTextInFull_AndNothingWhenThereIsNone()
    {
        Assert.Equal("Wild animal", ProfileAnswerFormatter.SummaryText(Field("Text"), Values(new ProfileFieldValueDto { TextValue = "Wild animal" }), []));
        Assert.Equal(string.Empty, ProfileAnswerFormatter.SummaryText(Field("Long Text"), [], []));
    }
}
