using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Entities
{
    public class Organization
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("id")]
        public int Id { get; set; }
        
        [Column("name")]
        [Required]
        [MaxLength(255)]
        public string Name { get; set; } = string.Empty;

        [Column("description")]
        [MaxLength(1000)]
        public string Description { get; set; } = string.Empty;

        [MaxLength(25 * 1024 * 1024)]
        [Column("logo_data")]
        public byte[] LogoData { get; set; }
        [Column("content_type")]
        [MaxLength(50)]
        public string ContentType { get; set; } // e.g., "image/jpeg"

        [Column("website_url")]
        [MaxLength(500)]
        public string WebsiteUrl { get; set; } = string.Empty;

    }
}
