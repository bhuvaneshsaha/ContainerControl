using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ContainerControl.Modules.Delivery.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Deployments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "deployments",
                schema: "delivery",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Image = table.Column<string>(type: "text", nullable: true),
                    CommandJson = table.Column<string>(type: "text", nullable: true),
                    ComposeYaml = table.Column<string>(type: "text", nullable: true),
                    InternalPort = table.Column<int>(type: "integer", nullable: true),
                    Hostname = table.Column<string>(type: "character varying(253)", maxLength: 253, nullable: true),
                    Exposed = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Error = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_deployments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "worker_lease",
                schema: "delivery",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_worker_lease", x => x.Id);
                });

            migrationBuilder.InsertData(
                schema: "delivery",
                table: "worker_lease",
                columns: new[] { "Id", "ExpiresAtUtc", "OwnerId" },
                values: new object[] { 1, null, null });

            migrationBuilder.CreateIndex(
                name: "IX_deployments_ApplicationId_CreatedAtUtc",
                schema: "delivery",
                table: "deployments",
                columns: new[] { "ApplicationId", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "deployments",
                schema: "delivery");

            migrationBuilder.DropTable(
                name: "worker_lease",
                schema: "delivery");
        }
    }
}
