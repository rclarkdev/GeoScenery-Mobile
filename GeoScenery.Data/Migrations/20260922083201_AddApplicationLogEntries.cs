using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GeoScenery.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddApplicationLogEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppLogEntries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Level = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    EventName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    HttpMethod = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    RequestPath = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    StatusCode = table.Column<int>(type: "int", nullable: true),
                    UserId = table.Column<long>(type: "bigint", nullable: true),
                    DurationMilliseconds = table.Column<long>(type: "bigint", nullable: true),
                    PropertiesJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppLogEntries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppLogEntries_CorrelationId",
                table: "AppLogEntries",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_AppLogEntries_CreatedAt",
                table: "AppLogEntries",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AppLogEntries_UserId",
                table: "AppLogEntries",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppLogEntries");
        }
    }
}
