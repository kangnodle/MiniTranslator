namespace MiniTranslator.Domain.Common;

public readonly record struct Unit
{
    public static Unit Value => default;
}

public sealed class Result<T>
{
    private readonly T? _value;

    private Result(T value)
    {
        _value = value;
        IsSuccess = true;
        Error = Error.None;
    }

    private Result(Error error)
    {
        _value = default;
        IsSuccess = false;
        Error = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public Error Error { get; }

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException($"Cannot access the value of a failed result ({Error}).");

    public static Result<T> Success(T value) => new(value);

    public static Result<T> Failure(Error error) => new(error);

    public static implicit operator Result<T>(T value) => Success(value);

    public static implicit operator Result<T>(Error error) => Failure(error);

    public Result<TOut> Map<TOut>(Func<T, TOut> map) =>
        IsSuccess ? Result<TOut>.Success(map(Value)) : Result<TOut>.Failure(Error);

    public Result<TOut> Bind<TOut>(Func<T, Result<TOut>> bind) =>
        IsSuccess ? bind(Value) : Result<TOut>.Failure(Error);

    public TOut Match<TOut>(Func<T, TOut> onSuccess, Func<Error, TOut> onFailure) =>
        IsSuccess ? onSuccess(Value) : onFailure(Error);

    public T GetValueOrDefault(T fallback) => IsSuccess ? Value : fallback;

    public override string ToString() => IsSuccess ? $"Success({Value})" : $"Failure({Error})";
}

public static class Result
{
    public static Result<Unit> Success() => Result<Unit>.Success(Unit.Value);

    public static Result<T> Success<T>(T value) => Result<T>.Success(value);

    public static Result<Unit> Failure(Error error) => Result<Unit>.Failure(error);

    public static Result<Unit> ToUnit<T>(this Result<T> result) => result.Map(_ => Unit.Value);
}
