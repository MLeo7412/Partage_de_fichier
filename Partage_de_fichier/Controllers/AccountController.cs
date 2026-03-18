using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Partage_de_fichier.Data;
using Partage_de_fichier.Models;
using Partage_de_fichier.ViewModels;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Partage_de_fichier.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AccountController(ApplicationDbContext context)
        {
            _context = context;
        }

       
        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        // Traite le formulaire d'inscription
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Register(RegisterViewModel model)
        {
            if (ModelState.IsValid)
            {
                // 1. Vérif existence...
                if (_context.Utilisateurs.Any(u => u.NomUtilisateur == model.NomUtilisateur)) { /* ... */ }

                // 2. Hachage du mot de passe pour l'auth
                string hash = BCrypt.Net.BCrypt.HashPassword(model.MotDePasse);

                // 3. Génération RSA
                using var rsa = RSA.Create(2048);
                string clePublique = rsa.ExportRSAPublicKeyPem();
                string clePriveeEnClair = rsa.ExportRSAPrivateKeyPem();

                
                // On utilise le mot de passe de l'utilisateur pour chiffrer sa clé RSA
                string clePriveeChiffree = ChiffrerClePrivee(clePriveeEnClair, model.MotDePasse);
                Console.WriteLine("Clé privée RSA chiffrée : " + clePriveeChiffree);

                // 5. Création entité
                var nouvelUtilisateur = new Utilisateur
                {
                    NomUtilisateur = model.NomUtilisateur,
                    MotDePasseHash = hash,
                    ClePubliqueRsa = clePublique,
                    ClePriveeRsaChiffree = clePriveeChiffree
                };

                _context.Utilisateurs.Add(nouvelUtilisateur);
                _context.SaveChanges();

                return RedirectToAction("Login", "Account");
            }
            return View(model);
        }
        [HttpGet]
        public IActionResult Login()
        {
            // Si l'utilisateur est déjà connecté, on l'envoie vers ses fichiers
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "File"); // On créera ce contrôleur plus tard
            }
            return View();
        }

        // 2. Traite le formulaire de connexion
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (ModelState.IsValid)
            {
                // A. Chercher l'utilisateur dans la base de données
                var utilisateur = _context.Utilisateurs.SingleOrDefault(u => u.NomUtilisateur == model.NomUtilisateur);

                // B. Vérifier le mot de passe avec Bcrypt
                if (utilisateur != null && BCrypt.Net.BCrypt.Verify(model.MotDePasse, utilisateur.MotDePasseHash))
                {
                    // C. Créer la "carte d'identité" (Claims) de l'utilisateur pour la session
                    var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, utilisateur.IdUtilisateur.ToString()),
                new Claim(ClaimTypes.Name, utilisateur.NomUtilisateur)
            };

                    var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

                    // D. Connecter l'utilisateur (Création du cookie sécurisé)
                    await HttpContext.SignInAsync(
                        CookieAuthenticationDefaults.AuthenticationScheme,
                        new ClaimsPrincipal(claimsIdentity));

                    // E. Rediriger vers la page des fichiers
                    return RedirectToAction("Index", "File");
                }

                ModelState.AddModelError("", "Nom d'utilisateur ou mot de passe incorrect.");
            }

            return View(model);
        }

        // 3. Gérer la déconnexion
        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login", "Account");
        }

        private string ChiffrerClePrivee(string texteAChiffrer, string motDePasse)
        {
            byte[] salt = RandomNumberGenerator.GetBytes(16); // Sel pour rendre le chiffrement unique
            using var deriveBytes = new Rfc2898DeriveBytes(motDePasse, salt, 100000, HashAlgorithmName.SHA256);
            byte[] key = deriveBytes.GetBytes(32); // Clé AES 256 bits
            byte[] iv = RandomNumberGenerator.GetBytes(16);  // Vecteur d'initialisation

            using var aes = Aes.Create();
            aes.Key = key;
            aes.IV = iv;

            using var mStream = new MemoryStream();
            using (var cStream = new CryptoStream(mStream, aes.CreateEncryptor(), CryptoStreamMode.Write))
            using (var sw = new StreamWriter(cStream))
            {
                sw.Write(texteAChiffrer);
            }

            // On concatène Sel + IV + Données chiffrées pour pouvoir déchiffrer plus tard
            var result = salt.Concat(iv).Concat(mStream.ToArray()).ToArray();
            return Convert.ToBase64String(result);
        }


    }
}