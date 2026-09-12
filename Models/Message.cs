namespace Freegram.Models;

public class Message
{
    public int Id { get; set; }

    public int ChatId { get; set; }
    public Chat Chat { get; set; } = null!;

    public int SenderId { get; set; }
    public User Sender { get; set; } = null!;

    public string Content { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<DeletedMessage> DeletedByUsers { get; set; }
        = new List<DeletedMessage>();
}