using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GeoScenery.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSceneRatings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SceneRatings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SceneId = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    Rating = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SceneRatings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SceneRatings_Scenes_SceneId",
                        column: x => x.SceneId,
                        principalTable: "Scenes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SceneRatings_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SceneRatings_SceneId_UserId",
                table: "SceneRatings",
                columns: new[] { "SceneId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SceneRatings_UserId",
                table: "SceneRatings",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SceneRatings");
        }
    }
}
