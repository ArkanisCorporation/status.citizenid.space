using Arkanis.Common.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

_ = builder.AddProject<Projects.Template_Service>("service")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck(HealthEndpointUrlPaths.Liveness);

builder.Build().Run();
