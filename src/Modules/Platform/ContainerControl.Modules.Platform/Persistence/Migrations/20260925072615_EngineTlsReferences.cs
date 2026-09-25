using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ContainerControl.Modules.Platform.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EngineTlsReferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CaRef",
                schema: "platform",
                table: "docker_hosts",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClientCertRef",
                schema: "platform",
                table: "docker_hosts",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClientKeyRef",
                schema: "platform",
                table: "docker_hosts",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CaRef",
                schema: "platform",
                table: "docker_hosts");

            migrationBuilder.DropColumn(
                name: "ClientCertRef",
                schema: "platform",
                table: "docker_hosts");

            migrationBuilder.DropColumn(
                name: "ClientKeyRef",
                schema: "platform",
                table: "docker_hosts");
        }
    }
}
