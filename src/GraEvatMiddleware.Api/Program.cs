using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;
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
builder.Logging.AddJsonConsole(options =>
{
    options.IncludeScopes = true;
    options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
    options.UseUtcTimestamp = true;
});
builder.Logging.AddDebug();

builder.Services
    .AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var problem = new ValidationProblemDetails(context.ModelState)
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = "Request validation failed.",
                Instance = context.HttpContext.Request.Path
            };
            problem.Extensions["code"] = "VALIDATION_FAILED";
            problem.Extensions["correlationId"] = context.HttpContext.TraceIdentifier;
            return new UnprocessableEntityObjectResult(problem);
        };
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
builder.Services.Configure<RateLimitingOptions>(
    builder.Configuration.GetSection(RateLimitingOptions.SectionName));
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var rateLimiting = context.RequestServices
            .GetRequiredService<Microsoft.Extensions.Options.IOptions<RateLimitingOptions>>()
            .Value;
        if (!rateLimiting.Enabled)
        {
            return RateLimitPartition.GetNoLimiter("disabled");
        }

        var suppliedCredential = context.Request.Headers["X-API-Key"].FirstOrDefault()
            ?? context.Request.Headers["X-Admin-Key"].FirstOrDefault();
        var source = suppliedCredential ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var partition = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source)));
        return RateLimitPartition.GetFixedWindowLimiter(partition, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = Math.Max(1, rateLimiting.PermitLimit),
            Window = TimeSpan.FromSeconds(Math.Max(1, rateLimiting.WindowSeconds)),
            QueueLimit = Math.Max(0, rateLimiting.QueueLimit),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            AutoReplenishment = true
        });
    });
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.ContentType = "application/problem+json";
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "Request rate limit exceeded.",
            Instance = context.HttpContext.Request.Path,
            Extensions =
            {
                ["code"] = "RATE_LIMIT_EXCEEDED",
                ["correlationId"] = context.HttpContext.TraceIdentifier
            }
        };
        await context.HttpContext.Response.WriteAsync(
            JsonSerializer.Serialize(problem),
            cancellationToken);
    };
});

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
app.UseMiddleware<RequestTelemetryMiddleware>();
app.UseMiddleware<ExchangeLoggingMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program;
