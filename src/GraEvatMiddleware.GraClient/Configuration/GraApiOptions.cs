namespace GraEvatMiddleware.GraClient.Configuration;

public sealed class GraApiOptions
{
    public const string SectionName = "GraApi";

    public string BaseUrl { get; init; } = string.Empty;

    public int RequestTimeoutSeconds { get; init; } = 30;

    public bool Enabled { get; init; }
}
