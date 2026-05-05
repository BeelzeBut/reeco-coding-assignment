namespace OrderOps.Api.Infrastructure;

public abstract class DomainException : Exception
{
    protected DomainException(string message, string code) : base(message) { Code = code; }
    public string Code { get; }
    public abstract int StatusCode { get; }
}

public sealed class NotFoundException : DomainException
{
    public NotFoundException(string what) : base($"{what} not found", "not_found") { }
    public override int StatusCode => 404;
}

public sealed class ValidationException : DomainException
{
    public ValidationException(string message, string code = "validation_error") : base(message, code) { }
    public override int StatusCode => 400;
}

public abstract class ConflictException : DomainException
{
    protected ConflictException(string message, string code) : base(message, code) { }
    public override int StatusCode => 409;
}

public sealed class OrderAlreadyCancelledException : ConflictException
{
    public OrderAlreadyCancelledException() : base("Order is already cancelled", "already_cancelled") { }
}

public sealed class OptimisticConcurrencyException : ConflictException
{
    public OptimisticConcurrencyException() : base("Order was modified concurrently", "version_conflict") { }
}
