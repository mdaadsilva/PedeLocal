using System.Net;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using PedeLocal.Application.Common.Exceptions;

namespace PedeLocal.Api.Errors;

public record ErrorResponse(string Code, string Message, string TraceId, IDictionary<string, string[]>? Errors = null);

public class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var traceId = httpContext.TraceIdentifier;

        var (statusCode, response) = exception switch
        {
            ValidationAppException vex => ((int)vex.StatusCode, new ErrorResponse(vex.Code, vex.Message, traceId, vex.Errors)),
            ValidationException fvex => ((int)HttpStatusCode.UnprocessableEntity, new ErrorResponse(
                "VALIDATION_ERROR", "Um ou mais campos são inválidos.", traceId, ToErrorDictionary(fvex))),
            AppException aex => ((int)aex.StatusCode, new ErrorResponse(aex.Code, aex.Message, traceId)),
            _ => ((int)HttpStatusCode.InternalServerError,
                new ErrorResponse("INTERNAL_ERROR", "Ocorreu um erro inesperado.", traceId))
        };

        if (statusCode == (int)HttpStatusCode.InternalServerError)
        {
            logger.LogError(exception, "Unhandled exception. TraceId={TraceId}", traceId);
        }
        else
        {
            logger.LogWarning(exception, "Handled application exception {Code}. TraceId={TraceId}", response.Code, traceId);
        }

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(response, cancellationToken);
        return true;
    }

    private static IDictionary<string, string[]> ToErrorDictionary(ValidationException exception) =>
        exception.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
}
