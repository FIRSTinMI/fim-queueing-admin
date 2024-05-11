using System.ComponentModel.DataAnnotations;

namespace fim_queueing_admin.Models;

public class CartStreamInfo
{
    public required Guid CartId { get; set; }
    public required int Index { get; set; }

    public bool Enabled { get; set; } = true;
    
    [MaxLength(512)]
    public string? RtmpUrl { get; set; }
    
    [MaxLength(512)]
    public string? RtmpKey { get; set; }
}