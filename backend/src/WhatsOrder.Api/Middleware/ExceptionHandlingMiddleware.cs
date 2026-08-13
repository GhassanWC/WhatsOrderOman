using Microsoft.AspNetCore.Mvc;
using WhatsOrder.Application.Common;

namespace WhatsOrder.Api.Middleware;

/// <summary>Maps application exceptions to RFC 7807 ProblemDetails responses.</summary>
public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            if (context.Response.HasStarted)
                throw;

            var (status, title, code) = ex switch
            {
                NotFoundException => (StatusCodes.Status404NotFound, ex.Message, "not_found"),
                ForbiddenException => (StatusCodes.Status403Forbidden, ex.Message, "forbidden"),
                ConflictException => (StatusCodes.Status409Conflict, ex.Message, "conflict"),
                AuthFailedException => (StatusCodes.Status401Unauthorized, ex.Message, "auth_failed"),
                BusinessRuleException businessRule => (StatusCodes.Status400BadRequest, ex.Message, businessRule.Code),
                FluentValidation.ValidationException => (StatusCodes.Status400BadRequest, ex.Message, "validation"),
                OperationCanceledException when context.RequestAborted.IsCancellationRequested =>
                    (StatusCodes.Status499ClientClosedRequest, "Request cancelled.", "cancelled"),
                _ => (StatusCodes.Status500InternalServerError, "Something went wrong. Please try again.", "server_error")
            };

            if (status == StatusCodes.Status500InternalServerError)
                logger.LogError(ex, "Unhandled exception on {Method} {Path}", context.Request.Method, context.Request.Path);
            else if (status == StatusCodes.Status499ClientClosedRequest)
                return;

            context.Response.StatusCode = status;
            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = status,
                Title = title,
                Extensions = { ["code"] = code }
            });
        }
    }
}
