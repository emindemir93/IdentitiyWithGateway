using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Api.Migrations.ConfigurationDb
{
    public partial class IdentityScopeSeed : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {

            migrationBuilder.Sql(@"INSERT INTO IdentityDb.[Identity].ApiResourceScope (Scope, ApiResourceId)
                                                                VALUES (N'Identity', (select top 1 Id from IdentityDb.[Identity].ApiResources where Name = 'Identity'));");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
