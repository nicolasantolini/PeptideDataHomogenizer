using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PeptideDataHomogenizer.Migrations
{
    /// <inheritdoc />
    public partial class EditProteinDataFK : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProteinData_Articles_ArticleDoi",
                table: "ProteinData");

            migrationBuilder.DropColumn(
                name: "article_id",
                table: "ProteinData");

            migrationBuilder.RenameColumn(
                name: "ArticleDoi",
                table: "ProteinData",
                newName: "article_doi");

            migrationBuilder.RenameIndex(
                name: "IX_ProteinData_ArticleDoi",
                table: "ProteinData",
                newName: "IX_ProteinData_article_doi");

            migrationBuilder.AlterColumn<string>(
                name: "protein_id",
                table: "ProteinData",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddForeignKey(
                name: "FK_ProteinData_Articles_article_doi",
                table: "ProteinData",
                column: "article_doi",
                principalTable: "Articles",
                principalColumn: "doi",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProteinData_Articles_article_doi",
                table: "ProteinData");

            migrationBuilder.RenameColumn(
                name: "article_doi",
                table: "ProteinData",
                newName: "ArticleDoi");

            migrationBuilder.RenameIndex(
                name: "IX_ProteinData_article_doi",
                table: "ProteinData",
                newName: "IX_ProteinData_ArticleDoi");

            migrationBuilder.AlterColumn<int>(
                name: "protein_id",
                table: "ProteinData",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<int>(
                name: "article_id",
                table: "ProteinData",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddForeignKey(
                name: "FK_ProteinData_Articles_ArticleDoi",
                table: "ProteinData",
                column: "ArticleDoi",
                principalTable: "Articles",
                principalColumn: "doi",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
