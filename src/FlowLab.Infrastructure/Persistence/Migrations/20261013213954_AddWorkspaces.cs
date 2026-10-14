using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlowLab.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddWorkspaces : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "WorkspaceId",
            table: "Scenarios",
            type: "uuid",
            nullable: false,
            defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

        migrationBuilder.CreateTable(
            name: "Workspaces",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Workspaces", x => x.Id);
            });

        migrationBuilder.InsertData("Workspaces", new[] { "Id", "Name", "CreatedAt" },
                new object[] { Guid.Empty, "Лаборатория", new DateTimeOffset(2026, 10, 14, 0, 0, 0, TimeSpan.Zero) });

        migrationBuilder.CreateIndex(
            name: "IX_Scenarios_WorkspaceId",
            table: "Scenarios",
            column: "WorkspaceId");

        migrationBuilder.AddForeignKey(
            name: "FK_Scenarios_Workspaces_WorkspaceId",
            table: "Scenarios",
            column: "WorkspaceId",
            principalTable: "Workspaces",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_Scenarios_Workspaces_WorkspaceId",
            table: "Scenarios");

        migrationBuilder.DropTable(
            name: "Workspaces");

        migrationBuilder.DropIndex(
            name: "IX_Scenarios_WorkspaceId",
            table: "Scenarios");

        migrationBuilder.DropColumn(
            name: "WorkspaceId",
            table: "Scenarios");
    }
}
