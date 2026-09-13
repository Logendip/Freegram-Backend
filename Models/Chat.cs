namespace Freegram.Models;

public class Chat
{
    public int Id { get; set; }

    public string? Name { get; set; }

    public bool IsGroup { get; set; }

    public int? CreatorId { get; set; }

    public User? Creator { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ChatMember> Members { get; set; }
        = new List<ChatMember>();

    public ICollection<Message> Messages { get; set; }
        = new List<Message>();
}