using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PeptideDataHomogenizer.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreateUpdatedArticle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Doi",
                table: "Articles",
                newName: "doi");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "doi",
                table: "Articles",
                newName: "Doi");
        }
    }
}
