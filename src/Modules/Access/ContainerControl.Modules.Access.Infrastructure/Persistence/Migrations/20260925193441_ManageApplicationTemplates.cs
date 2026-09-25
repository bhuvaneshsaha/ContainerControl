using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ContainerControl.Modules.Access.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ManageApplicationTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "access",
                table: "permissions",
                columns: new[] { "Code", "Description", "DisplayName", "Module" },
                values: new object[] { "apps.templates.manage", "Publish and remove application templates.", "Manage application templates", "Applications" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "access",
                table: "permissions",
                keyColumn: "Code",
                keyValue: "apps.templates.manage");
        }
    }
}
