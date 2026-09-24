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

    [Fact]
    public void For_WithGuidKey_FormatsKeyCorrectly()
    {
        var id = Guid.NewGuid();
        var ex = NotFoundException.For("Disease", id);

        ex.Message.Should().Be($"Disease '{id}' was not found.");
    }

    [Fact]
    public void For_WithSpecialCharactersInEntityName_FormatsCorrectly()
    {
        var ex = NotFoundException.For("User Profile", "john.doe@example.com");

        ex.Message.Should().Be("User Profile 'john.doe@example.com' was not found.");
    }

    [Fact]
    public void For_WithNullableKey_FormatsAsEmptyString()
    {
        object? nullKey = null;
        var ex = NotFoundException.For("Record", nullKey!);

        ex.Message.Should().Be("Record '' was not found.");
    }

    [Fact]
    public void Exception_InheritsFromException()
    {
        var ex = new NotFoundException("Test");

        ex.Should().BeAssignableTo<Exception>();
    }

    [Fact]
    public void Exception_CanBeThrowAndCaught()
    {
        Action action = () => throw new NotFoundException("Item not found");

        action.Should().Throw<NotFoundException>().WithMessage("Item not found");
    }

    [Fact]
    public void Exception_PreservesStackTrace()
    {
        NotFoundException? caughtException = null;
        try
        {
            throw new NotFoundException("Test error");
        }
        catch (NotFoundException ex)
        {
            caughtException = ex;
        }

        caughtException.Should().NotBeNull();
        caughtException!.StackTrace.Should().NotBeNullOrEmpty();
    }
}
