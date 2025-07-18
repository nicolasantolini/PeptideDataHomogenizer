using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Entities
{
    public class ArticlePerProject
    {
        [Key]
        [Required]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [ForeignKey("ProjectId")]
        public Project Project { get; set; }
        [Column("ProjectId")]
        public int ProjectId { get; set; }

        [Required]
        [ForeignKey("ArticleId")]
        public Article Article { get; set; }
        [Column("ArticleId")]
        public string ArticleId { get; set; }

        [Column("IsDiscredited")]
        public bool IsDiscredited { get; set; } = false;

        [Column("DiscreditedReason")]
        [MaxLength(1000)]
        public string DiscreditedReason { get; set; } = string.Empty;

        [Column("IsApproved")]
        public bool IsApproved { get; set; } = false;

        [Column("datetime_approval")]
        public DateTime? DatetimeApproval { get; set; }

        [Column("approved_by")]
        [MaxLength(255)]
        public string ApprovedById { get; set; } = string.Empty;
    }
}
