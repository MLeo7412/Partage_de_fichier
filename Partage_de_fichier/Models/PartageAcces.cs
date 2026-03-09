using Partage_de_fichier.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Partage_de_fichier.Models
{
    public class PartageAcces
    {
        [Key]
        public int IdPartage { get; set; } 

        [Required]
        public int IdFichier { get; set; }

        [ForeignKey("IdFichier")]
        public Fichier Fichier { get; set; } = null!;

        [Required]
        public int IdUtilisateur { get; set; } // L'utilisateur qui reçoit l'accès

        [ForeignKey("IdUtilisateur")]
        public Utilisateur Destinataire { get; set; } =null!;

        [Required]
        public string CleAesChiffree { get; set; } =null!; // La clé AES du fichier, chiffrée avec la clé RSA publique du destinataire
    }
}
