using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Entities
{
    public class Chapter
    {
        [Key]
        [Column("id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [Column("title")]
        [MaxLength(255)]
        public string Title { get; set; }

        [Required]
        [Column("content")]
        [MaxLength(int.MaxValue)]
        public string Content { get; set; }

        [Required]
        [Column("index")]
        public int Index { get; set; }

        [Required]
        [Column("article_doi")]
        public string ArticleDoi { get; set; }

        [ForeignKey("ArticleDoi")]
        [JsonIgnore]
        public Article Article { get; set; }
    }

}
