using Microsoft.EntityFrameworkCore;
using Partage_de_fichier.Data;

var builder = WebApplication.CreateBuilder(args);

// Ajouter les services MVC au conteneur.
builder.Services.AddControllersWithViews();

//Enregistrer ApplicationDbContext pour utiliser PostgreSQL
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();