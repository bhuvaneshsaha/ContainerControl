using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ContainerControl.Modules.Delivery.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TrafficSlots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Mode",
                schema: "delivery",
                table: "deployments",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "replace");

            migrationBuilder.CreateTable(
                name: "traffic_slots",
                schema: "delivery",
                columns: table => new
                {
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    LiveSlot = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CandidateSlot = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    CandidatePercent = table.Column<int>(type: "integer", nullable: false),
                    PreviousSlot = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_traffic_slots", x => x.ApplicationId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "traffic_slots",
                schema: "delivery");

            migrationBuilder.DropColumn(
                name: "Mode",
                schema: "delivery",
                table: "deployments");
        }
    }
}
