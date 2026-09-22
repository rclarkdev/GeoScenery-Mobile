using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GeoScenery.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSceneLocationAndFixImageUrl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ImageUrl",
                table: "Scenes",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(2048)",
                oldMaxLength: 2048);

            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "Scenes",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "Scenes",
                type: "float",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "Scenes");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "Scenes");

            migrationBuilder.AlterColumn<string>(
                name: "ImageUrl",
                table: "Scenes",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");
        }
    }
}
