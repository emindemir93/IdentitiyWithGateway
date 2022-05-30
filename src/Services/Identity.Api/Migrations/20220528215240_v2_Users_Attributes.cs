using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Api.Migrations
{
    public partial class v2_Users_Attributes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CustomerId",
                schema: "Identity",
                table: "Users",
                type: "nvarchar(250)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InternalOrganizationId",
                schema: "Identity",
                table: "Users",
                type: "nvarchar(250)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MainPositionId",
                schema: "Identity",
                table: "Users",
                type: "nvarchar(250)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PositionTypeId",
                schema: "Identity",
                table: "Users",
                type: "nvarchar(250)",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CustomerId",
                schema: "Identity",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "InternalOrganizationId",
                schema: "Identity",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "MainPositionId",
                schema: "Identity",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PositionTypeId",
                schema: "Identity",
                table: "Users");
        }
    }
}
