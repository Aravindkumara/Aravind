using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace ServiceExcellence.Api.Infrastructure;

/// <summary>Turns exceptions into RFC 7807 problem responses and logs unexpected ones.</summary>
public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;
    private readonly IProblemDetailsService _problemDetails;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IProblemDetailsService problemDetails)
    {
        _logger = logger;
        _problemDetails = problemDetails;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        ProblemDetails problem;
        switch (exception)
        {
            case ValidationException validation:
                problem = new ValidationProblemDetails(validation.Errors)
                {
                    Status = validation.StatusCode,
                    Title = "Validation failed",
                    Detail = validation.Message
                };
                break;

            case AppException app:
                problem = new ProblemDetails { Status = app.StatusCode, Title = app.Message, Detail = app.Message };
                break;

            // A unique or foreign-key constraint hit by a race the service checks did not catch.
            case PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg:
                _logger.LogWarning(pg, "Unique constraint violation");
                problem = new ProblemDetails { Status = StatusCodes.Status409Conflict, Title = "A record with the same key already exists." };
                break;

            case PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation } pg:
                _logger.LogWarning(pg, "Foreign key violation");
                problem = new ProblemDetails { Status = StatusCodes.Status409Conflict, Title = "The record is referenced by other data or refers to missing data." };
                break;

            default:
                _logger.LogError(exception, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);
                problem = new ProblemDetails
                {
                    Status = StatusCodes.Status500InternalServerError,
                    Title = "An unexpected error occurred. Please try again or contact support."
                };
                break;
        }

        context.Response.StatusCode = problem.Status!.Value;
        return await _problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = problem,
            Exception = exception
        });
    }
}
