namespace CDC.Api.Domain.Exceptions;

/// <summary>
/// Thrown when <c>spiProfileContributor</c> rejects an insert because the username is already in
/// use by another global user (a race between the username lookup and the save). Surfaces as
/// HTTP 400.
/// </summary>
public sealed class DuplicateUsernameException : Exception
{
    /// <summary>Initialises a new instance of the <see cref="DuplicateUsernameException"/> class.</summary>
    public DuplicateUsernameException()
        : base("There is already a user with the specified username.")
    {
    }

    /// <summary>Initialises a new instance of the <see cref="DuplicateUsernameException"/> class.</summary>
    /// <param name="message">Description of the conflict.</param>
    public DuplicateUsernameException(string message)
        : base(message)
    {
    }

    /// <summary>Initialises a new instance of the <see cref="DuplicateUsernameException"/> class.</summary>
    /// <param name="message">Description of the conflict.</param>
    /// <param name="innerException">The underlying cause.</param>
    public DuplicateUsernameException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
