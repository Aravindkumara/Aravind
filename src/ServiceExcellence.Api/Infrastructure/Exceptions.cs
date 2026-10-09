namespace ServiceExcellence.Api.Infrastructure;

/// <summary>Base type for errors that map to a specific HTTP status and are safe to show to the user.</summary>
public abstract class AppException : Exception
{
    protected AppException(string message) : base(message) { }
    public abstract int StatusCode { get; }
}

public class NotFoundException : AppException
{
    public NotFoundException(string message) : base(message) { }
    public override int StatusCode => StatusCodes.Status404NotFound;
}

public class ConflictException : AppException
{
    public ConflictException(string message) : base(message) { }
    public override int StatusCode => StatusCodes.Status409Conflict;
}

public class ForbiddenException : AppException
{
    public ForbiddenException(string message) : base(message) { }
    public override int StatusCode => StatusCodes.Status403Forbidden;
}

/// <summary>A business-rule validation failure, optionally tied to a request field.</summary>
public class ValidationException : AppException
{
    public ValidationException(string message, string field = "") : base(message)
    {
        Errors = new Dictionary<string, string[]> { [field] = new[] { message } };
    }

    public IDictionary<string, string[]> Errors { get; }
    public override int StatusCode => StatusCodes.Status400BadRequest;
}
