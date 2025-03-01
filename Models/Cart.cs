using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace fim_queueing_admin.Models;

[EntityTypeConfiguration(typeof(CartEntityTypeConfiguration))]
public class Cart : BaseEquipment
{
    public AvCartConfiguration? Configuration { get; set; } = new();

    public ICollection<AlertCart>? AlertCarts { get; set; }
    public ICollection<EquipmentLog>? EquipmentLogs { get; set; }
    
    public class AvCartConfiguration
    {
        public string AuthToken { get; set; }
        public string AssistantVersion { get; set; }
        public DateTime? LastSeen { get; set; }
        public IEnumerable<CartStreamInfo>? StreamInfo { get; set; }
    }
}

public class CartEntityTypeConfiguration : BaseEquipmentEntityTypeConfiguration<Cart>
{
    protected override void ConfigureEntity(EntityTypeBuilder<Cart> builder)
    {
        builder
            .HasMany(a => a.AlertCarts)
            .WithOne(ac => ac.Cart);
        builder.HasQueryFilter(c => c.EquipmentType == EquipmentType.AvCart);
        builder.OwnsOne(c => c.Configuration, d =>
        {
            d.ToJson();
            d.OwnsMany<CartStreamInfo>(c => c.StreamInfo);
        });
        builder.HasMany<EquipmentLog>(c => c.EquipmentLogs).WithOne().HasForeignKey(l => l.EquipmentId);
    }
}