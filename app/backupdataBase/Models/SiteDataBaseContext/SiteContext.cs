using Microsoft.EntityFrameworkCore;
using Polly.CircuitBreaker;
using Polly;
using System.Xml;
namespace backupdataBase.Models.SiteDataBaseContext
{
    public class SiteContext : DbContext
    {
        public DbSet<Products> Products { get; set; }
        public SiteContext(DbContextOptions<SiteContext> options) : base(options) { }
    }
    public class DatabaseService
    {
        private readonly SiteContext _context;
        private readonly AsyncCircuitBreakerPolicy _circuitBreakerPolicy;
        public DatabaseService(SiteContext context)
        {
            _context = context; _circuitBreakerPolicy
                = Policy.Handle<Exception>().CircuitBreakerAsync(2, TimeSpan.FromMinutes(1));
        }
        public async Task<List<Products>> GetDataAsync()
        { return await _circuitBreakerPolicy.ExecuteAsync(async () =>
        { return await _context.Products.ToListAsync(); }); }
    }
}

