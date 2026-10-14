using CDC.Api.Domain.Exceptions;
using FluentAssertions;

namespace CDC.Api.Tests.Domain;

public class SpeciesReorderBlockedExceptionTests
{
    [Fact]
    public void Ctor_Parameterless_SetsDefaultMessage()
    {
        var ex = new SpeciesReorderBlockedException();

        ex.Message.Should().Be("This species cannot be moved in that direction.");
    }

    [Fact]
    public void Ctor_WithMessage_SetsMessage()
    {
        var ex = new SpeciesReorderBlockedException("No sibling exists at the resulting position.");

        ex.Message.Should().Be("No sibling exists at the resulting position.");
    }

    [Fact]
    public void Ctor_WithMessageAndInnerException_SetsMessageAndInnerException()
    {
        var inner = new InvalidOperationException("Sequence number gap");
        var ex = new SpeciesReorderBlockedException("Move rejected", inner);

        ex.Message.Should().Be("Move rejected");
        ex.InnerException.Should().Be(inner);
    }
}
