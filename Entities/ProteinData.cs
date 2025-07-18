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
            WaterModelType = proteinData.WaterModelType;
            ForceField = proteinData.ForceField;
            SimulationMethod = proteinData.SimulationMethod;
            Temperature = proteinData.Temperature;
            Ions = proteinData.Ions;
            IonConcentration = proteinData.IonConcentration;
            SimulationLength = proteinData.SimulationLength;
            Kd = proteinData.Kd;
            KOff = proteinData.KOff;
            KOn = proteinData.KOn;
            FreeBindingEnergy = proteinData.FreeBindingEnergy;
            Residue = proteinData.Residue;
            Binder = proteinData.Binder;
            ProjectId = proteinData.ProjectId;
            ArticleDoi = proteinData.ArticleDoi;
            Approved = proteinData.Approved;
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

        [Column("residue")]
        [MaxLength(255)]
        public string? Residue { get; set; } = string.Empty;

        [Column("binder")]
        [MaxLength(255)]
        public string Binder { get; set; } = string.Empty;

        [Column("software_name")]
        [MaxLength(255)]
        public string SoftwareName { get; set; } = string.Empty;

        [Column("software_version")]
        [MaxLength(255)]
        public string SoftwareVersion { get; set; } = string.Empty;

        [Column("water_model")]
        [MaxLength(255)]
        public string WaterModel { get; set; } = string.Empty;

        [Column("water_model_type")]
        [MaxLength(50)]
        public string WaterModelType { get; set; } = string.Empty;

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

        [Column("Kd")]
        public double Kd { get; set; } = 0.0;

        [Column("KOff")]
        public double KOff { get; set; } = 0.0;

        [Column("KOn")]
        public double KOn { get; set; } = 0.0;

        [Column("free_binding_energy")]
        public double FreeBindingEnergy { get; set; } = 0.0;

        [Required]
        [ForeignKey("ProjectId")]
        public Project Project { get; set; }
        [Column("ProjectId")]
        public int ProjectId { get; set; }

        [Required]
        [ForeignKey("ArticleDoi")]
        public Article Article { get; set; } = new Article();
        [Column("ArticleDoi")]
        public string ArticleDoi { get; set; } = string.Empty;

        [Column("Approved")]
        public bool Approved { get; set; } = false;


        public bool AreEquals(ProteinData other)
        {
            if (other == null) return false;
            return ProteinId == other.ProteinId &&
                   Classification == other.Classification &&
                   Organism == other.Organism &&
                   Method == other.Method &&
                   Residue == other.Residue &&
                     Binder == other.Binder &&
                   SoftwareName == other.SoftwareName &&
                   SoftwareVersion == other.SoftwareVersion &&
                   WaterModel == other.WaterModel &&
                   WaterModelType == other.WaterModelType &&
                   ForceField == other.ForceField &&
                   SimulationMethod == other.SimulationMethod &&
                   Temperature.Equals(other.Temperature) &&
                   Ions == other.Ions &&
                   IonConcentration.Equals(other.IonConcentration) &&
                   SimulationLength.Equals(other.SimulationLength) &&
                   Kd.Equals(other.Kd) &&
                   KOff.Equals(other.KOff) &&
                   KOn.Equals(other.KOn) &&
                   FreeBindingEnergy.Equals(other.FreeBindingEnergy) &&
                   ArticleDoi == other.ArticleDoi &&
                   Approved == other.Approved;
        }

    }
}
