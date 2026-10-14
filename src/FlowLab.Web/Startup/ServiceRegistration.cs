using System.Text.Json.Serialization;
using FlowLab.Application;
using FlowLab.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Npgsql;
namespace FlowLab.Web;

public static class ServiceRegistration
{
    public static WebApplicationBuilder AddFlowLab(this WebApplicationBuilder builder)
    {
        if (builder.Environment.IsDevelopment())
        {
            builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true);
        }
        builder.Configuration.AddEnvironmentVariables();
        builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 256 * 1024);
        builder.Logging.AddJsonConsole();
        builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        builder.Services.AddSignalR().AddJsonProtocol(o => o.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        builder.Services.AddProblemDetails();
        builder.Services.AddExceptionHandler<ApiExceptionHandler>();
        var connection = builder.Configuration.GetConnectionString("FlowLab") ?? throw new InvalidOperationException("Задайте ConnectionStrings__FlowLab. Инструкция: README.md.");
        var cs = new NpgsqlConnectionStringBuilder(connection);
        if (!builder.Environment.IsDevelopment() && (string.IsNullOrWhiteSpace(cs.Password) || cs.Password == "postgres"))
        {
            throw new InvalidOperationException("Для production задайте отдельный пароль PostgreSQL.");
        }

        builder.Services.AddDbContext<LabDbContext>(o => o.UseNpgsql(connection));
        builder.Services.AddScoped<ILabStore, LabStore>();
        builder.Services.AddScoped<ScenarioService>();
        builder.Services.AddScoped<WorkspaceStore>();
        builder.Services.AddScoped<RunService>();
        builder.Services.AddSingleton<RunQueue>();
        builder.Services.AddSingleton<IRunScheduler>(services => services.GetRequiredService<RunQueue>());
        builder.Services.AddHostedService<SimulationWorker>();
        builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("postgresql");
        return builder;
    }
}
