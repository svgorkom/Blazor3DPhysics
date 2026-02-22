namespace BlazorClient.Domain.Common;

/// <summary>
/// Represents the result of an operation that can succeed or fail.
/// Provides a functional approach to error handling without exceptions.
/// </summary>
/// <typeparam name="T">The type of the value on success.</typeparam>
public readonly struct Result<T>
{
    /// <summary>
    /// The value if the operation succeeded.
    /// </summary>
    public T? Value { get; }

    /// <summary>
    /// The error message if the operation failed.
    /// </summary>
    public string? Error { get; }

    /// <summary>
    /// Whether the operation succeeded.
    /// </summary>
    public bool IsSuccess => Error == null;

    /// <summary>
    /// Whether the operation failed.
    /// </summary>
    public bool IsFailure => !IsSuccess;

    private Result(T? value, string? error)
    {
        Value = value;
        Error = error;
    }

    /// <summary>
    /// Creates a successful result with the given value.
    /// </summary>
    public static Result<T> Success(T value) => new(value, null);

    /// <summary>
    /// Creates a failed result with the given error message.
    /// </summary>
    public static Result<T> Failure(string error) => new(default, error);

    /// <summary>
    /// Pattern matches on the result, calling the appropriate function.
    /// </summary>
    public TResult Match<TResult>(Func<T, TResult> onSuccess, Func<string, TResult> onFailure)
        => IsSuccess ? onSuccess(Value!) : onFailure(Error!);

    /// <summary>
    /// Executes an action if the result is successful.
    /// </summary>
    public Result<T> OnSuccess(Action<T> action)
    {
        if (IsSuccess) action(Value!);
        return this;
    }

    /// <summary>
    /// Executes an action if the result is a failure.
    /// </summary>
    public Result<T> OnFailure(Action<string> action)
    {
        if (IsFailure) action(Error!);
        return this;
    }

    /// <summary>
    /// Maps the value to a new type if successful.
    /// </summary>
    public Result<TNew> Map<TNew>(Func<T, TNew> mapper)
        => IsSuccess ? Result<TNew>.Success(mapper(Value!)) : Result<TNew>.Failure(Error!);

    /// <summary>
    /// Implicit conversion to bool for easy checking.
    /// </summary>
    public static implicit operator bool(Result<T> result) => result.IsSuccess;
}

/// <summary>
/// Represents the result of an operation without a return value.
/// </summary>
public readonly struct Result
{
    /// <summary>
    /// The error message if the operation failed.
    /// </summary>
    public string? Error { get; }

    /// <summary>
    /// Whether the operation succeeded.
    /// </summary>
    public bool IsSuccess => Error == null;

    /// <summary>
    /// Whether the operation failed.
    /// </summary>
    public bool IsFailure => !IsSuccess;

    private Result(string? error) => Error = error;

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static Result Success() => new(null);

    /// <summary>
    /// Creates a failed result with the given error message.
    /// </summary>
    public static Result Failure(string error) => new(error);

    /// <summary>
    /// Pattern matches on the result, calling the appropriate function.
    /// </summary>
    public TResult Match<TResult>(Func<TResult> onSuccess, Func<string, TResult> onFailure)
        => IsSuccess ? onSuccess() : onFailure(Error!);

    /// <summary>
    /// Executes an action if the result is successful.
    /// </summary>
    public Result OnSuccess(Action action)
    {
        if (IsSuccess) action();
        return this;
    }

    /// <summary>
    /// Executes an action if the result is a failure.
    /// </summary>
    public Result OnFailure(Action<string> action)
    {
        if (IsFailure) action(Error!);
        return this;
    }

    /// <summary>
    /// Implicit conversion to bool for easy checking.
    /// </summary>
    public static implicit operator bool(Result result) => result.IsSuccess;
}
