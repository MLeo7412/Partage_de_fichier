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

        // Affiche la liste des fichiers de l'utilisateur ET ceux partagés avec lui
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            // 1. Récupère les fichiers dont l'utilisateur est le propriétaire
            var mesFichiers = await _context.Fichiers
                .Where(f => f.IdProprietaire == userId)
                .ToListAsync();

            // 2. Récupère les fichiers qui ont été partagés avec cet utilisateur
            var fichiersPartages = await _context.PartagesAcces
                .Include(p => p.Fichier)
                .ThenInclude(f => f.Proprietaire) // Permet de savoir qui a partagé le fichier
                .Where(p => p.IdUtilisateur == userId && p.Fichier.IdProprietaire != userId)
                .Select(p => p.Fichier)
                .ToListAsync();

            // 3. On envoie les deux listes à la vue
            var viewModel = new FileShareViewModel
            {
                MesFichiers = mesFichiers,
                FichiersPartages = fichiersPartages
            };

            return View(viewModel);
        }

        // Gère l'upload et le chiffrement AES du fichier
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
            var utilisateur = _context.Utilisateurs.Find(userId);

            if (utilisateur == null) return Unauthorized();

            //Préparer le dossier de stockage sécurisé
            string uploadFolder = Path.Combine(_env.ContentRootPath, "App_Data", "Uploads");
            Directory.CreateDirectory(uploadFolder); // Crée le dossier s'il n'existe pas

            string nomFichierSecurise = Guid.NewGuid().ToString() + ".enc";
            string cheminComplet = Path.Combine(uploadFolder, nomFichierSecurise);

            //Générer la clé et le Vecteur d'Initialisation pour AES-256
            using Aes aes = Aes.Create();
            aes.KeySize = 256;
            aes.GenerateKey();
            aes.GenerateIV();

            //Chiffrer le fichier et le sauvegarder sur le disque
            using (FileStream fileStream = new FileStream(cheminComplet, FileMode.Create))
            using (ICryptoTransform encryptor = aes.CreateEncryptor())
            using (CryptoStream cryptoStream = new CryptoStream(fileStream, encryptor, CryptoStreamMode.Write))
            {
                await fichierUpload.CopyToAsync(cryptoStream);
            }

            //Chiffrer la clé AES avec la clé publique RSA de l'utilisateur
            using RSA rsa = RSA.Create();
            rsa.ImportFromPem(utilisateur.ClePubliqueRsa);
            byte[] cleAesChiffreeBytes = rsa.Encrypt(aes.Key, RSAEncryptionPadding.OaepSHA256);
            string cleAesChiffreeBase64 = Convert.ToBase64String(cleAesChiffreeBytes);

            //Enregistrer les métadonnées dans la base de données
            var nouveauFichier = new Fichier
            {
                NomFichier = fichierUpload.FileName,
                CheminServeur = cheminComplet,
                IvAes = Convert.ToBase64String(aes.IV),
                IdProprietaire = userId
            };

            _context.Fichiers.Add(nouveauFichier);
            await _context.SaveChangesAsync(); // Sauvegarde pour générer l'IdFichier

            //Donner l'accès au propriétaire lui-même
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
        // Gère le téléchargement et le déchiffrement du fichier
        [HttpGet]
        public async Task<IActionResult> Download(int id)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            // 1. Vérifier que l'utilisateur a bien le droit d'accéder à ce fichier
            var acces = await _context.PartagesAcces
                .Include(p => p.Fichier)
                .Include(p => p.Destinataire)
                .FirstOrDefaultAsync(p => p.IdFichier == id && p.IdUtilisateur == userId);

            if (acces == null)
            {
                return Unauthorized("Vous n'avez pas l'autorisation d'accéder à ce fichier.");
            }

            var fichier = acces.Fichier;
            var utilisateur = acces.Destinataire;

            // 2. Récupérer la clé privée RSA de l'utilisateur
            // (Dans Register, nous l'avions simplement encodée en Base64 pour simuler le chiffrement, on fait donc l'inverse)
            string clePriveePem = Encoding.UTF8.GetString(Convert.FromBase64String(utilisateur.ClePriveeRsaChiffree));

            // 3. Déchiffrer la clé AES avec la clé privée RSA
            using RSA rsa = RSA.Create();
            rsa.ImportFromPem(clePriveePem);

            byte[] cleAesChiffreeBytes = Convert.FromBase64String(acces.CleAesChiffree);
            byte[] cleAesEnClair;

            try
            {
                cleAesEnClair = rsa.Decrypt(cleAesChiffreeBytes, RSAEncryptionPadding.OaepSHA256);
            }
            catch (CryptographicException)
            {
                return BadRequest("Erreur de déchiffrement de la clé d'accès.");
            }

            // 4. Déchiffrer le fichier avec la clé AES
            byte[] ivAes = Convert.FromBase64String(fichier.IvAes);

            if (!System.IO.File.Exists(fichier.CheminServeur))
            {
                return NotFound("Le fichier physique est introuvable sur le serveur.");
            }

            using Aes aes = Aes.Create();
            aes.Key = cleAesEnClair;
            aes.IV = ivAes;

            var memoryStream = new MemoryStream();
            using (FileStream fileStream = new FileStream(fichier.CheminServeur, FileMode.Open, FileAccess.Read))
            using (ICryptoTransform decryptor = aes.CreateDecryptor())
            using (CryptoStream cryptoStream = new CryptoStream(fileStream, decryptor, CryptoStreamMode.Read))
            {
                // On lit le fichier chiffré et on le copie en clair dans la mémoire RAM
                await cryptoStream.CopyToAsync(memoryStream);
            }

            // 5. Renvoyer le fichier à l'utilisateur
            memoryStream.Position = 0; // On remet le curseur de lecture au début du flux

            // On utilise "application/octet-stream" pour forcer le téléchargement, quel que soit le type de fichier
            return File(memoryStream, "application/octet-stream", fichier.NomFichier);
        }
    }
}