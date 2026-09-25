using ContainerControl.Host.Hosting;
using ContainerControl.Modules.Access.Application.SignIn;
using ContainerControl.Modules.Access.Infrastructure.DependencyInjection;
using ContainerControl.Modules.Access.Infrastructure.Http;
using ContainerControl.Modules.Access.Infrastructure.Persistence;
using ContainerControl.Modules.Applications;
using ContainerControl.Modules.Applications.Http;
using ContainerControl.Modules.Delivery;
using ContainerControl.Modules.Delivery.Http;
using ContainerControl.Modules.Edge;
using ContainerControl.Modules.Edge.Http;
using ContainerControl.Modules.Platform;
using ContainerControl.Modules.Platform.Http;
using ContainerControl.Modules.Registries;
using ContainerControl.Modules.Registries.Connections;
using ContainerControl.Modules.Registries.Http;
using ContainerControl.Modules.Runtime;
using ContainerControl.Modules.Runtime.Http;
using ContainerControl.SharedKernel.Correlation;
using ContainerControl.SharedKernel.DependencyInjection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
BootstrapAdminConfiguration.MapEnvironmentVariables(builder.Configuration);

builder.Host.UseSerilog((context, _, logger) =>
{
    logger
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "ContainerControl")
        .WriteTo.Console();
});

var secureCookies = !builder.Environment.IsDevelopment() && !builder.Environment.IsEnvironment("Testing");

builder.Services.AddSharedKernel();
builder.Services.AddSingleton<ICorrelationContext, HttpCorrelationContext>();
builder.Services.AddAccessModule(builder.Configuration, secureCookies);
builder.Services.AddPlatformModule(builder.Configuration);
builder.Services.AddRegistriesModule(builder.Configuration);
builder.Services.AddApplicationsModule(builder.Configuration);
builder.Services.AddScoped<ContainerControl.Modules.Platform.Engine.IEngineSecretReader, InfisicalEngineSecretReader>();
builder.Services.AddDeliveryModule(builder.Configuration);
builder.Services.AddEdgeModule(builder.Configuration);
builder.Services.AddRuntimeModule(builder.Configuration);
builder.Services.AddHostedService<DatabaseInitializer>();
builder.Services.AddHostedService<EcrTokenRefresh>();
builder.Services.AddExceptionHandler<UnhandledExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

builder.Services.AddHealthChecks()
    .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(), tags: ["live"])
    .AddDbContextCheck<AccessDbContext>("database", tags: ["ready"]);

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:4200"];
builder.Services.AddCors(options =>
{
    options.AddPolicy("spa", policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()
            .WithExposedHeaders("X-XSRF-TOKEN", "X-Correlation-ID");
    });
});

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("ContainerControl"))
    .WithTracing(tracing => tracing
        .AddSource(AccessTelemetry.SourceName)
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter())
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddOtlpExporter());

var app = builder.Build();

app.UseCorrelationId();
app.UseExceptionHandler();
app.UseSerilogRequestLogging(options =>
{
    options.EnrichDiagnosticContext = (diagnostic, http) =>
    {
        if (http.Items.TryGetValue(Correlation.ItemKey, out var correlationId))
        {
            diagnostic.Set("CorrelationId", correlationId);
        }
    };
});
app.UseCors("spa");
app.UseAuthentication();
app.UseAuthorization();
app.UseApiAntiforgery();
app.UseDisabledUserGate();

if (!app.Environment.IsProduction())
{
    app.MapOpenApi();
}

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live"),
    ResponseWriter = HealthResponseWriter.Write
}).AllowAnonymous();

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = HealthResponseWriter.Write
}).AllowAnonymous();

app.MapAccessEndpoints();
app.MapPlatformEndpoints();
app.MapApplicationEndpoints();
app.MapTemplateEndpoints();
app.MapEdgeEndpoints();
app.MapDeliveryEndpoints();
app.MapRuntimeEndpoints();
app.MapRegistryEndpoints();

app.Run();

public partial class Program;
