using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace fim_queueing_admin.Models;

[Table("AlertCarts")]
[EntityTypeConfiguration(typeof(AlertCartEntityTypeConfiguration))]
public class AlertCart
{
    public required Guid AlertId { get; set; }
    public Alert? Alert { get; set; }
    public required Guid CartId { get; set; }
    public Cart? Cart { get; set; }
    public DateTime? ReadTime { get; set; }
}

public class AlertCartEntityTypeConfiguration : IEntityTypeConfiguration<AlertCart>
{
    public void Configure(EntityTypeBuilder<AlertCart> builder)
    {
        builder.HasKey(nameof(AlertCart.AlertId), nameof(AlertCart.CartId));
    }
}