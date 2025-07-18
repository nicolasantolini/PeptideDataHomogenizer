using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Entities
{
    public class ExtractedTable
    {
        [Key]
        [Column("id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Column("caption")]
        public string Caption { get; set; } = string.Empty;

        [Column("tableJson")]
        public List<Dictionary<string, string>> Rows { get; set; } = new();

        [ForeignKey("article_doi")]
        [Column("article_doi")]
        public string ArticleDoi { get; set; }
    }
}