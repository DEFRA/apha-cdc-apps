using CDC.Api.Domain.Exceptions;
using FluentAssertions;

namespace CDC.Api.Tests.Domain;

public class NotFoundExceptionTests
{
    [Fact]
    public void Ctor_Parameterless_SetsDefaultMessage()
    {
        var ex = new NotFoundException();

        ex.Message.Should().Be("The requested resource was not found.");
    }

    [Fact]
    public void Ctor_WithMessage_SetsMessage()
    {
        var ex = new NotFoundException("Species 'Cattle' was not found.");

        ex.Message.Should().Be("Species 'Cattle' was not found.");
    }

    [Fact]
    public void Ctor_WithMessageAndInnerException_SetsMessageAndInnerException()
    {
        var inner = new InvalidOperationException("Database error");
        var ex = new NotFoundException("Lookup failed", inner);

        ex.Message.Should().Be("Lookup failed");
        ex.InnerException.Should().Be(inner);
    }

    [Fact]
    public void For_CreatesExceptionWithFormattedMessage()
    {
        var ex = NotFoundException.For("Species", "Cattle");

        ex.Message.Should().Be("Species 'Cattle' was not found.");
    }

    [Fact]
    public void For_WithNumericKey_FormatsKeyCorrectly()
    {
        var ex = NotFoundException.For("Profile", 42);

        ex.Message.Should().Be("Profile '42' was not found.");
    }
}
