using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.Annotations;
using Swashbuckle.AspNetCore.Filters;
using OpenTextIntegrationAPI.Utilities;
using OpenTextIntegrationAPI.Models;
using OpenTextIntegrationAPI.Models.Filter;
using OpenTextIntegrationAPI.ClassObjects;
using OpenTextIntegrationAPI.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http.Features;
using System.Text.Json.Serialization;

// Program.cs - Main entry point for the OpenText Integration API application
// Handles application configuration, dependency injection, and middleware setup

var builder = WebApplication.CreateBuilder(args);

builder.Services
  .AddControllers()                // register MVC
  .ConfigureApiBehaviorOptions(opts => {
      // disable the automatic 400-response so your action always runs
      opts.SuppressModelStateInvalidFilter = true;
  })
  .AddJsonOptions(opts => {
      opts.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
      opts.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
  });


// Create a temporary logger for initialization phase
var tempLogger = new FileLoggerService(
    Microsoft.Extensions.Options.Options.Create(
        new FileLoggerOptions
{
LogDirectory = "logs",
LogLevel = "DEBUG"
}
    )
);
tempLogger.Log("Initializing OpenText Integration API application", LogLevel.INFO);

//
// SERVICE CONFIGURATION
//

// Configure OpenText settings from appsettings.json
tempLogger.Log("Configuring OpenText settings from appsettings.json", LogLevel.DEBUG);
builder.Services.Configure<OpenTextSettings>(builder.Configuration.GetSection("OpenText"));

// Configure file logger settings from appsettings.json
tempLogger.Log("Configuring FileLogger settings from appsettings.json", LogLevel.DEBUG);
builder.Services.Configure<FileLoggerOptions>(builder.Configuration.GetSection("FileLogger"));

// Register application services with dependency injection
tempLogger.Log("Registering application services", LogLevel.DEBUG);

// Register HTTP clients and authentication services
tempLogger.Log("Registering HTTP client and authentication services", LogLevel.DEBUG);
builder.Services.AddHttpClient<AuthService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<AuthManager>();

// Register business logic services
tempLogger.Log("Registering business logic services", LogLevel.DEBUG);
builder.Services.AddScoped<CSUtilities>();
builder.Services.AddScoped<CRBusinessWorkspace>();
builder.Services.AddScoped<MasterData>();
builder.Services.AddScoped<Node>();

// Register file-based logger as a singleton for application-wide logging
tempLogger.Log("Registering ILogService as singleton with FileLoggerService implementation", LogLevel.DEBUG);
builder.Services.AddSingleton<ILogService, FileLoggerService>();

// Register controller support for handling HTTP requests
tempLogger.Log("Registering controller services", LogLevel.DEBUG);
builder.Services.AddControllers();

//
// SWAGGER/OPENAPI CONFIGURATION
//

tempLogger.Log("Configuring Swagger/OpenAPI documentation", LogLevel.DEBUG);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
// Enable annotation support for Swagger documentation
options.EnableAnnotations();

// Define the OpenAPI document information
options.SwaggerDoc("v1", new OpenApiInfo
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

// Define bearer token security scheme for API authentication
tempLogger.Log("Configuring Bearer token security scheme", LogLevel.DEBUG);
options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
{
Description = "Enter the JWT token in the following format: Bearer {token}",
Name = "Authorization",
In = ParameterLocation.Header,
Type = SecuritySchemeType.ApiKey,
Scheme = "Bearer"
});

// Apply security requirement globally to all endpoints
options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                },
                Scheme = "Bearer",
                Name = "Bearer",
                In = ParameterLocation.Header,
            },
            new List<string>()
        }
    });

// Load example filters and operation filters for better Swagger documentation
tempLogger.Log("Configuring Swagger examples and operation filters", LogLevel.DEBUG);
options.ExampleFilters();
options.OperationFilter<SwaggerFilters>();
});

// Register Swagger example providers from the specified assembly
tempLogger.Log("Registering Swagger example providers", LogLevel.DEBUG);
builder.Services.AddSwaggerExamplesFromAssemblyOf<InvalidParameterExample>();

//
// APPLICATION BUILDING AND CONFIGURATION
//

// 1) Increase ASP.NET Core’s multipart‐form limit
builder.Services.Configure<FormOptions>(options =>
{
    // Be sure these are high enough for your files:
    options.ValueLengthLimit = int.MaxValue;
    options.MultipartHeadersLengthLimit = int.MaxValue;
    options.MultipartBodyLengthLimit = long.MaxValue; // <-- lift the limit!
});

// 2) Increase Kestrel’s max request‐body size
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = long.MaxValue; // or a sensible max
});

// (If you’re also hosting in‐process under IIS, you can bump it there too:)
builder.Services.Configure<IISServerOptions>(options =>
{
    options.MaxRequestBodySize = long.MaxValue;
});

// Build the application with all configured services
tempLogger.Log("Building application with configured services", LogLevel.DEBUG);
var app = builder.Build();

// Resolve the configured logger from DI container
var logger = app.Services.GetRequiredService<ILogService>();
logger.Log("Starting OpenText Integration API", LogLevel.INFO);
logger.Log($"Environment: {app.Environment.EnvironmentName}", LogLevel.INFO);
logger.Log("Configuring middleware and request pipeline...", LogLevel.DEBUG);

//
// MIDDLEWARE CONFIGURATION
//

// Configure the request logging middleware to capture request bodies
logger.Log("Enabling request logging middleware", LogLevel.DEBUG);
//app.UseMiddleware<RequestLoggingMiddleware>();

// Configure the base path when running under an IIS virtual directory
// This allows the application to work correctly when hosted at /integrationRESTAPI
if (!app.Environment.IsDevelopment())
{
logger.Log("Configuring application path base for IIS virtual directory", LogLevel.DEBUG);
app.UsePathBase("/integrationRESTAPI");
}

// Configure Swagger middleware for all environments
logger.Log("Enabling Swagger documentation middleware", LogLevel.DEBUG);
app.UseSwagger();
app.UseSwaggerUI(options =>
{
// Configure SwaggerEndpoint based on environment
if (app.Environment.IsDevelopment())
{
// In development, use the standard path
logger.Log("Configuring Swagger endpoint for development environment", LogLevel.DEBUG);
options.SwaggerEndpoint("/swagger/v1/swagger.json", "OpenText Integration API v1");
}
else
{
// In QAS/production, use the absolute path with virtual directory prefix
logger.Log("Configuring Swagger endpoint for non-development environment", LogLevel.DEBUG);
options.SwaggerEndpoint("/integrationRESTAPI/swagger/v1/swagger.json", "OpenText Integration API v1");
}
options.RoutePrefix = "swagger"; // Swagger UI available at /swagger
});

// Enable HTTPS redirection for security (only if not in development)
if (!app.Environment.IsDevelopment())
{
logger.Log("Enabling HTTPS redirection middleware", LogLevel.DEBUG);
//app.UseHttpsRedirection();
}



// Enable routing middleware for request routing
logger.Log("Enabling routing middleware", LogLevel.DEBUG);
app.UseRouting();

// Enable authentication and authorization middleware for security
logger.Log("Enabling authentication and authorization middleware", LogLevel.DEBUG);
app.UseAuthentication();
app.UseAuthorization();

// Map controller endpoints to handle HTTP requests
logger.Log("Mapping controller routes for HTTP request handling", LogLevel.DEBUG);
app.MapControllers();

// Start running the application and listen for incoming requests
logger.Log("Application initialization complete", LogLevel.INFO);
logger.Log("Application is running and listening for requests", LogLevel.INFO);
app.Run();

//
// MIDDLEWARE CLASSES (must be defined after top-level statements)
//

/// <summary>
/// Middleware for logging all incoming HTTP requests including their body content
/// </summary>
public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogService _logger;

    /// <summary>
    /// Initializes a new instance of the RequestLoggingMiddleware
    /// </summary>
    /// <param name="next">The next middleware in the pipeline</param>
    /// <param name="logger">Logging service for saving request information</param>
    public RequestLoggingMiddleware(RequestDelegate next, ILogService logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <summary>
    /// Processes an HTTP request by logging it and then invoking the next middleware
    /// </summary>
    /// <param name="context">The HTTP context for the request</param>
    /// <returns>A task that represents the completion of request processing</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        // Enable buffering to allow reading the request body multiple times
        context.Request.EnableBuffering();

        // Read the request body
        string requestBody = string.Empty;
        using (var reader = new StreamReader(
            context.Request.Body,
            encoding: Encoding.UTF8,
            detectEncodingFromByteOrderMarks: false,
            leaveOpen: true))
        {
            requestBody = await reader.ReadToEndAsync();
            context.Request.Body.Position = 0;  // Rewind the stream for subsequent middleware
        }

        // Create an object containing all relevant request information
        var requestInfo = new
        {
            Method = context.Request.Method,
            Path = context.Request.Path,
            QueryString = context.Request.QueryString.ToString(),
            Headers = context.Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString()),
            Body = requestBody
        };

        // Generate a unique identifier for this request log
        string requestLogId = $"inbound_request_{Guid.NewGuid():N}";

        // Log the complete request information including body
        _logger.LogRawInbound(requestLogId,
            JsonSerializer.Serialize(requestInfo, new JsonSerializerOptions { WriteIndented = true }));

        // Continue to the next middleware in the pipeline
        await _next(context);
    }
}