namespace WhatsOrder.Application.Common;

/// <summary>Maps to HTTP 404.</summary>
public class NotFoundException(string message) : Exception(message);

/// <summary>Maps to HTTP 403.</summary>
public class ForbiddenException(string message) : Exception(message);

/// <summary>Maps to HTTP 409 (e.g. slug already taken).</summary>
public class ConflictException(string message) : Exception(message);

/// <summary>Maps to HTTP 400 with a stable machine-readable code for the frontend.</summary>
public class BusinessRuleException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

/// <summary>Maps to HTTP 401 (bad credentials / invalid refresh token).</summary>
public class AuthFailedException(string message) : Exception(message);
