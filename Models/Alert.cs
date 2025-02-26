using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace fim_queueing_admin.Models;

[Table("Alerts")]
[EntityTypeConfiguration(typeof(AlertEntityTypeConfiguration))]
public class Alert
{
    public Guid Id { get; set; }
    
    public required string Content { get; set; }
    public DateTime CreatedAt { get; set; }
    public ICollection<AlertCart>? AlertCarts { get; set; }
}

public class AlertEntityTypeConfiguration : IEntityTypeConfiguration<Alert>
{
    public void Configure(EntityTypeBuilder<Alert> builder)
    {
        builder
            .ToTable("alerts", "temp")
            .HasMany(a => a.AlertCarts)
            .WithOne(ac => ac.Alert);
    }
}