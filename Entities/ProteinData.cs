using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Entities
{
    /// <summary>
    /// Represents simulation data for a protein, including software, method, and approval metadata.
    /// </summary>
    public record ProteinData
    {
        public ProteinData (ProteinData proteinData)
        {
            Id = proteinData.Id;
            ProteinId = proteinData.ProteinId;
            Classification = proteinData.Classification;
            Organism = proteinData.Organism;
            Method = proteinData.Method;
            SoftwareName = proteinData.SoftwareName;
            SoftwareVersion = proteinData.SoftwareVersion;
            WaterModel = proteinData.WaterModel;
            ForceField = proteinData.ForceField;
            SimulationMethod = proteinData.SimulationMethod;
            Temperature = proteinData.Temperature;
            Ions = proteinData.Ions;
            IonConcentration = proteinData.IonConcentration;
            SimulationLength = proteinData.SimulationLength;
            ArticleDoi = proteinData.ArticleDoi;
            Approved = proteinData.Approved;
            DatetimeApproval = proteinData.DatetimeApproval;
            ApprovedById = proteinData.ApprovedById;
        }

        [Key]
        [Column("id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [Column("protein_id")]
        public string ProteinId { get; set; }

        [Column("classification")]
        [MaxLength(255)]
        public string Classification { get; set; } = string.Empty;

        [Column("organism")]
        [MaxLength(255)]
        public string Organism { get; set; } = string.Empty;

        [Column("method")]
        [MaxLength(255)]
        public string Method { get; set; } = string.Empty;

        [Column("software_name")]
        [MaxLength(255)]
        public string SoftwareName { get; set; } = string.Empty;

        [Column("software_version")]
        [MaxLength(255)]
        public string SoftwareVersion { get; set; } = string.Empty;

        [Column("water_model")]
        [MaxLength(255)]
        public string WaterModel { get; set; } = string.Empty;

        [Column("force_field")]
        [MaxLength(255)]
        public string ForceField { get; set; } = string.Empty;
        [Column("simulation_method")]
        [MaxLength(255)]
        public string SimulationMethod { get; set; } = string.Empty;


        [Column("temperature")]
        public double Temperature { get; set; } = 0.0;

        [Column("ions")]
        [MaxLength(255)]
        public string Ions { get; set; } = string.Empty;

        [Column("ion_concentration")]
        public double IonConcentration { get; set; } = 0.0;

        [Column("simulation_length")]
        public double SimulationLength { get; set; } = 0.0;

        [Required]
        [Column("article_doi")]
        public string ArticleDoi { get; set; }

        [ForeignKey("ArticleDoi")]
        [JsonIgnore]
        public Article Article { get; set; }

        [Required]
        [Column("approved")]
        public bool Approved { get; set; } = false;

        [Column("datetime_approval")]
        public DateTime? DatetimeApproval { get; set; }

        [Column("approved_by")]
        [MaxLength(255)]
        public string ApprovedById { get; set; } = string.Empty;
    }
}
