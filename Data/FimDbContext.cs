using fim_queueing_admin.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace fim_queueing_admin.Data;

public class FimDbContext : DbContext
{
    public FimDbContext(DbContextOptions options) : base(options)
    {
    }
    
    /// <summary>
    /// AV Carts, including matching cert subjects
    /// </summary>
    public DbSet<Cart> Carts { get; set; } = null!;

    public DbSet<Alert> Alerts { get; set; } = null!;
    
    public DbSet<AlertCart> AlertCarts { get; set; } = null!;

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<UtcNullableDateConverter>();
        base.ConfigureConventions(configurationBuilder);
    }

    private static readonly DateTime MySqlMaxDateTime = new DateTime(9999, 12, 31, 23, 59, 59);

    private class UtcDateConverter : ValueConverter<DateTime, DateTime>
    {
        public UtcDateConverter()
            : base(d => d == DateTime.MaxValue ? MySqlMaxDateTime : d, d => DateTime.SpecifyKind(d == MySqlMaxDateTime ? DateTime.MaxValue : d, DateTimeKind.Utc))
        {}
    }
    
    private class UtcNullableDateConverter : ValueConverter<DateTime?, DateTime?>
    {
        public UtcNullableDateConverter()
            : base(d => d == DateTime.MaxValue ? MySqlMaxDateTime : d, d => d == null ? null : DateTime.SpecifyKind(d == MySqlMaxDateTime ? DateTime.MaxValue : (DateTime)d, DateTimeKind.Utc))
        {}
    }
}