// [Data/ApplicationDbContext.cs]

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
        public DbSet<HistoryEntry> HistoryEntries { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // --- OPRAVA PRO DUPLICITY FILMŮ ---
            modelBuilder.Entity<Movie>()
                .HasIndex(m => m.TMDbId)
                .IsUnique();
            
            // --- NOVÁ OPRAVA PRO DUPLICITY SERIÁLŮ ---
            modelBuilder.Entity<Show>()
                .HasIndex(s => s.TMDbId)
                .IsUnique();
            
            // --- OPRAVA PRO DUPLICITY WEBSHARE ODKAZŮ ---
            modelBuilder.Entity<WebshareLink>()
                .HasIndex(l => l.FileIdent)
                .IsUnique();
            
            
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
                
            // KLÍČOVÉ PRO BLACKLIST: Vytvoříme UNIKÁTNÍ INDEX na Title a MediaType.
            modelBuilder.Entity<HistoryEntry>()
                .HasIndex(h => new { h.Title, h.MediaType })
                .IsUnique();
        }
    }
}