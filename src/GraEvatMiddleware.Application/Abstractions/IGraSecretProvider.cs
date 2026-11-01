namespace GraEvatMiddleware.Application.Abstractions;

public interface IGraSecretProvider
{
    Task<string?> GetSecretAsync(string secretName, CancellationToken cancellationToken = default);
}
