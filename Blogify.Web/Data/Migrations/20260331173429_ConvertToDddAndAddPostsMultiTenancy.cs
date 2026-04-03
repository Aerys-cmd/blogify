using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Blogify.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class ConvertToDddAndAddPostsMultiTenancy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Posts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    Slug = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    PublishedRevisionId = table.Column<Guid>(type: "uuid", nullable: true),
                    DraftRevisionId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Posts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PostRevisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    PostId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    SeoTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SeoKeywords = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SeoDescription = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsDraft = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    ModifiedBy = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PostRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PostRevisions_Posts_PostId",
                        column: x => x.PostId,
                        principalTable: "Posts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Blogs_Subdomain",
                table: "Blogs",
                column: "Subdomain",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PostRevisions_PostId",
                table: "PostRevisions",
                column: "PostId");

            migrationBuilder.CreateIndex(
                name: "IX_PostRevisions_TenantId_PostId_CreatedAt",
                table: "PostRevisions",
                columns: new[] { "TenantId", "PostId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Posts_TenantId_Slug",
                table: "Posts",
                columns: new[] { "TenantId", "Slug" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PostRevisions");

            migrationBuilder.DropTable(
                name: "Posts");

            migrationBuilder.DropIndex(
                name: "IX_Blogs_Subdomain",
                table: "Blogs");
        }
    }
}
