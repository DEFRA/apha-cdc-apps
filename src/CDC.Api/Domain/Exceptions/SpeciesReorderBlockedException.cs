namespace CDC.Api.Domain.Exceptions;

/// <summary>
/// Thrown when <c>spuSpeciesSequenceNumber</c> cannot move a species up or down because no
/// sibling exists at the resulting sequence number - either it is already first/last, or an
/// earlier data gap means the immediately adjacent sequence number is missing. Surfaces as
/// HTTP 400, matching how the legacy CSLA business exception was shown to the user inline
/// rather than as a crash.
/// </summary>
public sealed class SpeciesReorderBlockedException : Exception
{
    /// <summary>Initialises a new instance of the <see cref="SpeciesReorderBlockedException"/> class.</summary>
    public SpeciesReorderBlockedException()
        : base("This species cannot be moved in that direction.")
    {
    }

    /// <summary>Initialises a new instance of the <see cref="SpeciesReorderBlockedException"/> class.</summary>
    /// <param name="message">Description of why the move was rejected.</param>
    public SpeciesReorderBlockedException(string message)
        : base(message)
    {
    }

    /// <summary>Initialises a new instance of the <see cref="SpeciesReorderBlockedException"/> class.</summary>
    /// <param name="message">Description of why the move was rejected.</param>
    /// <param name="innerException">The underlying cause.</param>
    public SpeciesReorderBlockedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
