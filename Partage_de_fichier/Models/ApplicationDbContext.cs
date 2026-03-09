using Microsoft.EntityFrameworkCore;
using Partage_de_fichier.Models; 

namespace Partage_de_fichier.Data
{
    public class ApplicationDbContext : DbContext
    {
        // Constructeur requis par EF Core pour passer la configuration (chaîne de connexion)
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // Déclaration de vos 3 tables pour PostgreSQL
        public DbSet<Utilisateur> Utilisateurs { get; set; }
        public DbSet<Fichier> Fichiers { get; set; }
        public DbSet<PartageAcces> PartagesAcces { get; set; }

        // Configuration approfondie (Fluent API)
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // 1. Relation : Un Utilisateur possède plusieurs Fichiers
            modelBuilder.Entity<Fichier>()
                .HasOne(f => f.Proprietaire)
                .WithMany(u => u.FichiersPossedes)
                .HasForeignKey(f => f.IdProprietaire)
                .OnDelete(DeleteBehavior.Cascade); // Si on supprime l'utilisateur, on supprime ses fichiers

            // 2. Relation : Un Fichier a plusieurs Partages d'Accès
            modelBuilder.Entity<PartageAcces>()
                .HasOne(p => p.Fichier)
                .WithMany(f => f.AccesPartages)
                .HasForeignKey(p => p.IdFichier)
                .OnDelete(DeleteBehavior.Cascade); // Si on supprime le fichier, on supprime ses accès partagés

            // 3. Relation : Un Utilisateur reçoit plusieurs Partages d'Accès (Destinataire)
            modelBuilder.Entity<PartageAcces>()
                .HasOne(p => p.Destinataire)
                .WithMany(u => u.FichiersPartagesAvecMoi)
                .HasForeignKey(p => p.IdUtilisateur)
                .OnDelete(DeleteBehavior.Restrict); // TRÈS IMPORTANT : Restrict évite les erreurs de "boucles de suppression en cascade" spécifiques à PostgreSQL
        }
    }
}