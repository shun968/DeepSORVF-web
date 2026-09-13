using CleanArchitecture.Application.UseCases;
using CleanArchitecture.Domain.Ports;
using CleanArchitecture.Domain.Services;
using CleanArchitecture.Infrastructure.Adapters;
using CleanArchitecture.Infrastructure.Mocks;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// The whole point of the pattern: every step of the pipeline is chosen here, and nothing
// above this file knows which implementation it got. Swapping the mock detector for a real
// YOLOX one is a change to this line and nothing else.
builder.Services.AddScoped<IAisReader, CsvAisReader>();
builder.Services.AddScoped<ICameraParametersReader, TextFileCameraParametersReader>();
builder.Services.AddScoped<IDetector, MockDetector>();
builder.Services.AddScoped<ITracker, SequentialTracker>();
builder.Services.AddScoped<IFusionEngine, NearestVesselFusionEngine>();

// Scoped, because it remembers the previous second for the length of one run.
builder.Services.AddScoped<AisSightingService>();
builder.Services.AddScoped<ProcessVideoFrameUseCase>();
builder.Services.AddScoped<ProcessVesselTrackingRunUseCase>();

var app = builder.Build();

app.MapControllers();

await app.RunAsync();

// Exposes the top-level-statement Program class to WebApplicationFactory<Program> in tests.
public partial class Program;
