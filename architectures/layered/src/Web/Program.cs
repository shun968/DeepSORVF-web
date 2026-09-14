using LayeredArchitecture.Application.Pipeline;
using LayeredArchitecture.Application.Services;
using LayeredArchitecture.Domain.Repositories;
using LayeredArchitecture.Infrastructure.Detection;
using LayeredArchitecture.Infrastructure.Repositories;
using LayeredArchitecture.Web.Contracts;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.Configure<RunDefaults>(builder.Configuration.GetSection(RunDefaults.SectionName));
builder.Services.AddScoped<IAisRepository, CsvAisRepository>();
builder.Services.AddScoped<ICameraParametersRepository, TextFileCameraParametersRepository>();
builder.Services.AddScoped<IMotResultWriter, MotResultFileWriter>();
builder.Services.AddScoped<IVideoFrameRepository, OpenCvVideoFrameRepository>();

// Loaded once (the model is 35MB). `task run` points Detection:ModelPath at the ONNX export.
builder.Services.AddSingleton<IVesselDetector>(services => new YoloxVesselDetector(
    services.GetRequiredService<IConfiguration>()["Detection:ModelPath"]
    ?? Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "../../../../detection_yolox/model_data/YOLOX-final.onnx"))));
builder.Services.AddScoped<AisService>();
builder.Services.AddScoped<DetectionService>();
builder.Services.AddScoped<TrackingService>();
builder.Services.AddScoped<FusionService>();
builder.Services.AddScoped<VesselTrackingPipeline>();

var app = builder.Build();

// The viewer page (wwwroot/index.html) that draws a run over its frames.
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapControllers();

await app.RunAsync();

// Exposes the top-level-statement-generated Program class to WebApplicationFactory<Program>
// in tests/Web.IntegrationTests (which lives in a separate assembly, so it needs `public`).
public partial class Program;
