using CDC.Api.Application.Behaviours;
using CDC.Api.Features.Species.Commands;
using FluentAssertions;
using FluentValidation;
using MediatR;

namespace CDC.Api.Tests.Application;

public class ValidationBehaviourTests
{
    private static readonly UpdateSpeciesAnswerDataCommand ValidCommand = new()
    {
        SpeciesId = Guid.NewGuid(),
        LastUpdated = [0, 0, 0, 0, 0, 0, 7, 209],
        Changes = [new SpeciesFieldValueChange { FieldId = Guid.NewGuid(), Kind = SpeciesFieldValueKind.Boolean, BooleanValue = true }]
    };

    [Fact]
    public async Task Handle_InvokesTheHandler_WhenThereAreNoValidators()
    {
        var behaviour = new ValidationBehaviour<UpdateSpeciesAnswerDataCommand, string>([]);

        var response = await behaviour.Handle(ValidCommand, _ => Task.FromResult("handled"), CancellationToken.None);

        response.Should().Be("handled");
    }

    [Fact]
    public async Task Handle_InvokesTheHandler_WhenValidationPasses()
    {
        var behaviour = new ValidationBehaviour<UpdateSpeciesAnswerDataCommand, string>(
            [new UpdateSpeciesAnswerDataCommandValidator()]);

        var response = await behaviour.Handle(ValidCommand, _ => Task.FromResult("handled"), CancellationToken.None);

        response.Should().Be("handled");
    }

    [Fact]
    public async Task Handle_ThrowsAndSkipsTheHandler_WhenValidationFails()
    {
        var behaviour = new ValidationBehaviour<UpdateSpeciesAnswerDataCommand, string>(
            [new UpdateSpeciesAnswerDataCommandValidator()]);
        var handlerWasCalled = false;

        RequestHandlerDelegate<string> next = _ =>
        {
            handlerWasCalled = true;
            return Task.FromResult("handled");
        };

        var act = async () => await behaviour.Handle(new UpdateSpeciesAnswerDataCommand(), next, CancellationToken.None);

        var exception = await act.Should().ThrowAsync<ValidationException>();
        exception.Which.Errors.Should().NotBeEmpty();
        handlerWasCalled.Should().BeFalse();
    }
}
