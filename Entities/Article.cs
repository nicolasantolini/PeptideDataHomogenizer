using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Entities
{

    
public record Article
    {
        [Key]
        [Column("doi", TypeName = "nvarchar(255)")]
        [Required]
        [MaxLength(255)]
        public string Doi { get; set; } = string.Empty;

        [JsonPropertyName("pubMedId")]
        [MaxLength(255)]
        public string PubMedId { get; set; } = string.Empty;

        [JsonPropertyName("title")]
        [MaxLength(600)]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("journal")]
        [MaxLength(255)]
        public string Journal { get; set; } = string.Empty;

        [JsonPropertyName("authors")]
        public string Authors { get; set; } = string.Empty;

        [JsonPropertyName("abstract")]
        public string Abstract { get; set; } = string.Empty;

        [JsonPropertyName("publicationDate")]
        public DateTime PublicationDate { get; set; }

        [JsonPropertyName("proteinData")]
        public ICollection<ProteinData> ProteinData { get; set; } = new List<ProteinData>();

        [JsonPropertyName("chapters")]
        public ICollection<Chapter> Chapters { get; set; } = new List<Chapter>();

        [JsonPropertyName("tables")]
        public List<ExtractedTable> Tables { get; set; } = new List<ExtractedTable>();

        [JsonPropertyName("images")]
        public List<ImageHolder> Images { get; set; } = new List<ImageHolder>();
    }

    
}
