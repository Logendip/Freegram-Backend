namespace Freegram.Models;

public class ChatRequest
{
    public int Id { get; set; }

    public int? ChatId { get; set; }

    public Chat? Chat { get; set; }

    // Хто хоче почати чат
    public int SenderId { get; set; }

    public User Sender { get; set; } = null!;

    // Хто повинен прийняти або відхилити запит
    public int ReceiverId { get; set; }

    public User Receiver { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}