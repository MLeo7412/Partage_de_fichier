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
                // 1. Vérifier si l'utilisateur existe déjà
                if (_context.Utilisateurs.Any(u => u.NomUtilisateur == model.NomUtilisateur))
                {
                    ModelState.AddModelError("", "Ce nom d'utilisateur est déjà pris.");
                    return View(model);
                }

                // 2. Hacher le mot de passe avec Bcrypt (Le mot de passe n'est jamais en clair)
                string hash = BCrypt.Net.BCrypt.HashPassword(model.MotDePasse);

                // 3. Générer la paire de clés RSA (2048 bits est le standard de sécurité)
                using var rsa = RSA.Create(2048);

                // Exporter la clé publique (pour que les autres puissent lui partager des fichiers)
                string clePublique = rsa.ExportRSAPublicKeyPem();

                // Exporter la clé privée
                string clePriveeEnClair = rsa.ExportRSAPrivateKeyPem();

                // 4. Chiffrer la clé privée (Simulation : dans un vrai projet, on la chiffre avec AES en utilisant un dérivé du mot de passe)
                string clePriveeChiffree = Convert.ToBase64String(Encoding.UTF8.GetBytes(clePriveeEnClair));

                // 5. Créer l'entité Utilisateur
                var nouvelUtilisateur = new Utilisateur
                {
                    NomUtilisateur = model.NomUtilisateur,
                    MotDePasseHash = hash,
                    ClePubliqueRsa = clePublique,
                    ClePriveeRsaChiffree = clePriveeChiffree
                };

                // 6. Sauvegarder en base de données
                _context.Utilisateurs.Add(nouvelUtilisateur);
                _context.SaveChanges();

                // Rediriger vers la page de connexion après succès
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


    }
}