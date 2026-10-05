using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenParking.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAssignedCameraIdToSlots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AssignedCameraId",
                table: "Slots",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AssignedSensorId",
                table: "Slots",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BoundingBoxJson",
                table: "Slots",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Floor",
                table: "Slots",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "AnchorNorthWestLat",
                table: "FloorPlans",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "AnchorNorthWestLng",
                table: "FloorPlans",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "AnchorSouthEastLat",
                table: "FloorPlans",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "AnchorSouthEastLng",
                table: "FloorPlans",
                type: "double precision",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AssignedCameraId",
                table: "Slots");

            migrationBuilder.DropColumn(
                name: "AssignedSensorId",
                table: "Slots");

            migrationBuilder.DropColumn(
                name: "BoundingBoxJson",
                table: "Slots");

            migrationBuilder.DropColumn(
                name: "Floor",
                table: "Slots");

            migrationBuilder.DropColumn(
                name: "AnchorNorthWestLat",
                table: "FloorPlans");

            migrationBuilder.DropColumn(
                name: "AnchorNorthWestLng",
                table: "FloorPlans");

            migrationBuilder.DropColumn(
                name: "AnchorSouthEastLat",
                table: "FloorPlans");

            migrationBuilder.DropColumn(
                name: "AnchorSouthEastLng",
                table: "FloorPlans");
        }
    }
}
