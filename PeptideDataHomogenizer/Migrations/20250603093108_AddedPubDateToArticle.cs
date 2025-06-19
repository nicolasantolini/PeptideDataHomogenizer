using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PeptideDataHomogenizer.Migrations
{
    /// <inheritdoc />
    public partial class AddedPubDateToArticle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "publicationDate",
                table: "Articles",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "publicationDate",
                table: "Articles");
        }
    }
}
