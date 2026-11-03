using GraEvatMiddleware.Api.Authentication;
using GraEvatMiddleware.Api.Middleware;
using GraEvatMiddleware.Api.OpenApi;
using GraEvatMiddleware.GraClient;
using GraEvatMiddleware.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi;

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
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "GRA eVAT Middleware API",
        Version = "v1",
        Description = "Canonical multi-tenant API for ERP and Python clients."
    });
    options.AddSecurityDefinition("AdminKey", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Name = "X-Admin-Key",
        Description = "Administrative key used only for tenant and credential management."
    });
    options.AddSecurityDefinition("TenantApiKey", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Name = "X-API-Key",
        Description = "Tenant API key issued when a tenant or credential is created."
    });
    options.OperationFilter<SwaggerSecurityOperationFilter>();
});
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

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.RoutePrefix = "swagger";
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "GRA eVAT Middleware API v1");
        options.DocumentTitle = "GRA eVAT Middleware API";
        options.DisplayRequestDuration();
    });
}

app.UseHttpsRedirection();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ExchangeLoggingMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program;
