using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Entities.RegexData
{
    public class WaterModel
    {
        [Key]
        [Column("id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [Column("water_model_name")]
        [MaxLength(255)]
        public string WaterModelName { get; set; } = "";

        [Column("water_model_type")]
        [MaxLength(50)]
        public string WaterModelType { get; set; } = "";
    }
}
