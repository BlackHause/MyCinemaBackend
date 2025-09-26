using Microsoft.EntityFrameworkCore;
using KodiBackend.Data;
using KodiBackend.Services;
using System.Text.Json.Serialization;
using System.IO; 
using System;

// TATO VERZE JE URČENA PRO RAILWAY / GITHUB NASAZENÍ S PERZISTENTNÍ DATABÁZÍ

var builder = WebApplication.CreateBuilder(args);
var env = builder.Environment;

// --- KLÍČOVÉ NASTAVENÍ PRO RAILWAY / GITHUB (SQLite Perzistence) ---
string dbFileName = "kodi.db";
string dbPath;

// Kontrola prostředí: Pokud nejsme v režimu Development (tj. jsme na Railway/GitHub)
if (!env.IsDevelopment())
{
    // Cesta pro Railway/kontejner, kde /app/data je perzistentní volume
    string dataDirectory = "/app/data";
    dbPath = Path.Combine(dataDirectory, dbFileName);
    
    // Zajištění existence adresáře /app/data
    if (!Directory.Exists(dataDirectory))
    {
        Directory.CreateDirectory(dataDirectory);
        // Poznámka: Console.WriteLine v produkci není nutné, ale pomáhá při debugování
    }
}
else
{
    // Lokální vývoj: databáze je uložena v rootu projektu
    dbPath = Path.Combine(Directory.GetCurrentDirectory(), dbFileName);
}

// Konstrukce connection stringu
var connectionString = $"Data Source={dbPath}";

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));
// --- Konec nastavení pro persistenci ---


builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient<TMDbService>();

// Přidání CSFD Service
builder.Services.AddHttpClient<CSFDService>(); 

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