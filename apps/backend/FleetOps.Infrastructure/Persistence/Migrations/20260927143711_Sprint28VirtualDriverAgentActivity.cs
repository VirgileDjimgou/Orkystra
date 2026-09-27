using Microsoft.EntityFrameworkCore.Migrations;

#pragma warning disable CA1861 // EF-generated migration metadata uses array literals.

#nullable disable

namespace FleetOps.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class Sprint28VirtualDriverAgentActivity : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AgentActivities",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AgentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                DriverId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                VehicleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                MissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                StopId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Sequence = table.Column<long>(type: "bigint", nullable: false),
                ObservedState = table.Column<int>(type: "int", nullable: false),
                Policy = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                Action = table.Column<int>(type: "int", nullable: false),
                ResultCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                ResultMessage = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                OccurredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AgentActivities", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_AgentActivities_OrganizationId_AgentId_Sequence",
            table: "AgentActivities",
            columns: new[] { "OrganizationId", "AgentId", "Sequence" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_AgentActivities_OrganizationId_OccurredAtUtc",
            table: "AgentActivities",
            columns: new[] { "OrganizationId", "OccurredAtUtc" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "AgentActivities");
    }
}
