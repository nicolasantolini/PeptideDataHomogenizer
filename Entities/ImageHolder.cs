using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Entities
{
    public class ImageHolder
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("id")]
        public int Id { get; set; }

        [Column("caption")]
        public string Caption { get; set; } = string.Empty;

        [MaxLength(25 * 1024 * 1024)]
        [Column("image_data")]
        public byte[] ImageData { get; set; }

        [Column("file_name")]
        public string FileName { get; set; } = "";
        [Column("content_type")]
        public string ContentType { get; set; } = "";

        [ForeignKey("article_doi")]
        [Column("article_doi")]
        public string ArticleDoi { get; set; }

    }
}
