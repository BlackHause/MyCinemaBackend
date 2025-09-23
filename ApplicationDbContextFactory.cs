using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using KodiBackend.Data; // Ujisti se, že tato cesta je správná (pro tvůj ApplicationDbContext)

namespace KodiBackend
{
    public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
            optionsBuilder.UseSqlite("DataSource=kodi.db"); // Zde se databáze pojmenuje kodi.db

            return new ApplicationDbContext(optionsBuilder.Options);
        }
    }
}