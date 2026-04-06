using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Blogify.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMediaMetadataAndPostCoverImageFk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add metadata columns to Media
            migrationBuilder.AddColumn<string>(
                name: "AltText",
                table: "Media",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "Media",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Media",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ThumbnailUrl",
                table: "Media",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WidthPx",
                table: "Media",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HeightPx",
                table: "Media",
                type: "integer",
                nullable: true);

            // Add CoverImageId FK column to Posts
            migrationBuilder.AddColumn<Guid>(
                name: "CoverImageId",
                table: "Posts",
                type: "uuid",
                nullable: true);

            // Backfill CoverImageId from FeaturedImageUrl → Media.Url match
            migrationBuilder.Sql(
                """
                UPDATE "Posts" p
                SET "CoverImageId" = m."Id"
                FROM "Media" m
                WHERE p."FeaturedImageUrl" = m."Url"
                  AND p."FeaturedImageUrl" IS NOT NULL
                  AND m."DeletedAt" IS NULL;
                """);

            // Drop old FeaturedImageUrl column
            migrationBuilder.DropColumn(
                name: "FeaturedImageUrl",
                table: "Posts");

            // Add FK: Posts.CoverImageId → Media.Id ON DELETE SET NULL
            migrationBuilder.AddForeignKey(
                name: "FK_Posts_Media_CoverImageId",
                table: "Posts",
                column: "CoverImageId",
                principalTable: "Media",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // Add index on Posts.CoverImageId
            migrationBuilder.CreateIndex(
                name: "IX_Posts_CoverImageId",
                table: "Posts",
                column: "CoverImageId");

            // Add composite index on Media (BlogId, UploadedAt)
            migrationBuilder.CreateIndex(
                name: "IX_Media_BlogId_UploadedAt",
                table: "Media",
                columns: new[] { "BlogId", "UploadedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Posts_Media_CoverImageId",
                table: "Posts");

            migrationBuilder.DropIndex(
                name: "IX_Posts_CoverImageId",
                table: "Posts");

            migrationBuilder.DropIndex(
                name: "IX_Media_BlogId_UploadedAt",
                table: "Media");

            migrationBuilder.DropColumn(
                name: "CoverImageId",
                table: "Posts");

            migrationBuilder.AddColumn<string>(
                name: "FeaturedImageUrl",
                table: "Posts",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.DropColumn(name: "AltText", table: "Media");
            migrationBuilder.DropColumn(name: "Title", table: "Media");
            migrationBuilder.DropColumn(name: "Description", table: "Media");
            migrationBuilder.DropColumn(name: "ThumbnailUrl", table: "Media");
            migrationBuilder.DropColumn(name: "WidthPx", table: "Media");
            migrationBuilder.DropColumn(name: "HeightPx", table: "Media");
        }
    }
}

