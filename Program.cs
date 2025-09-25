using Microsoft.EntityFrameworkCore;
using KodiBackend.Data;
using KodiBackend.Services;
using System.Text.Json.Serialization;

// TATO VERZE JE URCENA POUZE PRO WEB (RAILWAY)
// Pro lokální testování je potřeba použít jinou verzi.

var builder = WebApplication.CreateBuilder(args);

// --- Pevně nastavená cesta k databázi pro Railway ---
var connectionString = "Data Source=/app/data/KodiBackend.db";

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));
// --- Konec změn ---


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