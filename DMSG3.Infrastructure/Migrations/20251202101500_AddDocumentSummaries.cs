using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DMSG3.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentSummaries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "summary_completed_at",
                schema: "public",
                table: "documents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "summary_error",
                schema: "public",
                table: "documents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "summary_status",
                schema: "public",
                table: "documents",
                type: "text",
                nullable: false,
                defaultValue: "Pending");

            migrationBuilder.AddColumn<string>(
                name: "summary_text",
                schema: "public",
                table: "documents",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "summary_completed_at",
                schema: "public",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "summary_error",
                schema: "public",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "summary_status",
                schema: "public",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "summary_text",
                schema: "public",
                table: "documents");
        }
    }
}
