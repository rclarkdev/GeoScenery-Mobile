using System.ComponentModel.DataAnnotations;

namespace GeoScenery.Data.Models;

public class Message
{
    public long Id { get; set; }

    public long SenderId { get; set; }

    public User Sender { get; set; } = null!;

    public long RecipientId { get; set; }

    public User Recipient { get; set; } = null!;

    [Required, MaxLength(2000)]
    public string Body { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}