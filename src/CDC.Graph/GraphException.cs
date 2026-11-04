namespace CDC.Graph;

/// <summary>Raised when a Microsoft Graph call fails (token request, network, timeout, or a non-success response).</summary>
public sealed class GraphException : Exception
{
    /// <summary>Initialises a new instance of the <see cref="GraphException"/> class.</summary>
    public GraphException()
    {
    }

    /// <summary>Initialises a new instance of the <see cref="GraphException"/> class.</summary>
    /// <param name="message">What failed. Never includes response bodies, tokens or personal data.</param>
    public GraphException(string message)
        : base(message)
    {
    }

    /// <summary>Initialises a new instance of the <see cref="GraphException"/> class.</summary>
    /// <param name="message">What failed. Never includes response bodies, tokens or personal data.</param>
    /// <param name="innerException">The underlying failure.</param>
    public GraphException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
