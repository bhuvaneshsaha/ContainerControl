using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ContainerControl.Modules.Platform.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TeamQuotasAndCapacity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "host_capacity",
                schema: "platform",
                columns: table => new
                {
                    HostId = table.Column<Guid>(type: "uuid", nullable: false),
                    CpuCount = table.Column<long>(type: "bigint", nullable: false),
                    MemoryBytes = table.Column<long>(type: "bigint", nullable: false),
                    StorageBytes = table.Column<long>(type: "bigint", nullable: true),
                    ReadAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_host_capacity", x => x.HostId);
                });

            migrationBuilder.CreateTable(
                name: "team_quotas",
                schema: "platform",
                columns: table => new
                {
                    TeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    CpuMillicores = table.Column<long>(type: "bigint", nullable: false),
                    MemoryBytes = table.Column<long>(type: "bigint", nullable: false),
                    StorageBytes = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_team_quotas", x => x.TeamId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "host_capacity",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "team_quotas",
                schema: "platform");
        }
    }
}
