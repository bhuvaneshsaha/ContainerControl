using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ContainerControl.Modules.Applications.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AppsAndSecrets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "apps",
                schema: "applications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    HostId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Environment = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Image = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CommandJson = table.Column<string>(type: "text", nullable: true),
                    ComposeYaml = table.Column<string>(type: "text", nullable: true),
                    InternalPort = table.Column<int>(type: "integer", nullable: true),
                    Hostname = table.Column<string>(type: "character varying(253)", maxLength: 253, nullable: true),
                    Exposed = table.Column<bool>(type: "boolean", nullable: false),
                    RequiresApproval = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_apps", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "secret_references",
                schema: "applications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    Environment = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    InjectionMode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_secret_references", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_apps_TeamId_Name_Environment",
                schema: "applications",
                table: "apps",
                columns: new[] { "TeamId", "Name", "Environment" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_secret_references_TeamId_Environment_Name",
                schema: "applications",
                table: "secret_references",
                columns: new[] { "TeamId", "Environment", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "apps",
                schema: "applications");

            migrationBuilder.DropTable(
                name: "secret_references",
                schema: "applications");
        }
    }
}
