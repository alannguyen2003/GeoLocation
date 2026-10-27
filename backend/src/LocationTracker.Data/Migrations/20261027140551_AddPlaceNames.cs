using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LocationTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPlaceNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "city",
                table: "location_points",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "country",
                table: "location_points",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "country_code",
                table: "location_points",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "district",
                table: "location_points",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "region",
                table: "location_points",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "city",
                table: "location_points");

            migrationBuilder.DropColumn(
                name: "country",
                table: "location_points");

            migrationBuilder.DropColumn(
                name: "country_code",
                table: "location_points");

            migrationBuilder.DropColumn(
                name: "district",
                table: "location_points");

            migrationBuilder.DropColumn(
                name: "region",
                table: "location_points");
        }
    }
}
