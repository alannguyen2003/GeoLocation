using LocationTracker.Business.Validation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace LocationTracker.Api.Errors;

/// <summary>Turns exceptions into RFC 7807 problem details. Stack traces are only shown in Development.</summary>
public class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    IHostEnvironment environment,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        ProblemDetails problem;

        switch (exception)
        {
            case BusinessValidationException validation:
                problem = new ValidationProblemDetails(validation.Errors)
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "One or more validation errors occurred.",
                };
                break;

            case BadHttpRequestException badRequest:
                problem = new ProblemDetails { Status = badRequest.StatusCode, Title = "Bad request." };
                break;

            case OperationCanceledException when context.RequestAborted.IsCancellationRequested:
                // The client went away; there is nobody to answer.
                return true;

            default:
                logger.LogError(exception, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);
                problem = new ProblemDetails
                {
                    Status = StatusCodes.Status500InternalServerError,
                    Title = "An unexpected error occurred.",
                    Detail = environment.IsDevelopment() ? exception.ToString() : null,
                };
                break;
        }

        context.Response.StatusCode = problem.Status!.Value;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = problem,
            Exception = exception,
        });
    }
}
