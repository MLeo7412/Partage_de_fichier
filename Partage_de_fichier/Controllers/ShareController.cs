using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Partage_de_fichier.Data;
using Partage_de_fichier.Models;
using Partage_de_fichier.ViewModels;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Partage_de_fichier.Controllers
{
    [Authorize]
    public class ShareController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ShareController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Affiche le formulaire de partage
        [HttpGet]
        public async Task<IActionResult> Index(int id)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var fichier = await _context.Fichiers.FirstOrDefaultAsync(f => f.IdFichier == id && f.IdProprietaire == userId);

            if (fichier == null) return NotFound("Fichier introuvable ou vous n'en êtes pas le propriétaire.");

            var model = new ShareViewModel
            {
                IdFichier = fichier.IdFichier,
                NomFichier = fichier.NomFichier
            };

            return View(model);
        }

        // Traite le partage sécurisé
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(ShareViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var proprietaireId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            // 1. Vérifier que le fichier appartient bien à l'utilisateur
            var accesProprietaire = await _context.PartagesAcces
                .Include(p => p.Destinataire) // Le propriétaire
                .FirstOrDefaultAsync(p => p.IdFichier == model.IdFichier && p.IdUtilisateur == proprietaireId);

            if (accesProprietaire == null) return Unauthorized("Accès refusé.");

            // 2. Trouver l'utilisateur cible
            var utilisateurCible = await _context.Utilisateurs
                .FirstOrDefaultAsync(u => u.NomUtilisateur == model.NomUtilisateurCible);

            if (utilisateurCible == null)
            {
                ModelState.AddModelError("", "L'utilisateur cible n'existe pas.");
                return View(model);
            }

            // Vérifier si le fichier est déjà partagé avec lui
            bool dejaPartage = await _context.PartagesAcces
                .AnyAsync(p => p.IdFichier == model.IdFichier && p.IdUtilisateur == utilisateurCible.IdUtilisateur);

            if (dejaPartage)
            {
                ModelState.AddModelError("", "Ce fichier est déjà partagé avec cet utilisateur.");
                return View(model);
            }

            try
            {
                // 3. Déchiffrer la clé AES avec la clé PRIVÉE du propriétaire
                string clePriveePem = Encoding.UTF8.GetString(Convert.FromBase64String(accesProprietaire.Destinataire.ClePriveeRsaChiffree));
                using RSA rsaProprietaire = RSA.Create();
                rsaProprietaire.ImportFromPem(clePriveePem);

                byte[] cleAesChiffreeBytes = Convert.FromBase64String(accesProprietaire.CleAesChiffree);
                byte[] cleAesEnClair = rsaProprietaire.Decrypt(cleAesChiffreeBytes, RSAEncryptionPadding.OaepSHA256);

                // 4. Re-chiffrer la clé AES avec la clé PUBLIQUE du destinataire
                using RSA rsaCible = RSA.Create();
                rsaCible.ImportFromPem(utilisateurCible.ClePubliqueRsa);
                byte[] nouvelleCleAesChiffree = rsaCible.Encrypt(cleAesEnClair, RSAEncryptionPadding.OaepSHA256);

                // 5. Sauvegarder le nouvel accès dans la base de données
                var nouvelAcces = new PartageAcces
                {
                    IdFichier = model.IdFichier,
                    IdUtilisateur = utilisateurCible.IdUtilisateur,
                    CleAesChiffree = Convert.ToBase64String(nouvelleCleAesChiffree)
                };

                _context.PartagesAcces.Add(nouvelAcces);
                await _context.SaveChangesAsync();

                TempData["MessageSucces"] = $"Le fichier a été partagé avec succès à {utilisateurCible.NomUtilisateur}.";
                return RedirectToAction("Index", "File");
            }
            catch (Exception)
            {
                ModelState.AddModelError("", "Une erreur cryptographique est survenue lors du partage.");
                return View(model);
            }
        }
    }
}