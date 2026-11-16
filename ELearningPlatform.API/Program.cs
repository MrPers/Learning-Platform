using ELearningPlatform.Application;
using ELearningPlatform.Infrastructure;
using ELearningPlatform.Infrastructure.Persistence.Seeding;
using Hangfire;
using Microsoft.Extensions.Configuration.Json;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Passwords, keys and tokens live in appsettings.Secrets.json (git-ignored). It sits right after
// appsettings.{Environment}.json, so environment variables and command-line arguments still override it.
AddSecretsFile(builder.Configuration, "appsettings.Secrets.json");

builder.Services
    .AddApplication()
    .AddInfrastructure()
    .AddApi(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
    // Default dashboard authorization only allows local requests
    app.MapHangfireDashboard("/hangfire");
}
else
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseCors(AppSettingsConstants.CORSPolicy);
app.UseAuthentication();
// After authentication, so per-user policies (payments) can partition by the user id
app.UseRateLimiter();
app.UseAuthorization();
app.MapControllers();

if (!app.Configuration.GetValue("Database:SkipInitialization", false))
    await app.Services.InitializeDatabaseAsync(seedTestData: app.Environment.IsDevelopment());

await app.RunAsync();

static void AddSecretsFile(ConfigurationManager configuration, string path)
{
    var sources = configuration.Sources;
    var lastJson = sources.Select((source, index) => (source, index))
        .LastOrDefault(x => x.source is JsonConfigurationSource { Path: not null } json && json.Path.StartsWith("appsettings", StringComparison.OrdinalIgnoreCase));
    // No file provider set: the configuration's base path (the content root) is used
    var source = new JsonConfigurationSource { Path = path, Optional = true, ReloadOnChange = true };
    if (lastJson.source == null)
        sources.Add(source);
    else
        sources.Insert(lastJson.index + 1, source);
}

public partial class Program;
