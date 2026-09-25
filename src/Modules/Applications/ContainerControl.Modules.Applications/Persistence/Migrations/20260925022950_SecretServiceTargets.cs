using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ContainerControl.Modules.Applications.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SecretServiceTargets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Existing secret_references rows are not backfilled.
            // No row in this table means the secret is injected into no service.
            migrationBuilder.CreateTable(
                name: "secret_service_targets",
                schema: "applications",
                columns: table => new
                {
                    SecretId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceName = table.Column<string>(type: "character varying(63)", maxLength: 63, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_secret_service_targets", x => new { x.SecretId, x.ServiceName });
                    table.ForeignKey(
                        name: "FK_secret_service_targets_secret_references_SecretId",
                        column: x => x.SecretId,
                        principalSchema: "applications",
                        principalTable: "secret_references",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "secret_service_targets",
                schema: "applications");
        }
    }
}
