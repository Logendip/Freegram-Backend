namespace Freegram.Models;

public class User
{
    public int Id { get; set; }

    public string Nickname { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ChatMember> ChatMemberships { get; set; }
        = new List<ChatMember>();

    public ICollection<Message> Messages { get; set; }
        = new List<Message>();

    public ICollection<MessageRead> ReadMessages { get; set; }
        = new List<MessageRead>();

    public ICollection<DeletedMessage> DeletedMessages { get; set; }
        = new List<DeletedMessage>();

    public ICollection<Chat> CreatedChats { get; set; }
        = new List<Chat>();

    public ICollection<ChatRequest> SentChatRequests { get; set; }
        = new List<ChatRequest>();

    public ICollection<ChatRequest> ReceivedChatRequests { get; set; }
        = new List<ChatRequest>();
}