using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DMSG3.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVirusScanAnalysisId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "virus_scan_analysis_id",
                schema: "public",
                table: "documents",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "virus_scan_analysis_id",
                schema: "public",
                table: "documents");
        }
    }
}
