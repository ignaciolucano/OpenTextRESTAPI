using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
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

// Program.cs - Main entry point for the OpenText Integration API application
// Handles application configuration, dependency injection, and middleware setup

var builder = WebApplication.CreateBuilder(args);

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

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    logger.Log("Enabling Swagger documentation middleware", LogLevel.DEBUG);
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "OpenText Integration API v1");
        options.RoutePrefix = "swagger"; // Swagger UI disponible en /swagger
    });
}

// Enable HTTPS redirection for security (only if not in development)
if (!app.Environment.IsDevelopment())
{
    logger.Log("Enabling HTTPS redirection middleware", LogLevel.DEBUG);
    app.UseHttpsRedirection();
}

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