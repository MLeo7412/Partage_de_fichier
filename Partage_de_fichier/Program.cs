using Microsoft.EntityFrameworkCore;
using Partage_de_fichier.Data;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

// --- 1. CONFIGURATION DES SERVICES ---

// Ajouter les services MVC au conteneur
builder.Services.AddControllersWithViews();

// Enregistrer ApplicationDbContext pour utiliser PostgreSQL
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Configuration de l'authentification (Cookies)
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login"; // Redirige ici si non connecté
        options.ExpireTimeSpan = TimeSpan.FromHours(2); // Le cookie expire après 2h
    });

// Configuration des Sessions (Obligatoire pour stocker la clé RSA)
builder.Services.AddDistributedMemoryCache(); 
builder.Services.AddSession(options => {
    options.IdleTimeout = TimeSpan.FromMinutes(15); // La clé s'efface de la mémoire après 15min d'inactivité
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});


var app = builder.Build();

// --- 2. CONFIGURATION  HTTP 

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// Activer la session AVANT l'authentification
app.UseSession();

app.UseAuthentication(); 
app.UseAuthorization();  

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

app.Run();
