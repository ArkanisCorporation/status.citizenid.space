using Arkanis.Common.Aspire.ServiceDefaults;
using Arkanis.Common.Observability.Serilog;
using Serilog;

_ = SerilogBootstrapLogger.UseBootstrapLogger();

try
{
    Log.Information("Starting service.");

    var builder = WebApplication.CreateBuilder(args);

    _ = builder.AddServiceDefaults();
    _ = builder.AddSerilogDefaults();
    _ = builder.Services.AddControllers();
    _ = builder.Services.AddProblemDetails();
    _ = builder.Services.AddOpenApi();

    var app = builder.Build();

    _ = app.UseExceptionHandler();

    if (app.Environment.IsDevelopment())
    {
        _ = app.MapOpenApi();
    }

    _ = app.MapControllers();
    _ = app.MapDefaultEndpoints();

    await app.RunAsync();
}
catch (Exception exception)
{
    Log.Fatal(exception, "Service terminated during startup.");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}
