using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http.Json;
using OutlookMailAdviser.Adapters.MailContent;
using OutlookMailAdviser.Adapters.Ollama;
using OutlookMailAdviser.Adapters.OpenAI;
using OutlookMailAdviser.Api.Features.MailAnalysis;
using OutlookMailAdviser.Api.Features.MailDrafting;
using OutlookMailAdviser.Api.Infrastructure;
using OutlookMailAdviser.Application.MailAnalysis;
using OutlookMailAdviser.Application.MailDrafting;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(options =>
{
    options.TimestampFormat = "HH:mm:ss ";
    options.SingleLine = true;
});
builder.Logging.AddDebug();
// The middleware otherwise logs the original exception (including inner details)
// before our redacted handler runs. The handler emits safe code/trace-id events.
builder.Logging.AddFilter("Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware", LogLevel.None);

builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Extensions["traceId"] =
            Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;
    };
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();
builder.Services.Configure<JsonOptions>(options =>
{
    options.SerializerOptions.Converters.Add(
        new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
});
builder.Services.AddScoped<IAnalyzeMailUseCase, AnalyzeMailHandler>();
builder.Services.AddScoped<IDraftMailUseCase, DraftMailHandler>();
builder.Services.AddMailContentAdapter(builder.Configuration);

var aiProvider = builder.Configuration["Ai:Provider"]?.Trim();
if (string.Equals(aiProvider, "Ollama", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddOllamaAdapter(builder.Configuration);
}
else if (string.Equals(aiProvider, "OpenAI", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddOpenAiAdapter(builder.Configuration);
}
else
{
    throw new InvalidOperationException(
        "Ai:Provider must be either 'Ollama' or 'OpenAI'.");
}

var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? [];

builder.Services.AddCors(options =>
{
    options.AddPolicy("OutlookAddIn", policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            policy
                .WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod();
        }
    });
});

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("OutlookAddIn");
app.UseStaticFiles();

app.MapGet("/", () => Results.Ok(new
{
    service = "Outlook Mail Adviser API",
    version = "v1"
}))
    .WithName("GetServiceInfo");

app.MapGet("/test", () => Results.Redirect("/test.html"))
    .ExcludeFromDescription();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready")
});

app.MapMailAnalysisEndpoints();
app.MapMailDraftingEndpoints();

app.Run();

public partial class Program;
