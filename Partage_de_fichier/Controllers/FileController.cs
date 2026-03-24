using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Partage_de_fichier.Data;
using Partage_de_fichier.Models;
using Partage_de_fichier.ViewModels;
using System.Security.Claims;
using System.Security.Cryptography;

namespace Partage_de_fichier.Controllers
{
    [Authorize] // Bloque l'accès si l'utilisateur n'est pas connecté
    public class FileController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;

        public FileController(ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        // 1. Affiche la liste des fichiers
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            // Fichiers dont l'utilisateur est le propriétaire
            var mesFichiers = await _context.Fichiers
                .Where(f => f.IdProprietaire == userId)
                .ToListAsync();

            // Fichiers partagés avec cet utilisateur
            var fichiersPartages = await _context.PartagesAcces
                .Include(p => p.Fichier)
                .ThenInclude(f => f.Proprietaire)
                .Where(p => p.IdUtilisateur == userId && p.Fichier.IdProprietaire != userId)
                .Select(p => p.Fichier)
                .ToListAsync();

            var viewModel = new FileShareViewModel
            {
                MesFichiers = mesFichiers,
                FichiersPartages = fichiersPartages
            };

            return View(viewModel);
        }

        // 2. Gère l'upload et le chiffrement AES du fichier
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Upload(IFormFile? fichierUpload)
        {
            if (fichierUpload == null || fichierUpload.Length == 0)
            {
                ModelState.AddModelError("", "Veuillez sélectionner un fichier valide.");
                return RedirectToAction("Index");
            }

            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var utilisateur = await _context.Utilisateurs.FindAsync(userId);

            if (utilisateur == null) return Unauthorized();

            // Préparer le dossier de stockage sécurisé
            string uploadFolder = Path.Combine(_env.ContentRootPath, "App_Data", "Uploads");
            Directory.CreateDirectory(uploadFolder);

            string nomFichierSecurise = Guid.NewGuid().ToString() + ".enc";
            string cheminComplet = Path.Combine(uploadFolder, nomFichierSecurise);

            // Générer la clé et le Vecteur d'Initialisation pour AES-256
            using Aes aes = Aes.Create();
            aes.KeySize = 256;
            aes.GenerateKey();
            aes.GenerateIV();

            // Chiffrer le fichier et le sauvegarder sur le disque
            using (FileStream fileStream = new FileStream(cheminComplet, FileMode.Create))
            using (ICryptoTransform encryptor = aes.CreateEncryptor())
            using (CryptoStream cryptoStream = new CryptoStream(fileStream, encryptor, CryptoStreamMode.Write))
            {
                await fichierUpload.CopyToAsync(cryptoStream);
            } // Le fichier est maintenant fermé et sécurisé sur le disque

            // Chiffrer la clé AES avec la CLÉ PUBLIQUE RSA de l'utilisateur
            // (La clé publique est en clair en BDD, pas besoin de la session ici !)
            using RSA rsa = RSA.Create();
            rsa.ImportFromPem(utilisateur.ClePubliqueRsa);
            byte[] cleAesChiffreeBytes = rsa.Encrypt(aes.Key, RSAEncryptionPadding.OaepSHA256);
            string cleAesChiffreeBase64 = Convert.ToBase64String(cleAesChiffreeBytes);

            // Enregistrer les métadonnées dans la base de données
            var nouveauFichier = new Fichier
            {
                NomFichier = fichierUpload.FileName,
                CheminServeur = cheminComplet,
                IvAes = Convert.ToBase64String(aes.IV),
                IdProprietaire = userId
            };

            _context.Fichiers.Add(nouveauFichier);
            await _context.SaveChangesAsync(); // Sauvegarde pour générer l'IdFichier

            // Donner l'accès au propriétaire lui-même
            var accesProprietaire = new PartageAcces
            {
                IdFichier = nouveauFichier.IdFichier,
                IdUtilisateur = userId,
                CleAesChiffree = cleAesChiffreeBase64
            };

            _context.PartagesAcces.Add(accesProprietaire);
            await _context.SaveChangesAsync();

            return RedirectToAction("Index");
        }

        // 3. Gère le téléchargement et le déchiffrement du fichier
        [HttpGet]
        public async Task<IActionResult> Download(int id)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            // Vérifier les accès
            var acces = await _context.PartagesAcces
                .Include(p => p.Fichier)
                .Include(p => p.Destinataire)
                .FirstOrDefaultAsync(p => p.IdFichier == id && p.IdUtilisateur == userId);

            if (acces == null) return Unauthorized("Accès refusé.");

            var fichier = acces.Fichier;

            // Récupérer la CLÉ PRIVÉE RSA en clair depuis la SESSION
            string? clePriveePem = HttpContext.Session.GetString("UserPrivateKey");

            if (string.IsNullOrEmpty(clePriveePem))
            {
                // Si la session a expiré (15 minutes), l'utilisateur doit se reconnecter
                return RedirectToAction("Login", "Account");
            }

            // Déchiffrer la clé AES avec la clé privée RSA
            using RSA rsa = RSA.Create();
            try
            {
                rsa.ImportFromPem(clePriveePem);
            }
            catch (Exception)
            {
                return BadRequest("La clé de sécurité en session est corrompue.");
            }

            byte[] cleAesChiffreeBytes = Convert.FromBase64String(acces.CleAesChiffree);
            byte[] cleAesEnClair;

            try
            {
                cleAesEnClair = rsa.Decrypt(cleAesChiffreeBytes, RSAEncryptionPadding.OaepSHA256);
            }
            catch (CryptographicException)
            {
                return BadRequest("Erreur de déchiffrement de la clé d'accès (RSA).");
            }

            // Déchiffrer le fichier avec la clé AES
            if (!System.IO.File.Exists(fichier.CheminServeur))
            {
                return NotFound("Fichier physique introuvable sur le serveur.");
            }

            var memoryStream = new MemoryStream();
            using (Aes aes = Aes.Create())
            {
                aes.Key = cleAesEnClair;
                aes.IV = Convert.FromBase64String(fichier.IvAes);

                using (FileStream fileStream = new FileStream(fichier.CheminServeur, FileMode.Open, FileAccess.Read))
                using (ICryptoTransform decryptor = aes.CreateDecryptor())
                using (CryptoStream cryptoStream = new CryptoStream(fileStream, decryptor, CryptoStreamMode.Read))
                {
                    await cryptoStream.CopyToAsync(memoryStream);
                }
            }

            // Renvoyer le fichier déchiffré à l'utilisateur
            memoryStream.Position = 0;
            return File(memoryStream, "application/octet-stream", fichier.NomFichier);
        }
    }
}
