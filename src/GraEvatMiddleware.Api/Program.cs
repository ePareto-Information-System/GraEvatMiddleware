using GraEvatMiddleware.Api.Authentication;
using GraEvatMiddleware.Api.Middleware;
using GraEvatMiddleware.GraClient;
using GraEvatMiddleware.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

builder.Services
    .AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
            new UnprocessableEntityObjectResult(new ValidationProblemDetails(context.ModelState));
    });
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddGraClient(builder.Configuration);
builder.Services.AddScoped<AdminKeyAuthorizationFilter>();
builder.Services
    .AddAuthentication(TenantApiKeyAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, TenantApiKeyAuthenticationHandler>(
        TenantApiKeyAuthenticationHandler.SchemeName,
        _ => { });
builder.Services.AddAuthorization();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program;
