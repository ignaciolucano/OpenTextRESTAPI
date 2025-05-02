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

var builder = WebApplication.CreateBuilder(args);

// Configure OpenText settings from appsettings.json
builder.Services.Configure<OpenTextSettings>(builder.Configuration.GetSection("OpenText"));

// Configure file logger settings from appsettings.json
builder.Services.Configure<FileLoggerOptions>(builder.Configuration.GetSection("FileLogger"));

// Register application services with dependency injection
builder.Services.AddHttpClient<AuthService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<AuthManager>();
builder.Services.AddScoped<CSUtilities>();
builder.Services.AddScoped<CRBusinessWorkspace>();
builder.Services.AddScoped<MasterData>();
builder.Services.AddScoped<Node>();

// Register file-based logger as a singleton
builder.Services.AddSingleton<ILogService, FileLoggerService>();

// Register controller support
builder.Services.AddControllers();

// Configure Swagger/OpenAPI documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.EnableAnnotations();

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

    // Define bearer token security scheme
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Enter the JWT token in the following format: Bearer {token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    // Apply security requirement globally
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

    // Load example filters and operation filters
    options.ExampleFilters();
    options.OperationFilter<SwaggerFilters>();
});

// Register Swagger example providers
builder.Services.AddSwaggerExamplesFromAssemblyOf<InvalidParameterExample>();

// Build the application
var app = builder.Build();

// Resolve logger and start logging the application startup
var logger = app.Services.GetRequiredService<ILogService>();
logger.Log("Starting OpenText Integration API", LogLevel.INFO);
logger.Log("Configuring middleware and request pipeline...", LogLevel.DEBUG);

// Enable Swagger middleware for API documentation
logger.Log("Enabling Swagger and SwaggerUI", LogLevel.DEBUG);
app.UseSwagger();
app.UseSwaggerUI();

// Enable HTTPS redirection
logger.Log("Enabling HTTPS redirection", LogLevel.DEBUG);
app.UseHttpsRedirection();

// Enable authentication and authorization middleware
logger.Log("Enabling authentication and authorization", LogLevel.DEBUG);
app.UseAuthentication();
app.UseAuthorization();

// Map controller endpoints to handle HTTP requests
logger.Log("Mapping controller routes", LogLevel.DEBUG);
app.MapControllers();

// Start running the application
logger.Log("Application is running and listening for requests", LogLevel.INFO);
app.Run();
