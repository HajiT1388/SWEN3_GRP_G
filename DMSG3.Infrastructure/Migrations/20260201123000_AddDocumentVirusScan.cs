using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DMSG3.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentVirusScan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "virus_scan_completed_at",
                schema: "public",
                table: "documents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "virus_scan_error",
                schema: "public",
                table: "documents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "virus_scan_started_at",
                schema: "public",
                table: "documents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "virus_scan_status",
                schema: "public",
                table: "documents",
                type: "text",
                nullable: false,
                defaultValue: "NotScanned");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "virus_scan_completed_at",
                schema: "public",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "virus_scan_error",
                schema: "public",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "virus_scan_started_at",
                schema: "public",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "virus_scan_status",
                schema: "public",
                table: "documents");
        }
    }
}
