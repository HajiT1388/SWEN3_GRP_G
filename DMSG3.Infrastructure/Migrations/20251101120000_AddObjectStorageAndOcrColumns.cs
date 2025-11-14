using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DMSG3.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddObjectStorageAndOcrColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "storage_bucket",
                schema: "public",
                table: "documents",
                type: "text",
                nullable: false,
                defaultValue: "documents");

            migrationBuilder.AddColumn<string>(
                name: "storage_object_name",
                schema: "public",
                table: "documents",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ocr_status",
                schema: "public",
                table: "documents",
                type: "text",
                nullable: false,
                defaultValue: "Pending");

            migrationBuilder.AddColumn<string>(
                name: "ocr_text",
                schema: "public",
                table: "documents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ocr_started_at",
                schema: "public",
                table: "documents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ocr_completed_at",
                schema: "public",
                table: "documents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ocr_error",
                schema: "public",
                table: "documents",
                type: "text",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE public.documents
                SET storage_object_name = 'legacy-' || id::text,
                    storage_bucket = COALESCE(storage_bucket, 'documents'),
                    ocr_status = COALESCE(ocr_status, 'Pending')
                WHERE storage_object_name = '';
                """);

            migrationBuilder.DropColumn(
                name: "content",
                schema: "public",
                table: "documents");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "content",
                schema: "public",
                table: "documents",
                type: "bytea",
                nullable: false,
                defaultValue: Array.Empty<byte>());

            migrationBuilder.DropColumn(
                name: "storage_bucket",
                schema: "public",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "storage_object_name",
                schema: "public",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "ocr_status",
                schema: "public",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "ocr_text",
                schema: "public",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "ocr_started_at",
                schema: "public",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "ocr_completed_at",
                schema: "public",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "ocr_error",
                schema: "public",
                table: "documents");
        }
    }
}