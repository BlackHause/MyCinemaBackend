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
        public DbSet<WebshareLink> WebshareLinks { get; set; }
        public DbSet<HistoryEntry> HistoryEntries { get; set; } // Nová tabulka pro historii

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Konfigurace kaskádového mazání pro filmy a jejich odkazy
            modelBuilder.Entity<Movie>()
                .HasMany(m => m.Links)
                .WithOne(l => l.Movie)
                .HasForeignKey(l => l.MovieId)
                .OnDelete(DeleteBehavior.Cascade);

            // Konfigurace kaskádového mazání pro seriály a jejich sezóny
            modelBuilder.Entity<Show>()
                .HasMany(s => s.Seasons)
                .WithOne(s => s.Show)
                .HasForeignKey(s => s.ShowId)
                .OnDelete(DeleteBehavior.Cascade);

            // Konfigurace kaskádového mazání pro sezóny a jejich epizody
            modelBuilder.Entity<Season>()
                .HasMany(s => s.Episodes)
                .WithOne(e => e.Season)
                .HasForeignKey(e => e.SeasonId)
                .OnDelete(DeleteBehavior.Cascade);

            // Konfigurace kaskádového mazání pro epizody a jejich odkazy
            modelBuilder.Entity<Episode>()
                .HasMany(e => e.Links)
                .WithOne(l => l.Episode)
                .HasForeignKey(l => l.EpisodeId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}