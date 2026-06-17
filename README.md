# .NET 10 Web API - In-Memory Cache with Invalidation (.NET 10, SQL Server & EF Core)

This project solves the problem of caching a list of entities (Cars) queried with complex filter parameters (combinations of Year, Make, and Model arrays) using an in-memory caching system in ASP.NET Core. 

It satisfies the following criteria:
1. **First request for query should be fetched from SQL Database (SQL Server).**
2. **Subsequent requests for the exact same query parameters should retrieve data from in-memory cache.**
3. **Any update to values in the database (Create, Edit, Delete) should immediately invalidate/delete the relevant caching.**

---

## Technical Stack & Architecture

- **Runtime:** .NET 10.0
- **Database:** Entity Framework Core with SQL Server (LocalDB by default)
- **Caching:** `IMemoryCache` (In-Memory cache provider)
- **Eviction Strategy:** `CancellationChangeToken` pattern
- **Auto-Initialization:** The database scheme is automatically verified and seeded on start via `context.Database.EnsureCreated()`.

---

## Eviction Design: Why `CancellationChangeToken`?

In `IMemoryCache`, there is no public API to clear all keys or find keys matching a wildcard (like `cars:list:*`). 
To solve this cleanly:
- We implement `ICacheSignal` containing a global `CancellationTokenSource`.
- Every list cache entry binds its lifetime to a change token using `.AddExpirationToken(_cacheSignal.GetToken())`.
- When a CRUD update occurs (POST/PUT/DELETE), we call `_cacheSignal.Invalidate()`. This cancels the token source, which instantly evicts **all** list cache entries. A new token source is then initialized for future cache additions.
- This is a thread-safe, high-performance, and standard pattern in .NET Core.

---

## Cache Key Normalization

Query parameters can be provided in any order and casing (e.g. `years=2020&makes=Toyota` vs `makes=toyota&years=2020`). 
To prevent duplicate caching of identical datasets:
1. All filter arrays (`Years`, `Makes`, `Models`) are sorted.
2. String filters are trimmed and mapped to lower-case.
3. The normalized values are joined to build a unique cache key, e.g. `cars:list:y:[2020]:ma:[toyota]:mo:[all]`.

---

## File Structure

- [Program.cs](Program.cs): Configures the application pipeline, dependency injection, and automatically runs DB creation/seeding on startup.
- [CarsController.cs](Controllers/CarsController.cs): Implements the REST API endpoints and manages cache hit/miss/invalidation lifecycle.
- [CarFilter.cs](Models/CarFilter.cs): Request model for binding array queries.
- [Car.cs](Models/Car.cs): Database entity representation.
- [CarDbContext.cs](Data/CarDbContext.cs): Database context with sample seed data.
- [CacheSignal.cs](Services/CacheSignal.cs): Cache eviction token service.

---

## Running the API

### Prerequisites
- **.NET 10 SDK** (installed on your system)
- **SQL Server LocalDB** (standard on Windows developer environments)

### Run Command
In the project directory, run:
```bash
dotnet run --launch-profile http
```

The API will start listening on: `http://localhost:5278`

---

## Verification and Testing

You can use the built-in [CarCacheApi.http](CarCacheApi.http) file or curl to test:

1. **Initial Load (Cache MISS):**
   ```bash
   GET http://localhost:5278/api/cars?years=2020
   ```
   *Response header will contain `X-Cache: MISS` and terminal logs will show SQL query execution.*

2. **Subsequent Load (Cache HIT):**
   ```bash
   GET http://localhost:5278/api/cars?years=2020
   ```
   *Response header will contain `X-Cache: HIT` and no SQL queries will execute.*

3. **Database Mutation (POST / PUT / DELETE):**
   ```bash
   POST http://localhost:5278/api/cars
   Content-Type: application/json

   {
     "make": "Toyota",
     "model": "Yaris",
     "year": 2020,
     "price": 18000.00,
     "color": "Red"
   }
   ```

4. **Request again (Cache MISS):**
   ```bash
   GET http://localhost:5278/api/cars?years=2020
   ```
   *Response will contain the newly added Car. The cache header will show `X-Cache: MISS` because the cache was invalidated upon creation.*
