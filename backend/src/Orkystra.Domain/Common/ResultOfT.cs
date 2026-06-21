namespace Orkystra.Domain.Common;

public sealed class Result<TValue> : Result
{
    private readonly TValue? _value;

    private Result(TValue value)
        : base(true, DomainError.None)
    {
        _value = value;
    }

    private Result(DomainError error)
        : base(false, error)
    {
    }

    public TValue Value =>
        IsSuccess
            ? _value!
            : throw new InvalidOperationException("A failed result does not contain a value.");

    public static Result<TValue> Success(TValue value) => new(value);

    public static new Result<TValue> Failure(DomainError error) => new(error);
}
