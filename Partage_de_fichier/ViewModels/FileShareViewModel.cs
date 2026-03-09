using Partage_de_fichier.Models;

namespace Partage_de_fichier.ViewModels
{
    public class FileShareViewModel
    {
        public List<Fichier> MesFichiers { get; set; } = new();
        public List<Fichier> FichiersPartages { get; set; } = new();
    }
}