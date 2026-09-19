using CDC.Api.Features.Species.Commands;
using FluentAssertions;

namespace CDC.Api.Tests.Species;

public class UpdateSpeciesAnswerDataCommandValidatorTests
{
    private readonly UpdateSpeciesAnswerDataCommandValidator validator = new();

    [Fact]
    public void ValidCommand_ShouldPass()
    {
        var result = validator.Validate(SpeciesTestData.UpdateCommand());

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void MissingProfileVersionId_ShouldFail()
    {
        // The legacy contract keys answer data on SpeciesId rather than a profile version id.
        var command = SpeciesTestData.UpdateCommand() with { SpeciesId = Guid.Empty };

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(failure => failure.PropertyName == nameof(command.SpeciesId));
    }

    [Fact]
    public void EmptyChanges_ShouldFail()
    {
        var command = SpeciesTestData.UpdateCommand() with { Changes = [] };

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(failure => failure.ErrorMessage == "At least one change is required.");
    }

    [Fact]
    public void DuplicateChanges_ShouldFail()
    {
        var command = SpeciesTestData.UpdateCommand(
            new SpeciesFieldValueChange { FieldId = SpeciesTestData.FieldId, Kind = SpeciesFieldValueKind.Boolean, BooleanValue = true },
            new SpeciesFieldValueChange { FieldId = SpeciesTestData.FieldId, Kind = SpeciesFieldValueKind.Boolean, BooleanValue = false });

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(failure => failure.ErrorMessage == "Each field may only appear once in a change set.");
    }

    [Fact]
    public void MissingRowVersion_ShouldFail()
    {
        var command = SpeciesTestData.UpdateCommand() with { LastUpdated = [1, 2] };

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(failure => failure.ErrorMessage == "The row version must be 8 bytes.");
    }

    [Fact]
    public void BooleanChangeWithoutValue_ShouldFail()
    {
        var command = SpeciesTestData.UpdateCommand(
            new SpeciesFieldValueChange { FieldId = SpeciesTestData.FieldId, Kind = SpeciesFieldValueKind.Boolean });

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(failure => failure.ErrorMessage == "A boolean value is required for a boolean field.");
    }

    [Fact]
    public void ListChangeWithEmptyGuid_ShouldFail()
    {
        var command = SpeciesTestData.UpdateCommand(
            new SpeciesFieldValueChange { FieldId = SpeciesTestData.FieldId, Kind = SpeciesFieldValueKind.List, ListValue = Guid.Empty });

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(failure => failure.ErrorMessage == "A list value must not be an empty GUID.");
    }

    [Fact]
    public void TextChangeWithoutValue_ShouldFail()
    {
        var command = SpeciesTestData.UpdateCommand(
            new SpeciesFieldValueChange { FieldId = SpeciesTestData.FieldId, Kind = SpeciesFieldValueKind.Text, TextValue = "  " });

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(failure => failure.ErrorMessage == "A text value is required for a text field.");
    }

    [Fact]
    public void MultiValueWithDuplicates_ShouldFail()
    {
        var command = SpeciesTestData.UpdateCommand(
            new SpeciesFieldValueChange
            {
                FieldId = SpeciesTestData.FieldId,
                Kind = SpeciesFieldValueKind.MultiValue,
                MultiValues = [SpeciesTestData.ListValueId, SpeciesTestData.ListValueId]
            });

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(failure => failure.ErrorMessage == "A multi-value field must not contain duplicate entries.");
    }

    [Fact]
    public void ClearingChangeWithValue_ShouldFail()
    {
        var command = SpeciesTestData.UpdateCommand(
            new SpeciesFieldValueChange { FieldId = SpeciesTestData.FieldId, Kind = SpeciesFieldValueKind.None, TextValue = "value" });

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(failure => failure.ErrorMessage == "A change that clears a field must not supply a value.");
    }

    [Fact]
    public void ClearingChangeWithoutValue_ShouldPass()
    {
        var command = SpeciesTestData.UpdateCommand(
            new SpeciesFieldValueChange { FieldId = SpeciesTestData.FieldId, Kind = SpeciesFieldValueKind.None });

        var result = validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }
}
