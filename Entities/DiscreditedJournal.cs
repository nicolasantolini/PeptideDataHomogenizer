using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Entities
{
    public class DiscreditedJournal
    {
        [Key]
        [Column("id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Column("title")]
        [MaxLength(255)]
        public string Title { get; set; }

        [ForeignKey("project")]
        [Column("project_id")]
        public int ProjectId { get; set; }
        public Project Project { get; set; }

        [Column("discredited_reason")]
        [MaxLength(1000)]
        public string DiscreditedReason { get; set; } = string.Empty;

        [Column("discredited_by")]
        [MaxLength(255)]
        public string DiscreditedById { get; set; } = string.Empty;

    }

}
