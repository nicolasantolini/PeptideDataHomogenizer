using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Entities
{

    
    public record Article
    {
        [Key]
        [Column("doi")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [JsonPropertyName("doi")]
        public string Doi { get; set; }

        [Required]
        [Column("PubMedId")]
        [JsonPropertyName("pubMedId")]
        public string PubMedId { get; set; }

        [Required]
        [Column("title")]
        [MaxLength(600)]
        [JsonPropertyName("title")]
        public string Title { get; set; }

        [Required]
        [Column("journal")]
        [JsonPropertyName("journal")]
        public string Journal { get; set; }

        [Required]
        [Column("authors")]
        [JsonPropertyName("authors")]
        public string Authors { get; set; }

        [Required]
        [Column("abstract")]
        [JsonPropertyName("abstract")]
        public string Abstract { get; set; }

        [Column("publication_date")]
        [JsonPropertyName("publicationDate")]
        public DateTime PublicationDate { get; set; }

        [Required]
        [Column("discredited")]
        [JsonPropertyName("discredited")]
        public bool Discredited { get; set; } = false;

        [Required]
        [Column("discredited_reason")]
        [JsonPropertyName("discreditedReason")]
        public string DiscreditedReason { get; set; } = "";

        [Required]
        [Column("completed")]
        [JsonPropertyName("completed")]
        public bool Completed { get; set; } = false;

        [JsonPropertyName("proteinData")]
        public ICollection<ProteinData> ProteinData { get; set; } = new List<ProteinData>();

        [JsonPropertyName("chapters")]
        public ICollection<Chapter> Chapters { get; set; } = new List<Chapter>();
    }
}
