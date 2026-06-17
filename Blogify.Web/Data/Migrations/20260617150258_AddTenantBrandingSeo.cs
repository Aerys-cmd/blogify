using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Blogify.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantBrandingSeo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "FaviconMediaId",
                table: "Blogs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LogoMediaId",
                table: "Blogs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MetaTitle",
                table: "Blogs",
                type: "TEXT",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SocialPreviewImageMediaId",
                table: "Blogs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Blogs_FaviconMediaId",
                table: "Blogs",
                column: "FaviconMediaId");

            migrationBuilder.CreateIndex(
                name: "IX_Blogs_LogoMediaId",
                table: "Blogs",
                column: "LogoMediaId");

            migrationBuilder.CreateIndex(
                name: "IX_Blogs_SocialPreviewImageMediaId",
                table: "Blogs",
                column: "SocialPreviewImageMediaId");

            migrationBuilder.AddForeignKey(
                name: "FK_Blogs_Media_FaviconMediaId",
                table: "Blogs",
                column: "FaviconMediaId",
                principalTable: "Media",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Blogs_Media_LogoMediaId",
                table: "Blogs",
                column: "LogoMediaId",
                principalTable: "Media",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Blogs_Media_SocialPreviewImageMediaId",
                table: "Blogs",
                column: "SocialPreviewImageMediaId",
                principalTable: "Media",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Blogs_Media_FaviconMediaId",
                table: "Blogs");

            migrationBuilder.DropForeignKey(
                name: "FK_Blogs_Media_LogoMediaId",
                table: "Blogs");

            migrationBuilder.DropForeignKey(
                name: "FK_Blogs_Media_SocialPreviewImageMediaId",
                table: "Blogs");

            migrationBuilder.DropIndex(
                name: "IX_Blogs_FaviconMediaId",
                table: "Blogs");

            migrationBuilder.DropIndex(
                name: "IX_Blogs_LogoMediaId",
                table: "Blogs");

            migrationBuilder.DropIndex(
                name: "IX_Blogs_SocialPreviewImageMediaId",
                table: "Blogs");

            migrationBuilder.DropColumn(
                name: "FaviconMediaId",
                table: "Blogs");

            migrationBuilder.DropColumn(
                name: "LogoMediaId",
                table: "Blogs");

            migrationBuilder.DropColumn(
                name: "MetaTitle",
                table: "Blogs");

            migrationBuilder.DropColumn(
                name: "SocialPreviewImageMediaId",
                table: "Blogs");
        }
    }
}
