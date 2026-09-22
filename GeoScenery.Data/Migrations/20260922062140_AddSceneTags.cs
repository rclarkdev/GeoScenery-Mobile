using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GeoScenery.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSceneTags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SceneTags",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SceneId = table.Column<long>(type: "bigint", nullable: false),
                    Tag = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SceneTags", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SceneTags_Scenes_SceneId",
                        column: x => x.SceneId,
                        principalTable: "Scenes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SceneTags_SceneId_Tag",
                table: "SceneTags",
                columns: new[] { "SceneId", "Tag" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SceneTags");
        }
    }
}
