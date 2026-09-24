using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ContainerControl.Modules.Registries.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RegistryConnections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "registry_connections",
                schema: "registries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Server = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Environment = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    UsernamePath = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    PasswordPath = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    AccessKeyPath = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    SecretKeyPath = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    EcrTokenExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_registry_connections", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "registry_connections",
                schema: "registries");
        }
    }
}
