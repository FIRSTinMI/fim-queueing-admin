using fim_queueing_admin.Models;
using Microsoft.EntityFrameworkCore;

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

    public DbSet<EquipmentLog> EquipmentLogs { get; set; } = null!;
    
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties(typeof(Enum)).HaveConversion<string>();
    }
}