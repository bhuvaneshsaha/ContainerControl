using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ContainerControl.Modules.Applications.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ApplicationTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "app_templates",
                schema: "applications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    NameKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Description = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    ComposeYaml = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_app_templates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_app_templates_NameKey",
                schema: "applications",
                table: "app_templates",
                column: "NameKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "app_templates",
                schema: "applications");
        }
    }
}
