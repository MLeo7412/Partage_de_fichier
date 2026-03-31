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

        // 1. Traite le formulaire d'inscription
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Register(RegisterViewModel model)
        {
            if (ModelState.IsValid)
            {
                // Vérifier si l'utilisateur existe déjà
                if (_context.Utilisateurs.Any(u => u.NomUtilisateur == model.NomUtilisateur))
                {
                    ModelState.AddModelError("", "Ce nom d'utilisateur est déjà pris.");
                    return View(model);
                }

                // Hachage du mot de passe pour l'authentification
                string hash = BCrypt.Net.BCrypt.HashPassword(model.MotDePasse);

                // Génération de la paire de clés RSA
                using var rsa = RSA.Create(2048);
                string clePublique = rsa.ExportRSAPublicKeyPem();
                string clePriveeEnClair = rsa.ExportRSAPrivateKeyPem();

                // Vrai chiffrement de la clé privée RSA avec AES
                string clePriveeChiffree = ChiffrerClePrivee(clePriveeEnClair, model.MotDePasse);
                Console.WriteLine("Clé privée RSA chiffrée : " + clePriveeChiffree);

                // Création de l'entité
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
                return RedirectToAction("Index", "File");
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
                var utilisateur = _context.Utilisateurs.SingleOrDefault(u => u.NomUtilisateur == model.NomUtilisateur);

                // Vérification du mot de passe avec le Hash en BDD
                if (utilisateur != null && BCrypt.Net.BCrypt.Verify(model.MotDePasse, utilisateur.MotDePasseHash))
                {
                    try
                    {
                        // 1. On déchiffre la clé RSA et on la met en Session
                        string clePriveeEnClair = DechiffrerClePrivee(utilisateur.ClePriveeRsaChiffree, model.MotDePasse);
                        HttpContext.Session.SetString("UserPrivateKey", clePriveeEnClair);

                        // 2. CRÉATION DU COOKIE DE CONNEXION (C'est ce qu'il te manquait !)
                        var claims = new List<Claim>
                        {
                            new Claim(ClaimTypes.NameIdentifier, utilisateur.IdUtilisateur.ToString()),
                            new Claim(ClaimTypes.Name, utilisateur.NomUtilisateur)
                        };

                        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

                        await HttpContext.SignInAsync(
                            CookieAuthenticationDefaults.AuthenticationScheme,
                            new ClaimsPrincipal(claimsIdentity));

                        // 3. Redirection vers l'accueil des fichiers
                        return RedirectToAction("Index", "File");
                    }
                    catch (Exception ex)
                    {
                        // En cas d'erreur de déchiffrement (ex: ancien compte mal chiffré)
                        ModelState.AddModelError("", "Erreur technique de sécurité : " + ex.Message);
                        return View(model);
                    }
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
            HttpContext.Session.Clear(); // On vide aussi la session par sécurité !
            return RedirectToAction("Login", "Account");
        }

        private string ChiffrerClePrivee(string texteAChiffrer, string motDePasse)
        {    
            // 1. On génère un "Sel" (16 octets) unique pour renforcer le mot de passe
            byte[] salt = RandomNumberGenerator.GetBytes(16);

            // 2. On crée une clé AES ultra-sécurisée (32 octets) à partir du mot de passe et du Sel
            using var deriveBytes = new Rfc2898DeriveBytes(motDePasse, salt, 100000, HashAlgorithmName.SHA256);
            byte[] key = deriveBytes.GetBytes(32);

            // 3. On génère un Vecteur d'Initialisation (IV) de 16 octets pour démarrer le chiffrement
            byte[] iv = RandomNumberGenerator.GetBytes(16);

            // 4. On configure l'algorithme AES avec notre clé et notre IV
            using var aes = Aes.Create();
            aes.Key = key;
            aes.IV = iv;

            // 5. On chiffre la clé privée (texteAChiffrer) dans un flux en mémoire
            using var mStream = new MemoryStream();
            using (var cStream = new CryptoStream(mStream, aes.CreateEncryptor(), CryptoStreamMode.Write))
            using (var sw = new StreamWriter(cStream))
            {
                sw.Write(texteAChiffrer);
            }

            // 6. On assemble le paquet complet : Sel + IV + Données chiffrées (dans cet ordre précis !)
            var result = salt.Concat(iv).Concat(mStream.ToArray()).ToArray();

            // On renvoie le paquet sous forme de texte (Base64) pour le sauvegarder facilement en BDD
            return Convert.ToBase64String(result);
        }

        private string DechiffrerClePrivee(string donneeChiffreeBase64, string motDePasse)
        {
            // 1. On transforme le texte Base64 en paquet d'octets
            byte[] fullPackage = Convert.FromBase64String(donneeChiffreeBase64);

            // 2. On extrait le Sel (les 16 premiers octets)
            byte[] salt = fullPackage.Take(16).ToArray();

            // 3. On extrait l'IV (les 16 octets suivants)
            byte[] iv = fullPackage.Skip(16).Take(16).ToArray();

            // 4. Le reste, ce sont les données chiffrées
            byte[] cipherText = fullPackage.Skip(32).ToArray();

            // 5. On régénère la MÊME clé AES en utilisant le mot de passe + le sel extrait
            using var deriveBytes = new Rfc2898DeriveBytes(motDePasse, salt, 100000, HashAlgorithmName.SHA256);
            byte[] key = deriveBytes.GetBytes(32);

            // 6. On déchiffre avec AES
            using var aes = Aes.Create();
            aes.Key = key;
            aes.IV = iv;

            using var mStream = new MemoryStream(cipherText);
            using var cStream = new CryptoStream(mStream, aes.CreateDecryptor(), CryptoStreamMode.Read);
            using var sr = new StreamReader(cStream);

            // On obtient enfin la clé RSA originale en clair !
            return sr.ReadToEnd();
        }
    }
}
