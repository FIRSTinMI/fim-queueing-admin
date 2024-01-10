using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace fim_queueing_admin.Models;

[Table("Carts")]
[EntityTypeConfiguration(typeof(CartEntityTypeConfiguration))]
public class Cart
{
    public Guid Id { get; set; }
    
    [MaxLength(255)]
    public required string Name { get; set; }
    
    [MaxLength(100)]
    public required string AuthToken { get; set; }
    
    public DateTime? LastSeen { get; set; }
    
    public ICollection<AlertCart>? AlertCarts { get; set; }
}

public class CartEntityTypeConfiguration : IEntityTypeConfiguration<Cart>
{
    public void Configure(EntityTypeBuilder<Cart> builder)
    {
        builder
            .HasMany(a => a.AlertCarts)
            .WithOne(ac => ac.Cart);
    }
}