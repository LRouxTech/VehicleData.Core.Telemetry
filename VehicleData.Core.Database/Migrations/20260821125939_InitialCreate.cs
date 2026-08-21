using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VehicleData.Core.Database.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TelemetryMessage",
                columns: table => new
                {
                    TelemetryId = table.Column<Guid>(type: "uuid", nullable: false),
                    VehicleId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Latitude = table.Column<double>(type: "double precision", precision: 4, nullable: false),
                    Longitude = table.Column<double>(type: "double precision", precision: 4, nullable: false),
                    Speed = table.Column<double>(type: "double precision", precision: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TelemetryMessage", x => x.TelemetryId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TelemetryMessage_VehicleId_Timestamp",
                table: "TelemetryMessage",
                columns: new[] { "VehicleId", "Timestamp" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TelemetryMessage");
        }
    }
}
