using Partage_de_fichier.Models;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Partage_de_fichier.Models
{
    public class Utilisateur
    {
        [Key]
        public int IdUtilisateur { get; set; }

        [Required]
        [StringLength(50)]
        public string NomUtilisateur { get; set; } = null!;

        [Required]
        public string MotDePasseHash { get; set; } = null!; // Contiendra le hash Bcrypt 

        [Required]
        public string ClePubliqueRsa { get; set; } = null!; // Utilisée par les autres pour lui partager un fichier

        [Required]
        public string ClePriveeRsaChiffree { get; set; } = null!; // Chiffrée symétriquement avec un dérivé du mot de passe

        // Propriétés de navigation Entity Framework
        public ICollection<Fichier> FichiersPossedes { get; set; } = new List<Fichier>();
        public ICollection<PartageAcces> FichiersPartagesAvecMoi { get; set; } = new List<PartageAcces>();
    }
}