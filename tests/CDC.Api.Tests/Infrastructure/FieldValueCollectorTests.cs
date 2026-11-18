using CDC.Api.Infrastructure.Repositories;
using FluentAssertions;

namespace CDC.Api.Tests.Infrastructure;

public sealed class FieldValueCollectorTests
{
    private static readonly Guid FieldId = Guid.NewGuid();
    private static readonly Guid OtherFieldId = Guid.NewGuid();
    private static readonly Guid SpeciesId = Guid.NewGuid();
    private static readonly Guid RowId = Guid.NewGuid();

    [Fact]
    public void Add_KeepsTheLastRowOfASingleValueFieldStoredMoreThanOnce()
    {
        var collector = new FieldValueCollector<string>();

        collector.Add(FieldId, null, null, isListValue: false, "first");
        collector.Add(OtherFieldId, null, null, isListValue: false, "other");
        collector.Add(FieldId, null, null, isListValue: false, "second");
        collector.Add(FieldId, null, null, isListValue: false, "third");

        collector.Values.Should().Equal("third", "other");
    }

    [Fact]
    public void Add_KeepsEveryRowOfAListValue()
    {
        var collector = new FieldValueCollector<string>();

        collector.Add(FieldId, null, null, isListValue: true, "item one");
        collector.Add(FieldId, null, null, isListValue: true, "item two");

        collector.Values.Should().Equal("item one", "item two");
    }

    [Fact]
    public void Add_TreatsAnotherSpeciesOrRowAsAnotherAnswer()
    {
        var collector = new FieldValueCollector<string>();

        collector.Add(FieldId, null, null, isListValue: false, "profile");
        collector.Add(FieldId, SpeciesId, null, isListValue: false, "species");
        collector.Add(FieldId, null, RowId, isListValue: false, "row");
        collector.Add(FieldId, SpeciesId, null, isListValue: false, "species again");

        collector.Values.Should().Equal("profile", "species again", "row");
    }

    [Fact]
    public void Values_IsEmpty_WhenNothingWasAdded()
    {
        new FieldValueCollector<string>().Values.Should().BeEmpty();
    }
}
