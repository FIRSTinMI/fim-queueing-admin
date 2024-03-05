using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace fim_queueing_admin.Models;

[Table("CartStreamInfos")]
[PrimaryKey(nameof(CartId), nameof(Index))]
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