using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SGV.Api.Infrastructure.Health;

/// <summary>
/// Shared JSON response writer for health check endpoints.
/// Serializes a sanitized DTO without <c>Exception</c> or stack trace fields
/// to avoid information leaks. Used by both <c>SGV.Api</c> and <c>SGV.Web</c>
/// (the latter via a project file link).
/// </summary>
public static class HealthCheckResponseWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    public static async Task WriteJson(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";

        var response = new HealthReportDto
        {
            Status = report.Status.ToString(),
            TotalDurationMs = report.TotalDuration.TotalMilliseconds,
            Entries = report.Entries.Select(e => new HealthEntryDto
            {
                Name = e.Key,
                Status = e.Value.Status.ToString(),
                Description = e.Value.Description ?? string.Empty,
                DurationMs = e.Value.Duration.TotalMilliseconds
            }).ToList()
        };

        await JsonSerializer.SerializeAsync(context.Response.Body, response, JsonOptions);
    }

    internal sealed record HealthReportDto
    {
        public string Status { get; init; } = string.Empty;
        public double TotalDurationMs { get; init; }
        public List<HealthEntryDto>? Entries { get; init; }
    }

    internal sealed record HealthEntryDto
    {
        public string Name { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public double DurationMs { get; init; }
    }
}
