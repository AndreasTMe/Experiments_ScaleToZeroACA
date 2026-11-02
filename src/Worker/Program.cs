var mainEntered = DateTimeOffset.UtcNow;

using var process = Process.GetCurrentProcess();

var timeline = new StartupTimeline
{
    ProcessStarted = new DateTimeOffset(process.StartTime),
    MainEntered    = mainEntered
};

var options = WorkerOptions.FromEnvironment();

// The empty builder reads no appsettings files or command line, so configuration comes from the environment only.
var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
builder.Logging.AddSimpleConsole(console =>
{
    console.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffffffZ ";
    console.UseUtcTimestamp = true;
});

builder.Services.AddSingleton(options);
builder.Services.AddSingleton(timeline);
builder.Services.AddSingleton(_ => new ServiceBusClient(options.ConnectionString));
builder.Services.AddHostedService<QueueProcessor>();

await builder.Build().RunAsync();