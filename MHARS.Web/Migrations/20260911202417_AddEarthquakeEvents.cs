using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MHARS.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddEarthquakeEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EarthquakeEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExternalId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Source = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Magnitude = table.Column<double>(type: "float", nullable: true),
                    MagnitudeType = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Place = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Latitude = table.Column<double>(type: "float", nullable: false),
                    Longitude = table.Column<double>(type: "float", nullable: false),
                    DepthKm = table.Column<double>(type: "float", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UsgsUpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FetchedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Scope = table.Column<int>(type: "int", nullable: false),
                    DistanceFromDhakaKm = table.Column<double>(type: "float", nullable: false),
                    NearestDivision = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    NearestDivisionKm = table.Column<double>(type: "float", nullable: false),
                    TsunamiFlag = table.Column<bool>(type: "bit", nullable: false),
                    FeltReports = table.Column<int>(type: "int", nullable: true),
                    ReviewStatus = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    DetailsUrl = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EarthquakeEvents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EarthquakeEvents_ExternalId",
                table: "EarthquakeEvents",
                column: "ExternalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EarthquakeEvents_Scope_OccurredAtUtc",
                table: "EarthquakeEvents",
                columns: new[] { "Scope", "OccurredAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EarthquakeEvents");
        }
    }
}
