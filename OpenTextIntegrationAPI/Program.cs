using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using OpenTextIntegrationAPI.ClassObjects;
using OpenTextIntegrationAPI.Models;
using OpenTextIntegrationAPI.Models.Filter;
using OpenTextIntegrationAPI.Services;
using OpenTextIntegrationAPI.Utilities;
using Swashbuckle.AspNetCore.Filters;
using Swashbuckle.AspNetCore.SwaggerGen;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

//
// 1) MVC + JSON + suppress automatic 400 so your action always runs
//
builder.Services
    .AddControllers()
    .ConfigureApiBehaviorOptions(opts =>
    {
        opts.SuppressModelStateInvalidFilter = true;
    })
    .AddJsonOptions(opts =>
    {
        opts.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        opts.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

//
// 2) Increase multipart and body‐size limits
//
builder.Services.Configure<FormOptions>(opts =>
{
    opts.ValueLengthLimit = int.MaxValue;
    opts.MultipartHeadersLengthLimit = int.MaxValue;
    opts.MultipartBodyLengthLimit = long.MaxValue;
});
builder.WebHost.ConfigureKestrel(opts =>
{
    opts.Limits.MaxRequestBodySize = long.MaxValue;
});
builder.Services.Configure<IISServerOptions>(opts =>
{
    opts.MaxRequestBodySize = long.MaxValue;
});

//
// 3) Your application services, HTTP clients, logging, etc.
//
builder.Services.Configure<OpenTextSettings>(builder.Configuration.GetSection("OpenText"));
builder.Services.Configure<FileLoggerOptions>(builder.Configuration.GetSection("FileLogger"));

builder.Services.AddHttpClient<AuthService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<AuthManager>();
builder.Services.AddScoped<CSUtilities>();
builder.Services.AddScoped<CRBusinessWorkspace>();
builder.Services.AddScoped<MasterData>();
builder.Services.AddScoped<Node>();
builder.Services.AddSingleton<ILogService, FileLoggerService>();

//
// 4) Swagger/OpenAPI + examples + operation filter
//
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(opts =>
{
    opts.EnableAnnotations();
    opts.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "OpenText Integration REST API",
        Version = "v1",
        Description = "Florida Crystals - REST API to Integrate with third party applications",
        Contact = new OpenApiContact
        {
            Name = "Rapid Deployment Solutions",
            Email = "ignacio.lucano@rds-consulting.com",
            Url = new Uri("https://rds-consulting.com")
        }
    });

    opts.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Bearer {token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    opts.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme
        {
            Reference = new OpenApiReference
            {
                Type = ReferenceType.SecurityScheme,
                Id = "Bearer"
            },
            In = ParameterLocation.Header,
            Scheme = "Bearer"
        }
        ] = new List<string>()
    });

    opts.CustomOperationIds(api =>
        api.TryGetMethodInfo(out var mi) ? $"{mi.DeclaringType.Name}_{mi.Name}" : null
    );
    opts.CustomSchemaIds(t => t.FullName.Replace("+", "."));

    opts.ExampleFilters();
    opts.OperationFilter<SwaggerFilters>();
});
builder.Services.AddSwaggerExamplesFromAssemblyOf<InvalidParameterExample>();

//
// 5) Build & start the pipeline
//
var app = builder.Build();
var logger = app.Services.GetRequiredService<ILogService>();
logger.Log("Starting OpenText Integration API", LogLevel.INFO);
logger.Log($"Environment: {app.Environment.EnvironmentName}", LogLevel.INFO);

//
// 6) Conditionally apply virtual directory if hosted under IIS
//
if (!app.Environment.IsDevelopment())
{
    app.UsePathBase("/integrationRESTAPI");
}

//
// 7) Swagger UI
//
app.UseSwagger();
app.UseSwaggerUI(opts =>
{
    if (app.Environment.IsDevelopment())
    {
        opts.SwaggerEndpoint("/swagger/v1/swagger.json", "OpenText Integration v1");
    }
    else
    {
        opts.SwaggerEndpoint("/integrationRESTAPI/swagger/v1/swagger.json", "OpenText Integration v1");
    }
    opts.RoutePrefix = "swagger";
});

//
// 8) Skip your logging‐middleware on the multipart endpoint
//
//app.UseWhen(
//    ctx => !(ctx.Request.Method == "POST"
//             && ctx.Request.Path.StartsWithSegments("/v1/Nodes/create")
//             && ctx.Request.HasFormContentType),
//    branch => branch.UseMiddleware<RequestLoggingMiddleware>()
//);

//
// 9) HTTPS redirect only if you truly want it (leave commented if Suite cannot do HTTPS)
//
if (!app.Environment.IsDevelopment())
{
    // app.UseHttpsRedirection();
}

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

logger.Log("Application initialization complete", LogLevel.INFO);
app.Run();

//
// --- SUPPORTING TYPES BELOW ---
//

/// <summary>
/// Logs inbound requests; note we skip it on create‐document uploads above.
/// </summary>
public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogService     _logger;
    public RequestLoggingMiddleware(RequestDelegate next, ILogService logger)
    {
        _next   = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        ctx.Request.EnableBuffering();

        // Read the entire body into a local variable
        using var sr = new StreamReader(
            ctx.Request.Body,
            encoding: Encoding.UTF8,
            detectEncodingFromByteOrderMarks: false,
            leaveOpen: true);
        var requestBody = await sr.ReadToEndAsync();

        // Rewind so the next middleware (and MVC) can still read it
        ctx.Request.Body.Position = 0;

        // Build your log payload, now using the local 'requestBody'
        var info = new
        {
            Method      = ctx.Request.Method,
            Path        = ctx.Request.Path,
            QueryString = ctx.Request.QueryString.Value,
            Body        = requestBody
        };

        // Write it out
        _logger.LogRawInbound(
            $"req_{Guid.NewGuid():N}",
            JsonSerializer.Serialize(info, new JsonSerializerOptions { WriteIndented = true })
        );

        // Continue on
        await _next(ctx);
    }
}

