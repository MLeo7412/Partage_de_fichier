using System.ComponentModel.DataAnnotations;

namespace Partage_de_fichier.ViewModels
{
    public class RegisterViewModel
    {
        [Required(ErrorMessage = "Le nom d'utilisateur est requis.")]
        [StringLength(50)]
        public string NomUtilisateur { get; set; } = null!;

        [Required(ErrorMessage = "Le mot de passe est requis.")]
        [DataType(DataType.Password)]
        public string MotDePasse { get; set; } = null!;

        [Required(ErrorMessage = "Veuillez confirmer le mot de passe.")]
        [DataType(DataType.Password)]
        [Compare("MotDePasse", ErrorMessage = "Les mots de passe ne correspondent pas.")]
        public string ConfirmationMotDePasse { get; set; } = null!;
    }
}