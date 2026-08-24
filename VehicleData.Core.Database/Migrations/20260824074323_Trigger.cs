using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VehicleData.Core.Database.Migrations
{
    /// <inheritdoc />
    public partial class Trigger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                                 CREATE OR REPLACE FUNCTION trim_telemetry_table()
                                 RETURNS TRIGGER AS $$
                                 DECLARE
                                     row_count INT;
                                 BEGIN
                                     SELECT COUNT(*) INTO row_count FROM "TelemetryMessage";
                                     
                                     IF row_count > 100 THEN
                                         DELETE FROM "TelemetryMessage"
                                         WHERE "TelemetryId" IN (
                                             SELECT "TelemetryId" FROM "TelemetryMessage"
                                             ORDER BY "Timestamp" ASC
                                             LIMIT (row_count - 100)
                                         );
                                     END IF;
                                     
                                     RETURN NEW;
                                 END;
                                 $$ LANGUAGE plpgsql;

                                 CREATE TRIGGER trigger_trim_telemetry
                                 AFTER INSERT ON "TelemetryMessage"
                                 FOR EACH STATEMENT
                                 EXECUTE FUNCTION trim_telemetry_table();
                                 """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                                 DROP TRIGGER IF EXISTS trigger_trim_telemetry ON "TelemetryMessage";

                                 DROP FUNCTION IF EXISTS trim_telemetry_table();
                                 """);
        }
    }
}
