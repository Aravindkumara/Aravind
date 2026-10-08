using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using QuestPDF.Infrastructure;
using Serilog;
using ServiceExcellence.Api.Infrastructure;
using ServiceExcellence.Api.Services;
using ServiceExcellence.Data;

Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, config) => config
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    // Data layer (Dapper + PostgreSQL)
    var connectionString = builder.Configuration.GetConnectionString("Default")
        ?? throw new InvalidOperationException("Connection string 'Default' is not configured.");
    builder.Services.AddDataLayer(connectionString);

    // Application services
    builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.Section));
    builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection(StorageOptions.Section));
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddSingleton<ITokenService, TokenService>();
    builder.Services.AddSingleton<IFileStorage, LocalFileStorage>();
    builder.Services.AddScoped<IAuditService, AuditService>();
    builder.Services.AddScoped<SopService>();
    builder.Services.AddScoped<SopPdfGenerator>();
    QuestPDF.Settings.License = LicenseType.Community;

    // Authentication (JWT bearer) and authorization
    var jwt = builder.Configuration.GetSection(JwtOptions.Section).Get<JwtOptions>() ?? new JwtOptions();
    if (jwt.Key.Length < 32)
        throw new InvalidOperationException("Jwt:Key must be at least 32 characters long.");

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwt.Issuer,
                ValidateAudience = true,
                ValidAudience = jwt.Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = jwt.SigningKey,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(1),
                NameClaimType = AppClaims.Username,
                RoleClaimType = AppClaims.Role
            };
        });
    builder.Services.AddAuthorization();

    builder.Services.AddControllers()
        .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
    builder.Services.AddProblemDetails();
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

    // Swagger with JWT support
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "OEM Product Service Excellence API",
            Version = "v1",
            Description = "Standard operating procedures for dealer product service, classified by division, model category, model, variant and assembly."
        });
        c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Paste the token returned by POST /api/auth/login."
        });
        c.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
                Array.Empty<string>()
            }
        });
        var xml = Path.Combine(AppContext.BaseDirectory, "ServiceExcellence.Api.xml");
        if (File.Exists(xml)) c.IncludeXmlComments(xml);
    });

    builder.Services.AddHealthChecks();

    var app = builder.Build();

    // Request logging goes first so it records the final status code produced by the exception handler.
    app.UseSerilogRequestLogging();
    app.UseExceptionHandler();
    app.UseStatusCodePages();

    if (app.Configuration.GetValue("Swagger:Enabled", app.Environment.IsDevelopment()))
    {
        app.UseSwagger();
        app.UseSwaggerUI(c => c.DocumentTitle = "Service Excellence API");
    }

    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();
    app.MapHealthChecks("/health");

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "API terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
