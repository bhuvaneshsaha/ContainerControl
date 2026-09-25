using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ContainerControl.Modules.Runtime.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StoredLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "log_cursors",
                schema: "runtime",
                columns: table => new
                {
                    ContainerId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    LastAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_log_cursors", x => x.ContainerId);
                });

            migrationBuilder.CreateTable(
                name: "log_lines",
                schema: "runtime",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContainerId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Service = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    RecordedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Text = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    TextHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_log_lines", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_log_lines_ApplicationId_RecordedAtUtc",
                schema: "runtime",
                table: "log_lines",
                columns: new[] { "ApplicationId", "RecordedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_log_lines_ContainerId_RecordedAtUtc_TextHash",
                schema: "runtime",
                table: "log_lines",
                columns: new[] { "ContainerId", "RecordedAtUtc", "TextHash" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "log_cursors",
                schema: "runtime");

            migrationBuilder.DropTable(
                name: "log_lines",
                schema: "runtime");
        }
    }
}
