using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ContainerControl.Modules.Platform.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AlertSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "alert_settings",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    WebhookUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Recipients = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alert_settings", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "alert_settings",
                schema: "platform");
        }
    }
}
