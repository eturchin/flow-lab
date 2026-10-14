using System.Text.Json;
using System.Text.Json.Serialization;
using FlowLab.Application;
using FlowLab.Domain;
using Microsoft.EntityFrameworkCore;

namespace FlowLab.Infrastructure;

public sealed class LabDbContext(DbContextOptions<LabDbContext> options) : DbContext(options)
{
    public DbSet<ScenarioEntity> Scenarios => Set<ScenarioEntity>();
    public DbSet<RunEntity> Runs => Set<RunEntity>();
    public DbSet<WorkspaceEntity> Workspaces => Set<WorkspaceEntity>();
    protected override void OnModelCreating(ModelBuilder model)
    {
        model.ApplyConfigurationsFromAssembly(typeof(LabDbContext).Assembly);
        model.Entity<WorkspaceEntity>().Property(x => x.Name).HasMaxLength(120);
    }
}
