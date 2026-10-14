using FlowLab.Web;

var builder = WebApplication.CreateBuilder(args).AddFlowLab();
var app = builder.Build();

await app.Services.InitializeDatabaseAsync();

app.UseExceptionHandler();
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapHealthChecks("/health/ready");
app.MapGet("/health/live", () => Results.Ok(new { status = "healthy" }));
app.MapScenarioEndpoints();
app.MapWorkspaceEndpoints();
app.MapRunEndpoints();
app.MapHub<SimulationHub>("/hubs/simulation");
app.MapFallback("/api/{**path}", () => Results.NotFound());
app.MapFallbackToFile("index.html");

await app.RunAsync();
