using System.Net;

namespace PedeLocal.Application.Common.Exceptions;

/// <summary>Base for all expected, user-facing failures. Maps directly to the API's standard error envelope.</summary>
public class AppException : Exception
{
    public string Code { get; }
    public HttpStatusCode StatusCode { get; }

    public AppException(string code, string message, HttpStatusCode statusCode = HttpStatusCode.BadRequest)
        : base(message)
    {
        Code = code;
        StatusCode = statusCode;
    }
}

public class NotFoundException : AppException
{
    public NotFoundException(string code, string message) : base(code, message, HttpStatusCode.NotFound)
    {
    }
}

public class ConflictException : AppException
{
    public ConflictException(string code, string message) : base(code, message, HttpStatusCode.Conflict)
    {
    }
}

public class ForbiddenException : AppException
{
    public ForbiddenException(string code, string message) : base(code, message, HttpStatusCode.Forbidden)
    {
    }
}

public class UnauthorizedAppException : AppException
{
    public UnauthorizedAppException(string code, string message) : base(code, message, HttpStatusCode.Unauthorized)
    {
    }
}

public class ValidationAppException : AppException
{
    public IDictionary<string, string[]> Errors { get; }

    public ValidationAppException(IDictionary<string, string[]> errors)
        : base("VALIDATION_ERROR", "Um ou mais campos são inválidos.", HttpStatusCode.UnprocessableEntity)
    {
        Errors = errors;
    }
}
