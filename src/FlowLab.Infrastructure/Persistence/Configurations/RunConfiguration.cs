using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FlowLab.Infrastructure;

internal sealed class RunConfiguration : IEntityTypeConfiguration<RunEntity>
{
    public void Configure(EntityTypeBuilder<RunEntity> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SnapshotJson).HasColumnType("jsonb");
        builder.Property(x => x.ResultJson).HasColumnType("jsonb");
        builder.Property(x => x.FailuresJson).HasColumnType("jsonb");
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(24);
        builder.Property(x => x.Error).HasMaxLength(500);
        builder.HasIndex(x => new { x.ScenarioId, x.CreatedAt });
    }
}
