using Partage_de_fichier.Models;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Partage_de_fichier.Models
{
    public class Fichier
    {
        [Key]
        public int IdFichier { get; set; }

        [Required]
        [StringLength(255)]
        public string NomFichier { get; set; } = null!;

        [Required]
        public string CheminServeur { get; set; } = null!; // Chemin vers le fichier chiffré sur le disque

        [Required]
        public string IvAes { get; set; } = null!; // Vecteur d'Initialisation (IV) indispensable pour déchiffrer l'AES

        [Required]
        public int IdProprietaire { get; set; }

        [ForeignKey("IdProprietaire")]
        public Utilisateur Proprietaire { get; set; } = null!;

        // Propriété de navigation
        public ICollection<PartageAcces> AccesPartages { get; set; } = new List<PartageAcces>();
    }
}