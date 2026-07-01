using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CitizenPortal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProtocolYear : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "protocol_year",
                table: "Applications",
                type: "character varying(4)",
                maxLength: 4,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "protocol_year",
                table: "Applications");
        }
    }
}
