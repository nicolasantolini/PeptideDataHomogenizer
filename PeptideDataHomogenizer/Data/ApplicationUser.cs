using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PeptideDataHomogenizer.Data;

public class ApplicationUser : IdentityUser
{
    [ProtectedPersonalData]
    [Column("first_name")]
    public string FirstName { get; set; } = string.Empty;
    [ProtectedPersonalData]
    [Column("last_name")]
    public string LastName { get; set; } = string.Empty;

    [ProtectedPersonalData]
    [Column("title")]
    public string Title { get; set; } = string.Empty;

    [MaxLength(500)]
    [Column("registration_token")]
    public long? RegistrationToken { get; set; } = null;

    [Column("registration_token_expiration")]
    public DateTime? RegistrationTokenExpiration { get; set; } = null;

    [Column("has_registered")]
    public bool HasRegistered { get; set; } = false;

    [NotMapped]
    public string FullName => $"{FirstName} {LastName}";

    [NotMapped]
    public string Initials
    {
        get
        {
            var firstInitial = !string.IsNullOrEmpty(FirstName) ? FirstName[0].ToString() : string.Empty;
            var lastInitial = !string.IsNullOrEmpty(LastName) ? LastName[0].ToString() : string.Empty;
            return $"{firstInitial}{lastInitial}".ToUpperInvariant();
        }
    }

    [NotMapped]
    public string ExtendedFullName => Title != string.Empty ? $"{Title} {FirstName} {LastName}" : $"{FirstName} {LastName}";




}

