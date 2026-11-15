namespace GraEvatMiddleware.Infrastructure.Authentication;

public sealed class SecurityOptions
{
    public const string SectionName = "Security";

    public string AdminKey { get; init; } = string.Empty;

    public string ApiKeyPepper { get; init; } = string.Empty;
}
