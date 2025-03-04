using System.ComponentModel.DataAnnotations;
using System.Text.Json;
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
    
    public string? Category { get; set; }
    
    public LogSeverity Severity { get; set; } = LogSeverity.Info;
    
    public JsonElement? ExtraInfo { get; set; }
}

public enum LogSeverity
{
    Debug,
    Info,
    Warn,
    Error,
    Fatal
}

public class EquipmentLogEntityTypeConfiguration : IEntityTypeConfiguration<EquipmentLog>
{
    public void Configure(EntityTypeBuilder<EquipmentLog> builder)
    {
        builder.ToTable("equipment_logs");
    }
}