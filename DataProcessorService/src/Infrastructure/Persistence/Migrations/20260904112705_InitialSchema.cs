using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DataProcessorService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sensors",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    sensor_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sensors", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "meter_readings",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    sensor_id = table.Column<int>(type: "integer", nullable: false),
                    collected_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    sensor_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    co2 = table.Column<int>(type: "integer", nullable: true),
                    pm25 = table.Column<int>(type: "integer", nullable: true),
                    humidity = table.Column<int>(type: "integer", nullable: true),
                    energy_kwh = table.Column<double>(type: "double precision", nullable: true),
                    motion_detected = table.Column<bool>(type: "boolean", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_meter_readings", x => x.id);
                    table.ForeignKey(
                        name: "FK_meter_readings_sensors_sensor_id",
                        column: x => x.sensor_id,
                        principalTable: "sensors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_meter_readings_collected_id",
                table: "meter_readings",
                columns: new[] { "collected_at", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_meter_readings_type_collected_desc",
                table: "meter_readings",
                columns: new[] { "sensor_type", "collected_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ux_meter_readings_sensor_collected",
                table: "meter_readings",
                columns: new[] { "sensor_id", "collected_at" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_sensors_name_type",
                table: "sensors",
                columns: new[] { "name", "sensor_type" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "meter_readings");

            migrationBuilder.DropTable(
                name: "sensors");
        }
    }
}
