using CarCacheApi.Data;
using CarCacheApi.Models;
using CarCacheApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CarCacheApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CarsController : ControllerBase
{
    private readonly CarDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly ICacheSignal _cacheSignal;
    private readonly ILogger<CarsController> _logger;

    public CarsController(
        CarDbContext context,
        IMemoryCache cache,
        ICacheSignal cacheSignal,
        ILogger<CarsController> _logger)
    {
        _context = context;
        _cache = cache;
        _cacheSignal = cacheSignal;
        this._logger = _logger;
    }

    // GET: api/cars
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Car>>> GetCarsList([FromQuery] CarFilter filter)
    {
        var cacheKey = GetCacheKey(filter);

        // Try checking cache first
        if (_cache.TryGetValue(cacheKey, out List<Car>? cachedCars) && cachedCars != null)
        {
            _logger.LogInformation("Cache HIT for key: {CacheKey}", cacheKey);
            Response.Headers.Append("X-Cache", "HIT");
            return Ok(cachedCars);
        }

        _logger.LogInformation("Cache MISS for key: {CacheKey}. Querying SQL Server database...", cacheKey);
        Response.Headers.Append("X-Cache", "MISS");

        // Fetch filtered cars from database
        IQueryable<Car> query = _context.Cars;

        if (filter.Years != null && filter.Years.Length > 0)
        {
            query = query.Where(c => filter.Years.Contains(c.Year));
        }

        if (filter.Makes != null && filter.Makes.Length > 0)
        {
            // Standard case-insensitive comparison based on SQL Server collation
            query = query.Where(c => filter.Makes.Contains(c.Make));
        }

        if (filter.Models != null && filter.Models.Length > 0)
        {
            query = query.Where(c => filter.Models.Contains(c.Model));
        }

        var carsList = await query.ToListAsync();

        // Configure cache options linked to the eviction token
        var cacheEntryOptions = new MemoryCacheEntryOptions()
            .AddExpirationToken(_cacheSignal.GetToken())
            .SetAbsoluteExpiration(TimeSpan.FromHours(1)); // Absolute fallback expiration

        _cache.Set(cacheKey, carsList, cacheEntryOptions);

        return Ok(carsList);
    }

    // GET: api/cars/{id}
    [HttpGet("{id}")]
    public async Task<ActionResult<Car>> GetById(int id)
    {
        var car = await _context.Cars.FindAsync(id);
        if (car == null)
        {
            return NotFound();
        }

        return Ok(car);
    }

    // POST: api/cars
    [HttpPost]
    public async Task<ActionResult<Car>> AddCar([FromBody] Car car)
    {
        _context.Cars.Add(car);
        await _context.SaveChangesAsync();

        // Invalidate all query list caches
        _cacheSignal.Invalidate();
        _logger.LogInformation("Database updated: Car added (Id: {CarId}). Evicting cache.", car.Id);

        return CreatedAtAction(nameof(GetById), new { id = car.Id }, car);
    }

    // PUT: api/cars/{id}
    [HttpPut("{id}")]
    public async Task<IActionResult> EditCar(int id, [FromBody] Car updatedCar)
    {
        if (id != updatedCar.Id)
        {
            return BadRequest("ID in URL does not match ID in body.");
        }

        var car = await _context.Cars.FindAsync(id);
        if (car == null)
        {
            return NotFound();
        }

        // Update properties
        car.Make = updatedCar.Make;
        car.Model = updatedCar.Model;
        car.Year = updatedCar.Year;
        car.Price = updatedCar.Price;
        car.Color = updatedCar.Color;

        _context.Entry(car).State = EntityState.Modified;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!CarExists(id))
            {
                return NotFound();
            }
            throw;
        }

        // Invalidate all query list caches
        _cacheSignal.Invalidate();
        _logger.LogInformation("Database updated: Car updated (Id: {CarId}). Evicting cache.", id);

        return NoContent();
    }

    // DELETE: api/cars/{id}
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteCar(int id)
    {
        var car = await _context.Cars.FindAsync(id);
        if (car == null)
        {
            return NotFound();
        }

        _context.Cars.Remove(car);
        await _context.SaveChangesAsync();

        // Invalidate all query list caches
        _cacheSignal.Invalidate();
        _logger.LogInformation("Database updated: Car deleted (Id: {CarId}). Evicting cache.", id);

        return NoContent();
    }

    private bool CarExists(int id)
    {
        return _context.Cars.Any(e => e.Id == id);
    }

    /// <summary>
    /// Generates a normalized, deterministic cache key from filter arrays.
    /// Sorts and lowercases values to guarantee that identical filters 
    /// in different order and casing yield the exact same cache key.
    /// </summary>
    private string GetCacheKey(CarFilter filter)
    {
        var years = filter.Years != null && filter.Years.Length > 0
            ? string.Join(",", filter.Years.OrderBy(y => y))
            : "all";

        var makes = filter.Makes != null && filter.Makes.Length > 0
            ? string.Join(",", filter.Makes.Select(m => m.Trim().ToLowerInvariant()).OrderBy(m => m))
            : "all";

        var models = filter.Models != null && filter.Models.Length > 0
            ? string.Join(",", filter.Models.Select(m => m.Trim().ToLowerInvariant()).OrderBy(m => m))
            : "all";

        return $"cars:list:y:[{years}]:ma:[{makes}]:mo:[{models}]";
    }
}
