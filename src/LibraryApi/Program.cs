using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Text.Json.Serialization;
using LibraryApi.Auth;
using LibraryApi.Data;
using LibraryApi.Infrastructure;
using LibraryApi.Options;
using LibraryApi.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);
// A local ignored JSON file is shared by F5, CLI and published-demo scripts.
// Environment variables and CLI arguments retain the highest priority.
var localSettings = Environment.GetEnvironmentVariable("LIBRARY_SETTINGS_FILE")
    ?? Path.Combine(builder.Environment.ContentRootPath, "..", "..", ".local", "demo-settings.json");
builder.Configuration.AddJsonFile(Path.GetFullPath(localSettings), optional: true, reloadOnChange: false)
    .AddEnvironmentVariables().AddCommandLine(args);

builder.Services.AddOptions<JwtOptions>().BindConfiguration("Jwt").ValidateDataAnnotations()
    .Validate(o => Encoding.UTF8.GetByteCount(o.Key) >= 64,
        "Задайте Jwt:Key длиной от 64 байт. Выполните scripts/setup-demo.ps1.").ValidateOnStart();
builder.Services.AddOptions<LibraryOptions>().BindConfiguration("Library").ValidateDataAnnotations().ValidateOnStart();
builder.Services.Configure<DemoOptions>(builder.Configuration.GetSection("Demo"));
builder.Services.AddDbContext<LibraryDbContext>((services, o) =>
{
    var config = services.GetRequiredService<IConfiguration>();
    var connection = new SqliteConnectionStringBuilder(config.GetConnectionString("Library")
        ?? throw new InvalidOperationException("Не настроено подключение Library."));
    if (connection.DataSource != ":memory:")
    {
        connection.DataSource = Path.GetFullPath(connection.DataSource, builder.Environment.ContentRootPath);
        Directory.CreateDirectory(Path.GetDirectoryName(connection.DataSource)!);
    }
    o.UseSqlite(connection.ToString()).AddInterceptors(new SqliteFunctionsInterceptor());
});
builder.Services.AddSingleton(TimeProvider.System);
// This API uses JWT only; it has no cookies or persistent DataProtection payloads.
// Keep MVC's unused protection keys in memory instead of writing to a user profile.
builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
builder.Services.AddScoped<IPasswordHasher<AppUser>, PasswordHasher<AppUser>>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<CatalogService>();
builder.Services.AddScoped<LoanService>();
builder.Services.AddScoped<ReportService>();
builder.Services.AddControllers().AddJsonOptions(o =>
    o.JsonSerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow);
builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = context =>
{
    context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
    context.ProblemDetails.Instance ??= context.HttpContext.Request.Path;
});
builder.Services.Configure<ApiBehaviorOptions>(o => o.InvalidModelStateResponseFactory = context =>
{
    var problem = new ValidationProblemDetails(context.ModelState)
    {
        Status = 400, Title = "Ошибка проверки входных данных", Instance = context.HttpContext.Request.Path
    };
    problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
    return new BadRequestObjectResult(problem) { ContentTypes = { "application/problem+json" } };
});
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme).Configure<Microsoft.Extensions.Options.IOptions<JwtOptions>>((o, settings) =>
{
    var jwt = settings.Value;
    o.MapInboundClaims = false;
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true, ValidIssuer = jwt.Issuer,
        ValidateAudience = true, ValidAudience = jwt.Audience,
        ValidateLifetime = true, ClockSkew = TimeSpan.Zero,
        ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        NameClaimType = JwtRegisteredClaimNames.UniqueName, RoleClaimType = "role"
    };
});
builder.Services.AddAuthorization();
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
{
    var origins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [];
    if (origins.Length > 0) p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
}));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new OpenApiInfo { Title = "Library API", Version = "v1",
        Description = "Библиотека: экземпляры книг, читатели, выдачи и четыре отчёта. Даты UTC; периоды [from, to)." });
    o.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "LibraryApi.xml"));
    o.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT", Description = "Получите accessToken через /api/v1/auth/login."
    });
    o.OperationFilter<ApiDocumentationFilter>();
});

var app = builder.Build();
app.UseExceptionHandler();
app.UseStatusCodePages(async status =>
{
    var http = status.HttpContext;
    var service = http.RequestServices.GetRequiredService<IProblemDetailsService>();
    await service.WriteAsync(new ProblemDetailsContext
    {
        HttpContext = http,
        ProblemDetails = new ProblemDetails
        {
            Status = http.Response.StatusCode,
            Title = http.Response.StatusCode switch { 401 => "Требуется действительный токен", 403 => "Недостаточно прав", 404 => "Ресурс не найден", _ => "Ошибка HTTP" }
        }
    });
});
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Demo:Swagger"))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.MapControllers();
app.MapGet("/", () => Results.Ok(new { name = "Library API", version = "1.0.0", catalog = "/api/v1/books" })).AllowAnonymous();
await DatabaseInitializer.Initialize(app.Services);
await app.RunAsync();

public partial class Program;
