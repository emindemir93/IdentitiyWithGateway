using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Api.Migrations
{
    public partial class v1_User : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DateOfBirth",
                schema: "Identity",
                table: "Users",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "Gender",
                schema: "Identity",
                table: "Users",
                type: "nvarchar(250)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdentityNumber",
                schema: "Identity",
                table: "Users",
                type: "nvarchar(100)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                schema: "Identity",
                table: "Users",
                type: "nvarchar(250)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RoleType",
                schema: "Identity",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DateOfBirth",
                schema: "Identity",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Gender",
                schema: "Identity",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IdentityNumber",
                schema: "Identity",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Name",
                schema: "Identity",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "RoleType",
                schema: "Identity",
                table: "Users");
        }
    }
}
