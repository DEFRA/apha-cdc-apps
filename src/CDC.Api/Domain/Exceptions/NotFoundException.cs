namespace CDC.Api.Domain.Exceptions;

/// <summary>
/// Thrown when a requested record does not exist. Surfaces as HTTP 404 with an RFC 7807 body.
/// </summary>
public sealed class NotFoundException : Exception
{
    /// <summary>Initialises a new instance of the <see cref="NotFoundException"/> class.</summary>
    public NotFoundException()
        : base("The requested resource was not found.")
    {
    }

    /// <summary>Initialises a new instance of the <see cref="NotFoundException"/> class.</summary>
    /// <param name="message">Description of what could not be found.</param>
    public NotFoundException(string message)
        : base(message)
    {
    }

    /// <summary>Initialises a new instance of the <see cref="NotFoundException"/> class.</summary>
    /// <param name="message">Description of what could not be found.</param>
    /// <param name="innerException">The underlying cause.</param>
    public NotFoundException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Creates an exception describing a missing record.</summary>
    /// <param name="entityName">The entity type, for example <c>Species</c>.</param>
    /// <param name="key">The identifier that was looked up.</param>
    /// <returns>A configured <see cref="NotFoundException"/>.</returns>
    public static NotFoundException For(string entityName, object key) =>
        new($"{entityName} '{key}' was not found.");
}
