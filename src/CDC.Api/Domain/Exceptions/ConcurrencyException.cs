namespace CDC.Api.Domain.Exceptions;

/// <summary>
/// Thrown when an update is rejected because the record changed after it was read. Raised
/// when <c>spuSpeciesAnswerData</c> reports a row version mismatch. Surfaces as HTTP 409.
/// </summary>
public sealed class ConcurrencyException : Exception
{
    /// <summary>Initialises a new instance of the <see cref="ConcurrencyException"/> class.</summary>
    public ConcurrencyException()
        : base("The record has been edited by another user.")
    {
    }

    /// <summary>Initialises a new instance of the <see cref="ConcurrencyException"/> class.</summary>
    /// <param name="message">Description of the conflict.</param>
    public ConcurrencyException(string message)
        : base(message)
    {
    }

    /// <summary>Initialises a new instance of the <see cref="ConcurrencyException"/> class.</summary>
    /// <param name="message">Description of the conflict.</param>
    /// <param name="innerException">The underlying cause.</param>
    public ConcurrencyException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
