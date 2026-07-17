using Microsoft.EntityFrameworkCore;
using JLMS.Api.Data;
using JLMS.Api.Services;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

// ---------- Services ----------
builder.Services.AddControllers().AddJsonOptions(options =>
{
    // Keep enums/strings as-is, avoid $id/$ref noise in JSON for the frontend
    options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "JLMS API",
        Version = "v1",
        Description = "Jewel Loan Management System — backend API (test build, empty database)"
    });
});

// ---------- Database (SQL Server via SQL Authentication) ----------
// The actual connection string lives in appsettings.json / appsettings.Development.json
// or in User Secrets (recommended for the password) — see README for setup steps.
builder.Services.AddDbContext<JlmsDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("JlmsDb")));


builder.Services.AddScoped<LoanCalculationService>();

builder.Services.AddScoped<LoanOperationsCalculationHelper>();
builder.Services.AddScoped<LoanOperationsService>();
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
builder.Services.AddScoped<LoanReceiptPdfService>();
// ---------- CORS ----------
// Allows the static HTML/JS frontend (opened from file:// or a local dev
// server on any port) to call this API during testing.
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontendDev", policy =>
    {
        policy.SetIsOriginAllowed(_ => true) // permissive for local testing only
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// ---------- Middleware pipeline ----------
//if (app.Environment.IsDevelopment())
//{
//    app.UseSwagger();
//    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "JLMS API v1"));
//}

app.UseCors("AllowFrontendDev");

// Serve wwwroot (if present)
app.UseStaticFiles();

// Serve files from Uploads folder
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(
        Path.Combine(builder.Environment.ContentRootPath, "Uploads")),
    RequestPath = "/Uploads"
});

app.UseMiddleware<JLMS.Api.Middleware.UserContextMiddleware>();
app.UseAuthorization();

app.MapControllers();

// Simple root + health check so opening the base URL doesn't 404 confusingly
app.MapGet("/", () => Results.Redirect("/swagger"));
app.MapGet("/api/health", () => Results.Ok(new { status = "ok", time = DateTime.UtcNow }));

app.Run();
