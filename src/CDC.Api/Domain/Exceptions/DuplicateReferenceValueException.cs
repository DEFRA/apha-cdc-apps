namespace CDC.Api.Domain.Exceptions;

/// <summary>
/// Thrown when a reference value change is rejected because another value in the same table
/// already has that lookup text, as raised by <c>spiReferenceValue</c>.
/// </summary>
public sealed class DuplicateReferenceValueException : Exception
{
    /// <summary>The message the legacy procedures raise, shown to the user unchanged.</summary>
    public const string DefaultMessage = "There is already a reference value with this name";

    /// <summary>Initialises a new instance of the <see cref="DuplicateReferenceValueException"/> class.</summary>
    public DuplicateReferenceValueException()
        : base(DefaultMessage)
    {
    }

    /// <summary>Initialises a new instance of the <see cref="DuplicateReferenceValueException"/> class.</summary>
    /// <param name="message">Description of the clash.</param>
    public DuplicateReferenceValueException(string message)
        : base(message)
    {
    }

    /// <summary>Initialises a new instance of the <see cref="DuplicateReferenceValueException"/> class.</summary>
    /// <param name="message">Description of the clash.</param>
    /// <param name="innerException">The underlying cause.</param>
    public DuplicateReferenceValueException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
