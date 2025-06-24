// Program.cs
// Entry point for the OpenText Integration REST API application
// Configures services, middleware, Swagger, and logging pipeline
// Author: [Your Name]
// Date: [Current Date]

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
using OpenTextIntegrationAPI.Middlewares;
using Swashbuckle.AspNetCore.Filters;
using Swashbuckle.AspNetCore.SwaggerGen;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

#region ░░ SERVICE CONFIGURATION ░░

//
// 1. Add MVC Controllers + JSON Settings
//
builder.Services
    .AddControllers()
    .ConfigureApiBehaviorOptions(opts =>
    {
        // Disables automatic 400 response when model state is invalid
        opts.SuppressModelStateInvalidFilter = true;
    })
    .AddJsonOptions(opts =>
    {
        // Prevents reference loops during JSON serialization
        opts.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        // Ignores null values when serializing JSON
        opts.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

//
// 2. Increase Request Size Limits for File Uploads (Form Options + Kestrel + IIS)
//
builder.Services.AddControllersWithViews();

builder.Services.Configure<FormOptions>(opts =>
{
    // Set maximum allowed length for form value
    opts.ValueLengthLimit = int.MaxValue;
    // Set maximum allowed length for multipart headers
    opts.MultipartHeadersLengthLimit = int.MaxValue;
    // Set maximum allowed length for multipart body (file uploads)
    opts.MultipartBodyLengthLimit = long.MaxValue;
});

builder.WebHost.ConfigureKestrel(opts =>
{
    // Set maximum request body size for Kestrel server
    opts.Limits.MaxRequestBodySize = long.MaxValue;
    // Enable synchronous IO for compatibility with some logging or legacy code
    opts.AllowSynchronousIO = true;
});

builder.Services.Configure<IISServerOptions>(opts =>
{
    // Set maximum request body size for IIS server
    opts.MaxRequestBodySize = long.MaxValue;
});

//
// 3. Register Application Services, Configuration Bindings, and Logging
//
// Bind OpenText settings from configuration section
builder.Services.Configure<OpenTextSettings>(builder.Configuration.GetSection("OpenText"));
// Bind FileLogger options from configuration section
builder.Services.Configure<FileLoggerOptions>(builder.Configuration.GetSection("FileLogger"));

// Register HttpClient for AuthService
builder.Services.AddHttpClient<AuthService>();
// Register scoped services for dependency injection
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<AuthManager>();
builder.Services.AddScoped<CSUtilities>();
builder.Services.AddScoped<CRBusinessWorkspace>();
builder.Services.AddScoped<MasterData>();
builder.Services.AddScoped<Node>();
builder.Services.AddScoped<MemberService>();

// Register singleton logging service
builder.Services.AddSingleton<ILogService, FileLoggerService>();
// Register HTTP context accessor for accessing HTTP context in services
builder.Services.AddHttpContextAccessor();

#endregion

#region ░░ SWAGGER + OPENAPI ░░

//
// 4. Configure Swagger with Authentication, Descriptions, Examples, and Filters
//
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(opts =>
{
    // Enable Swagger annotations for decorating API methods
    opts.EnableAnnotations();

    // Define Swagger document info
    opts.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "OpenText Integration REST API",
        Version = "v1",
        Description = "Florida Crystals - REST API to Integrate with third-party applications",
        Contact = new OpenApiContact
        {
            Name = "Rapid Deployment Solutions",
            Email = "ignacio.lucano@rds-consulting.com",
            Url = new Uri("https://rds-consulting.com")
        }
    });

    opts.DocInclusionPredicate((name, api) => !api.RelativePath.StartsWith("loganalyzer"));

    // Add security definition for Bearer token authentication
    opts.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Bearer {token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    // Add security requirement to use Bearer token globally
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
        }] = new List<string>()
    });

    // Customize operation IDs to use controller and method names
    opts.CustomOperationIds(api =>
        api.TryGetMethodInfo(out var mi) ? $"{mi.DeclaringType.Name}_{mi.Name}" : null
    );

    // Customize schema IDs to use full type names with dots instead of plus signs
    opts.CustomSchemaIds(t => t.FullName.Replace("+", "."));

    // Enable example filters for Swagger UI
    opts.ExampleFilters();

    // Add custom operation filters for Swagger
    opts.OperationFilter<SwaggerFilters>();
});

// Register Swagger examples from assembly
builder.Services.AddSwaggerExamplesFromAssemblyOf<InvalidParameterExample>();

#endregion

#region ░░ BUILD + STARTUP LOGGING ░░

//
// 5. Build the Application and Log Startup
//
var app = builder.Build();

// Retrieve the logging service from DI container
var logger = app.Services.GetRequiredService<ILogService>();

// Log application startup information
logger.Log("Starting OpenText Integration API", LogLevel.INFO);
logger.Log($"Environment: {app.Environment.EnvironmentName}", LogLevel.INFO);

#endregion

#region ░░ PIPELINE CONFIGURATION ░░

//
// 6. Apply Virtual Path for IIS Deployments
//
if (!app.Environment.IsDevelopment())
{
    // Use a base path for IIS hosting environment
    app.UsePathBase("/integrationRESTAPI");
}

//
// 7. Enable Swagger UI
//
app.UseSwagger();
app.UseSwaggerUI(opts =>
{
    if (app.Environment.IsDevelopment())
    {
        // Swagger endpoint for development environment
        opts.SwaggerEndpoint("/swagger/v1/swagger.json", "OpenText Integration v1");
    }
    else
    {
        // Swagger endpoint for production or other environments with base path
        opts.SwaggerEndpoint("/integrationRESTAPI/swagger/v1/swagger.json", "OpenText Integration v1");
    }
    // Set Swagger UI route prefix
    opts.RoutePrefix = "swagger";
});

//
// 8. Enable HTTPS Redirection if required (usually disabled for Suite environments)
//
if (!app.Environment.IsDevelopment())
{
    // Uncomment to enable HTTPS redirection in non-development environments
    // app.UseHttpsRedirection();
}

//
// 9. Routing, Authentication, Authorization
//
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

//
// 10. Global Middleware for Full Request/Response Logging (Configured via appsettings)
//
app.UseMiddleware<RequestResponseLoggingMiddleware>();

//
// 11. Map Controller Routes
//
// Log Analyzer Routes 
app.MapControllerRoute(
    name: "loganalyzer_default",
    pattern: "loganalyzer",
    defaults: new { controller = "LogAnalyzer", action = "Index" });

app.MapControllerRoute(
    name: "loganalyzer_api",
    pattern: "loganalyzer/api/{action}/{id?}",
    defaults: new { controller = "LogAnalyzer" });

app.MapControllers();

//
// 12. Final Startup Log and Run
//
logger.Log("Application initialization complete", LogLevel.INFO);
app.Run();

#endregion