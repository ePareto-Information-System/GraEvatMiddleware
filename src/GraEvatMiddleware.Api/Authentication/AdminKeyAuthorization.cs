using System.Security.Cryptography;
using System.Text;
using GraEvatMiddleware.Infrastructure.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace GraEvatMiddleware.Api.Authentication;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class AdminKeyAttribute() : TypeFilterAttribute(typeof(AdminKeyAuthorizationFilter));

public sealed class AdminKeyAuthorizationFilter(IOptions<SecurityOptions> options) : IAsyncAuthorizationFilter
{
    public Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var configuredKey = options.Value.AdminKey;
        if (string.IsNullOrWhiteSpace(configuredKey))
        {
            context.Result = new ObjectResult(new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "Administrative authentication is not configured."
            })
            {
                StatusCode = StatusCodes.Status503ServiceUnavailable
            };
            return Task.CompletedTask;
        }

        var suppliedKey = context.HttpContext.Request.Headers["X-Admin-Key"].ToString();
        if (!FixedTimeEquals(suppliedKey, configuredKey))
        {
            context.Result = new UnauthorizedObjectResult(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Administrative authentication failed."
            });
        }

        return Task.CompletedTask;
    }

    private static bool FixedTimeEquals(string supplied, string configured)
    {
        var suppliedBytes = SHA256.HashData(Encoding.UTF8.GetBytes(supplied));
        var configuredBytes = SHA256.HashData(Encoding.UTF8.GetBytes(configured));
        return CryptographicOperations.FixedTimeEquals(suppliedBytes, configuredBytes);
    }
}
