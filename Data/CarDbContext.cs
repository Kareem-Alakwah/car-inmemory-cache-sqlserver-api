using CarCacheApi.Models;
using Microsoft.EntityFrameworkCore;

namespace CarCacheApi.Data;

public class CarDbContext : DbContext
{
    public CarDbContext(DbContextOptions<CarDbContext> options) : base(options)
    {
    }

    public DbSet<Car> Cars => Set<Car>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure decimal precision for Price
        modelBuilder.Entity<Car>()
            .Property(c => c.Price)
            .HasPrecision(18, 2);

        // Seed Initial Data
        modelBuilder.Entity<Car>().HasData(
            new Car { Id = 1, Make = "Toyota", Model = "Camry", Year = 2020, Price = 24500.00m, Color = "Silver" },
            new Car { Id = 2, Make = "Toyota", Model = "RAV4", Year = 2022, Price = 28200.00m, Color = "Blue" },
            new Car { Id = 3, Make = "Ford", Model = "Mustang", Year = 2020, Price = 36000.00m, Color = "Red" },
            new Car { Id = 4, Make = "Ford", Model = "F-150", Year = 2021, Price = 42000.00m, Color = "White" },
            new Car { Id = 5, Make = "Honda", Model = "Civic", Year = 2021, Price = 22000.00m, Color = "Grey" },
            new Car { Id = 6, Make = "Tesla", Model = "Model 3", Year = 2023, Price = 39990.00m, Color = "White" },
            new Car { Id = 7, Make = "BMW", Model = "3 Series", Year = 2023, Price = 45000.00m, Color = "Black" }
        );
    }
}
