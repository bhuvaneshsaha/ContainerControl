using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ContainerControl.Modules.Applications.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AllowDatabaseImages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AllowDatabaseImages",
                schema: "applications",
                table: "apps",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AllowDatabaseImages",
                schema: "applications",
                table: "apps");
        }
    }
}
