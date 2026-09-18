namespace CDC.Api.Domain.Common;

/// <summary>
/// Outcome classification for a <see cref="Result{T}"/>, so handlers can report expected
/// failures (a missing record, a concurrent edit) without throwing.
/// </summary>
public enum ResultStatus
{
    /// <summary>The operation completed and a value is available.</summary>
    Success = 0,

    /// <summary>The requested record does not exist.</summary>
    NotFound = 1,

    /// <summary>The operation clashed with a concurrent change and was abandoned.</summary>
    Conflict = 2
}

/// <summary>
/// Result of an application operation. Expected failures are modelled as values rather than
/// exceptions; only unexpected faults propagate to <c>GlobalExceptionMiddleware</c>.
/// </summary>
/// <typeparam name="T">Type of the value produced on success.</typeparam>
public sealed class Result<T>
{
    private readonly T? value;

    internal Result(ResultStatus status, T? value, string? error)
    {
        Status = status;
        this.value = value;
        Error = error;
    }

    /// <summary>Gets the outcome classification.</summary>
    public ResultStatus Status { get; }

    /// <summary>Gets a value indicating whether the operation succeeded.</summary>
    public bool IsSuccess => Status == ResultStatus.Success;

    /// <summary>Gets the failure description, or <see langword="null"/> when successful.</summary>
    public string? Error { get; }

    /// <summary>Gets the produced value.</summary>
    /// <exception cref="InvalidOperationException">Thrown when the result is not successful.</exception>
    public T Value => IsSuccess && value is not null
        ? value
        : throw new InvalidOperationException($"No value is available for a {Status} result.");
}

/// <summary>
/// Factory methods for <see cref="Result{T}"/>.
/// </summary>
public static class Result
{
    /// <summary>Creates a successful result.</summary>
    /// <typeparam name="T">Type of the value produced.</typeparam>
    /// <param name="value">The value produced by the operation.</param>
    /// <returns>A successful <see cref="Result{T}"/>.</returns>
    public static Result<T> Success<T>(T value) => new(ResultStatus.Success, value, null);

    /// <summary>Creates a "record does not exist" result.</summary>
    /// <typeparam name="T">Type of the value the operation would have produced.</typeparam>
    /// <param name="error">Description of what could not be found.</param>
    /// <returns>A not-found <see cref="Result{T}"/>.</returns>
    public static Result<T> NotFound<T>(string error) => new(ResultStatus.NotFound, default, error);

    /// <summary>Creates a "concurrent change" result.</summary>
    /// <typeparam name="T">Type of the value the operation would have produced.</typeparam>
    /// <param name="error">Description of the conflict.</param>
    /// <returns>A conflict <see cref="Result{T}"/>.</returns>
    public static Result<T> Conflict<T>(string error) => new(ResultStatus.Conflict, default, error);
}
