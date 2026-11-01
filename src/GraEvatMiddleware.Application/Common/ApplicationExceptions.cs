namespace GraEvatMiddleware.Application.Common;

public sealed class ResourceConflictException(string message) : Exception(message);
public sealed class ResourceNotFoundException(string message) : Exception(message);
public sealed class IdempotencyConflictException(string message) : Exception(message);
public sealed class ConfigurationMissingException(string message) : Exception(message);
