using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace fim_queueing_admin.Models;

public class EquipmentLog
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    public Guid EquipmentId { get; set; }
    
    [MaxLength(500)]
    public required string LogMessage { get; set; }
    
    public DateTime LogTimeUtc { get; set; } = DateTime.UtcNow;
}

public class AssistantLogEntityTypeConfiguration : IEntityTypeConfiguration<EquipmentLog>
{
    public void Configure(EntityTypeBuilder<EquipmentLog> builder)
    {
        builder.ToTable("equipment_logs");
    }
}