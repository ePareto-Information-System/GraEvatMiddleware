namespace GraEvatMiddleware.Application.Logging;

public sealed class ExchangeLoggingOptions
{
    public const string SectionName = "ExchangeLogging";
    public bool Enabled { get; set; } = true;
    public bool CaptureBodies { get; set; } = true;
    public int MaximumBodyCharacters { get; set; } = 65_536;
    public int RetentionDays { get; set; } = 90;
}
