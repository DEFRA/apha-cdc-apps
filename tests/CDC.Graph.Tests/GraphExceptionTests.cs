namespace CDC.Graph.Tests;

public class GraphExceptionTests
{
    [Fact]
    public void Constructor_WithoutArguments_HasADefaultMessage()
    {
        var exception = new GraphException();

        Assert.NotEmpty(exception.Message);
    }

    [Fact]
    public void Constructor_WithInnerException_KeepsMessageAndInnerException()
    {
        var inner = new InvalidOperationException("inner");

        var exception = new GraphException("outer", inner);

        Assert.Equal("outer", exception.Message);
        Assert.Same(inner, exception.InnerException);
    }
}
