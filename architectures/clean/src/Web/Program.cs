using CleanArchitecture.Application.UseCases;
using CleanArchitecture.Domain.Ports;
using CleanArchitecture.Domain.Services;
using CleanArchitecture.Infrastructure.Adapters;
using CleanArchitecture.Web.Contracts;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.Configure<RunDefaults>(builder.Configuration.GetSection(RunDefaults.SectionName));

// The whole point of the pattern: every step of the pipeline is chosen here, and nothing
// above this file knows which implementation it got. Replacing the mock detector with YOLOX
// changed these lines and the use cases not at all.
builder.Services.AddScoped<IAisReader, CsvAisReader>();
builder.Services.AddScoped<ICameraParametersReader, TextFileCameraParametersReader>();
builder.Services.AddScoped<IVideoFrameReader, OpenCvVideoFrameReader>();

// Loaded once (the model is 35MB). `task run` points Detection:ModelPath at the ONNX export.
builder.Services.AddSingleton<IDetector>(services => new YoloxDetector(
    services.GetRequiredService<IConfiguration>()["Detection:ModelPath"]
    ?? Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "../../../../detection_yolox/model_data/YOLOX-final.onnx"))));
builder.Services.AddScoped<ITracker, IouTracker>();
builder.Services.AddScoped<IFusionEngine, NearestVesselFusionEngine>();

// Scoped, because it remembers the previous second for the length of one run.
builder.Services.AddScoped<AisSightingService>();
builder.Services.AddScoped<ProcessVideoFrameUseCase>();
builder.Services.AddScoped<ProcessVesselTrackingRunUseCase>();

var app = builder.Build();

// The viewer page (wwwroot/index.html) that draws a run over its frames.
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapControllers();

await app.RunAsync();

// Exposes the top-level-statement Program class to WebApplicationFactory<Program> in tests.
public partial class Program;
