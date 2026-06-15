using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Blogify.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantPublicLanguage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PublicLanguage",
                table: "Blogs",
                type: "TEXT",
                maxLength: 2,
                nullable: false,
                defaultValue: "tr");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PublicLanguage",
                table: "Blogs");
        }
    }
}
