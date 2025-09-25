using Microsoft.EntityFrameworkCore;
using KodiBackend.Data;
using KodiBackend.Services;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// --- ZDE JE JEDINÁ POTŘEBNÁ ZMĚNA ---

// 1. Zkusíme načíst cestu k databázi z proměnné prostředí (tu nastavíme v Railway).
var databasePath = Environment.GetEnvironmentVariable("DATABASE_PATH");

// 2. Pokud proměnná `DATABASE_PATH` existuje (na Railway), použijeme ji.
//    Pokud neexistuje (lokálně u tebe), použijeme hodnotu z tvého `appsettings.json`.
var connectionString = databasePath ?? builder.Configuration.GetConnectionString("DefaultConnection");

// 3. Použijeme výsledný connection string k registraci databáze.
//    Tento blok nahrazuje tvůj původní `builder.Services.AddDbContext`.
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));

// --- KONEC ZMĚN ---


// Zbytek souboru zůstává stejný
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient<TMDbService>();

builder.Services.AddHttpClient();
builder.Services.AddScoped<IWebshareService, WebshareService>();


var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthorization();
app.MapControllers();

// Automaticky spustí migraci databáze při startu
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    dbContext.Database.Migrate();
}
app.Run();