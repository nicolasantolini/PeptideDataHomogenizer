using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Entities
{
    public class Project
    {
        [Key]
        [Column("id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [Column("name")]
        [MaxLength(255)]
        public string Name { get; set; } = string.Empty;

        [Column("description")]
        [MaxLength(1000)]
        public string Description { get; set; } = string.Empty;

        //[Column("logo_path")]
        //[MaxLength(500)]
        //public string LogoPath { get; set; } = string.Empty;

        [MaxLength(25 * 1024 * 1024)]
        [Column("logo_data")]
        public byte[] LogoData { get; set; }
        [Column("content_type")]
        [MaxLength(50)]
        public string ContentType { get; set; }

        [ForeignKey("OrganizationId")]
        public Organization Organization { get; set; }

        [Required]
        [Column("organization_id")]
        public int OrganizationId { get; set; }


    }
}
