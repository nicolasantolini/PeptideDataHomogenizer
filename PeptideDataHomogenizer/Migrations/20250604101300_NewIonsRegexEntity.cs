using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PeptideDataHomogenizer.Migrations
{
    /// <inheritdoc />
    public partial class NewIonsRegexEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "publicationDate",
                table: "Articles",
                newName: "publication_date");

            migrationBuilder.CreateTable(
                name: "Ions",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ion_name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ions", x => x.id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Ions");

            migrationBuilder.RenameColumn(
                name: "publication_date",
                table: "Articles",
                newName: "publicationDate");
        }
    }
}
