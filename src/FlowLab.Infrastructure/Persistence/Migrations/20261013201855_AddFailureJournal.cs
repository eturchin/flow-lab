using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlowLab.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddFailureJournal : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "FailuresJson",
            table: "Runs",
            type: "jsonb",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "FailuresJson",
            table: "Runs");
    }
}
