using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlowLab.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class InitialCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Scenarios",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                SettingsJson = table.Column<string>(type: "jsonb", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Scenarios", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Runs",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ScenarioId = table.Column<Guid>(type: "uuid", nullable: false),
                Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                Progress = table.Column<double>(type: "double precision", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                FinishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                SnapshotJson = table.Column<string>(type: "jsonb", nullable: false),
                ResultJson = table.Column<string>(type: "jsonb", nullable: true),
                Error = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Runs", x => x.Id);
                table.ForeignKey(
                    name: "FK_Runs_Scenarios_ScenarioId",
                    column: x => x.ScenarioId,
                    principalTable: "Scenarios",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Runs_ScenarioId_CreatedAt",
            table: "Runs",
            columns: new[] { "ScenarioId", "CreatedAt" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "Runs");

        migrationBuilder.DropTable(
            name: "Scenarios");
    }
}
