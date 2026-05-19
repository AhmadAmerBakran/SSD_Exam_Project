using SecureFileUploadPortal.Api.Configuration;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddPortalServices(builder.Configuration);

var app = builder.Build();

await app.EnsureDatabaseCreatedAsync();

app.ConfigureRequestPipeline();

await app.RunAsync();