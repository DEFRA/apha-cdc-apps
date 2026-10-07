namespace CDC.Api.Domain.Exceptions;

/// <summary>
/// Thrown when a species cannot be added or renamed because another species already uses the
/// name. Raised when <c>spiSpecies</c> or <c>spuSpecies</c> reports a duplicate. Surfaces as
/// HTTP 409.
/// </summary>
public sealed class DuplicateSpeciesNameException : Exception
{
    /// <summary>Initialises a new instance of the <see cref="DuplicateSpeciesNameException"/> class.</summary>
    public DuplicateSpeciesNameException()
        : base("There is already a species with this name.")
    {
    }

    /// <summary>Initialises a new instance of the <see cref="DuplicateSpeciesNameException"/> class.</summary>
    /// <param name="message">Description of the clash.</param>
    public DuplicateSpeciesNameException(string message)
        : base(message)
    {
    }

    /// <summary>Initialises a new instance of the <see cref="DuplicateSpeciesNameException"/> class.</summary>
    /// <param name="message">Description of the clash.</param>
    /// <param name="innerException">The underlying cause.</param>
    public DuplicateSpeciesNameException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
