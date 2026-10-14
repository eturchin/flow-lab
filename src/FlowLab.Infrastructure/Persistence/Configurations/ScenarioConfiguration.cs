using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FlowLab.Infrastructure;

internal sealed class ScenarioConfiguration : IEntityTypeConfiguration<ScenarioEntity>
{
    public void Configure(EntityTypeBuilder<ScenarioEntity> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasOne<WorkspaceEntity>().WithMany().HasForeignKey(x => x.WorkspaceId).OnDelete(DeleteBehavior.Cascade);
        builder.Property(x => x.Name).HasMaxLength(120);
        builder.Property(x => x.Description).HasMaxLength(2000);
        builder.Property(x => x.SettingsJson).HasColumnType("jsonb");
        builder.HasMany(x => x.Runs).WithOne().HasForeignKey(x => x.ScenarioId).OnDelete(DeleteBehavior.Cascade);
    }
}
