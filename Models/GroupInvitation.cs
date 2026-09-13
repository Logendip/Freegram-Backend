namespace Freegram.Models;

public class GroupInvitation
{
    public int Id { get; set; }

    public int ChatId { get; set; }

    public Chat Chat { get; set; } = null!;

    public int InvitedUserId { get; set; }

    public User InvitedUser { get; set; } = null!;

    public int InvitedByUserId { get; set; }

    public User InvitedByUser { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}