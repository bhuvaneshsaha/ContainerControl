using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ContainerControl.Modules.Registries.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialRegistries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "registries");

            migrationBuilder.CreateTable(
                name: "module_boundary",
                schema: "registries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ModuleName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_module_boundary", x => x.Id);
                });

            migrationBuilder.InsertData(
                schema: "registries",
                table: "module_boundary",
                columns: new[] { "Id", "ModuleName" },
                values: new object[] { 1, "Registries" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "module_boundary",
                schema: "registries");
        }
    }
}
