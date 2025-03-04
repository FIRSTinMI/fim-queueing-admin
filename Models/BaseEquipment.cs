using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace fim_queueing_admin.Models;

/// <summary>
/// This class contains fields which all equipment share.
/// Create an inheriting class for each equipment type with its type-specific configuration.
/// </summary>
public class BaseEquipment
{
    [Column("equipment_type_id")]
    public int EquipmentType { get; set; }
    public Guid Id { get; set; }
    
    [MaxLength(255)]
    public required string Name { get; set; }
    [MaxLength(16)]
    public string? TeamviewerId { get; set; }

}

public abstract class BaseEquipmentEntityTypeConfiguration<T> : IEntityTypeConfiguration<T> where T : BaseEquipment
{
    public void Configure(EntityTypeBuilder<T> builder)
    {
        builder.ToTable("equipment");
        ConfigureEntity(builder);
    }

    protected abstract void ConfigureEntity(EntityTypeBuilder<T> builder);
}