namespace CDC.Api.Domain.Exceptions;

/// <summary>
/// Thrown when <c>spuProfile</c> refuses a title: another profile already uses it, or it does not
/// apply to this kind of profile (a "what-if" scenario has no profile title, nor does a current
/// situation profile have a scenario title). The message is the procedure's own, which the legacy
/// page showed the user as it stood. Surfaces as HTTP 400.
/// </summary>
public sealed class ProfileTitleRefusedException : Exception
{
    /// <summary>Initialises a new instance of the <see cref="ProfileTitleRefusedException"/> class.</summary>
    public ProfileTitleRefusedException()
        : base("The profile title cannot be used.")
    {
    }

    /// <summary>Initialises a new instance of the <see cref="ProfileTitleRefusedException"/> class.</summary>
    /// <param name="message">Why the title was refused.</param>
    public ProfileTitleRefusedException(string message)
        : base(message)
    {
    }

    /// <summary>Initialises a new instance of the <see cref="ProfileTitleRefusedException"/> class.</summary>
    /// <param name="message">Why the title was refused.</param>
    /// <param name="innerException">The underlying cause.</param>
    public ProfileTitleRefusedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
