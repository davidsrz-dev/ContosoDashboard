using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ContosoDashboard.Models;

public class DocumentAuditLog
{
    [Key]
    public int DocumentAuditLogId { get; set; }

    public int? DocumentId { get; set; }

    [Required]
    [MaxLength(50)]
    public string ActionType { get; set; } = string.Empty;

    [Required]
    public int UserId { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [MaxLength(1000)]
    public string? Details { get; set; }

    // Navigation properties
    [ForeignKey("DocumentId")]
    public virtual Document? Document { get; set; }

    [ForeignKey("UserId")]
    public virtual User User { get; set; } = null!;
}
