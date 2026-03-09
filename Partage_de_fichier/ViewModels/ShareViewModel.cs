using System.ComponentModel.DataAnnotations;

namespace Partage_de_fichier.ViewModels
{
    public class ShareViewModel
    {
        [Required]
        public int IdFichier { get; set; }

        public string NomFichier { get; set; } = string.Empty;

        [Required(ErrorMessage = "Veuillez indiquer le nom de l'utilisateur cible.")]
        [Display(Name = "Partager avec (Nom d'utilisateur)")]
        public string NomUtilisateurCible { get; set; } = null!;
    }
}