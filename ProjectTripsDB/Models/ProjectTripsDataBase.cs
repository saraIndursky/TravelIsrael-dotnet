using Microsoft.EntityFrameworkCore;
using Repository.Entities;
using Repository.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static System.Reflection.Metadata.BlobBuilder;

namespace ProjectTripsDB.Models
{
    public class ProjectTripsDataBase : DbContext, IContext
    {
        public virtual DbSet<DayTrip> DayTrips { get; set; }
        public virtual DbSet<DayTripItem> DayTripItems { get; set; }
        public virtual DbSet<Place> Places { get; set; }
        public virtual DbSet<Image> Images { get; set; }
        public virtual DbSet<Region> Regions { get; set; }
        public virtual DbSet<Review> Reviews { get; set; }
        public virtual DbSet<Route> Routes { get; set; }
        public virtual DbSet<RoutePoint> RoutePoints { get; set; }
        public virtual DbSet<Repository.Entities.Type> Types { get; set; }
        public virtual DbSet<User> Users { get; set; }
        DbSet<DayTrip> IContext.DayTrips => DayTrips;
        DbSet<DayTripItem> IContext.DayTripItems => DayTripItems;
        DbSet<Place> IContext.Places => Places;
        DbSet<Image> IContext.Images => Images;
        DbSet<Region> IContext.Regions => Regions;
        DbSet<Review> IContext.Reviews => Reviews;
        DbSet<Route> IContext.Routes => Routes;
        DbSet<RoutePoint> IContext.RoutePoints => RoutePoints;
        DbSet<Repository.Entities.Type> IContext.Types => Types;
        DbSet<User> IContext.Users => Users;
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=ProjectTripsDB;Trusted_Connection=True", o=> o.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery));
        }
        public async Task save()
        {
            await SaveChangesAsync();
        }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Image>()
                .HasOne(pi => pi.User)
                .WithMany(pr => pr.Images)
                .HasForeignKey(pi => pi.CreatedByUserId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Region>()
                .HasOne(r => r.ParentRegion)
                .WithMany(r => r.SubRegions)
                .HasForeignKey(r => r.ParentRegionId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<DayTrip>()
                .HasIndex(t => t.TripHash)
                .IsUnique();

            modelBuilder.Entity<DayTrip>().ToTable("DayTrips");
            modelBuilder.Entity<DayTripItem>().ToTable("DayTripItems");
            modelBuilder.Entity<Place>().ToTable("Places");
            modelBuilder.Entity<Image>().ToTable("Images");
            modelBuilder.Entity<Region>().ToTable("Regions");
            modelBuilder.Entity<Review>().ToTable("Reviews");
            modelBuilder.Entity<Route>().ToTable("Routes");
            modelBuilder.Entity<RoutePoint>().ToTable("RoutePoints");
            modelBuilder.Entity<Repository.Entities.Type>().ToTable("Types");
            modelBuilder.Entity<User>().ToTable("Users");

            modelBuilder.Entity<User>().HasData(
            new User
            {
                Id = 1,
                FirstName = "Admin",
                LastName = "Admin",
                Email = "Admin@gmail.com",
                Password = "Admin#613", // TODO: seed a hashed password
                Role = Role.Admin,
                CreatedAt = DateOnly.FromDateTime(DateTime.Today),
                IsActive = true
            }
            );
        }
    }
}
