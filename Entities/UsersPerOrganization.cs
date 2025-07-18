using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Entities
{
    public class UsersPerOrganization
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [ForeignKey("Organization")]
        [Column("organization_id")]
        public int OrganizationId { get; set; }
        public virtual Organization Organization { get; set; }

        [ForeignKey("User")]
        [Column("user_id")]
        public string UserId { get; set; }

        [Column("role")]
        [MaxLength(50)]
        public string Role { get; set; } = string.Empty;
    }
}
