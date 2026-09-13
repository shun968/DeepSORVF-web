using LayeredArchitecture.Application.Pipeline;
using LayeredArchitecture.Application.Services;
using LayeredArchitecture.Domain.Repositories;
using LayeredArchitecture.Infrastructure.Repositories;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddScoped<IAisRepository, CsvAisRepository>();
builder.Services.AddScoped<AisService>();
builder.Services.AddScoped<DetectionService>();
builder.Services.AddScoped<TrackingService>();
builder.Services.AddScoped<FusionService>();
builder.Services.AddScoped<VesselTrackingPipeline>();

var app = builder.Build();

app.MapControllers();

await app.RunAsync();

// Exposes the top-level-statement-generated Program class to WebApplicationFactory<Program>
// in tests/Web.IntegrationTests (which lives in a separate assembly, so it needs `public`).
public partial class Program;
