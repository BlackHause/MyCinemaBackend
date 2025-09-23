using Microsoft.EntityFrameworkCore;
using KodiBackend.Models;

namespace KodiBackend.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Movie> Movies { get; set; }
        public DbSet<Show> Shows { get; set; }
        public DbSet<Season> Seasons { get; set; }
        public DbSet<Episode> Episodes { get; set; }
        // PŘIDÁN NOVÝ ŘÁDEK
        public DbSet<WebshareLink> WebshareLinks { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Konfigurace vazeb (volitelné, ale doporučené)
            modelBuilder.Entity<WebshareLink>()
                .HasOne(l => l.Movie)
                .WithMany(m => m.Links)
                .HasForeignKey(l => l.MovieId);

            modelBuilder.Entity<WebshareLink>()
                .HasOne(l => l.Episode)
                .WithMany(e => e.Links)
                .HasForeignKey(l => l.EpisodeId);
        }
    }
}