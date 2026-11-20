using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PitCrm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDemandVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "Demands",
                type: "integer",
                nullable: false,
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Version",
                table: "Demands");
        }
    }
}
