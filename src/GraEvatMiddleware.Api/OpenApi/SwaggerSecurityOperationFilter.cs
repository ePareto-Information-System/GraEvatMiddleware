using GraEvatMiddleware.Api.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace GraEvatMiddleware.Api.OpenApi;

public sealed class SwaggerSecurityOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var methodAttributes = context.MethodInfo.GetCustomAttributes(true);
        var controllerAttributes = context.MethodInfo.DeclaringType?.GetCustomAttributes(true) ?? [];
        var attributes = methodAttributes.Concat(controllerAttributes).ToArray();

        var requiresAdminKey = attributes.OfType<AdminKeyAttribute>().Any();
        var requiresTenantKey = attributes.OfType<AuthorizeAttribute>().Any();

        if (requiresAdminKey)
        {
            operation.Security ??= [];
            operation.Security.Add(new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("AdminKey", context.Document)] = []
            });
        }

        if (requiresTenantKey)
        {
            operation.Security ??= [];
            operation.Security.Add(new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("TenantApiKey", context.Document)] = []
            });
        }

        if (requiresAdminKey || requiresTenantKey)
        {
            operation.Responses ??= new OpenApiResponses();
            operation.Responses.TryAdd("401", new OpenApiResponse
            {
                Description = "Authentication failed."
            });
        }
    }
}
