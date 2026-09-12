using Freegram.Data;
using Freegram.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Freegram.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private readonly FreegramDbContext _context;

    public ChatHub(FreegramDbContext context)
    {
        _context = context;
    }

    public override async Task OnConnectedAsync()
    {
        var userId =
            Context.User?.FindFirstValue(
                ClaimTypes.NameIdentifier);

        var nickname =
            Context.User?.FindFirstValue(
                ClaimTypes.Name);

        Console.WriteLine(
            $"User connected: {nickname} (ID: {userId})"
        );

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(
        Exception? exception)
    {
        var userId =
            Context.User?.FindFirstValue(
                ClaimTypes.NameIdentifier);

        var nickname =
            Context.User?.FindFirstValue(
                ClaimTypes.Name);

        Console.WriteLine(
            $"User disconnected: {nickname} (ID: {userId})"
        );

        await base.OnDisconnectedAsync(
            exception);
    }

    public async Task JoinChat(int chatId)
    {
        var userId =
            GetCurrentUserId();

        var chatExists =
            await _context.Chats
                .AnyAsync(c =>
                    c.Id == chatId);

        if (!chatExists)
        {
            throw new HubException(
                "Chat not found."
            );
        }

        var isMember =
            await _context.ChatMembers
                .AnyAsync(m =>
                    m.ChatId == chatId &&
                    m.UserId == userId);

        if (!isMember)
        {
            throw new HubException(
                "You are not a member of this chat."
            );
        }

        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            GetGroupName(chatId)
        );

        await Clients.Caller.SendAsync(
            "JoinedChat",
            chatId
        );
    }

    public async Task LeaveChat(int chatId)
    {
        await Groups.RemoveFromGroupAsync(
            Context.ConnectionId,
            GetGroupName(chatId)
        );

        await Clients.Caller.SendAsync(
            "LeftChat",
            chatId
        );
    }

    public async Task SendMessage(
        int chatId,
        string content)
    {
        var userId =
            GetCurrentUserId();

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new HubException(
                "Message content is required."
            );
        }

        content = content.Trim();

        var chat =
            await _context.Chats
                .Include(c =>
                    c.Members)
                .FirstOrDefaultAsync(c =>
                    c.Id == chatId);

        if (chat == null)
        {
            throw new HubException(
                "Chat not found."
            );
        }

        var isMember =
            chat.Members.Any(m =>
                m.UserId == userId);

        if (!isMember)
        {
            throw new HubException(
                "You are not a member of this chat."
            );
        }

        var message = new Message
        {
            ChatId = chatId,
            SenderId = userId,
            Content = content,
            CreatedAt = DateTime.UtcNow
        };

        _context.Messages.Add(message);

        await _context.SaveChangesAsync();

        var sender =
            await _context.Users
                .Where(u =>
                    u.Id == userId)
                .Select(u => new
                {
                    u.Id,
                    u.Nickname
                })
                .FirstAsync();

        await Clients.Group(
                GetGroupName(chatId))
            .SendAsync(
                "ReceiveMessage",
                new
                {
                    message.Id,
                    message.ChatId,
                    message.Content,
                    message.CreatedAt,

                    Sender = sender
                }
            );
    }

    // ==========================================
    // DELETE MESSAGE FOR EVERYONE
    // ==========================================

    public async Task DeleteMessageForEveryone(
        int chatId,
        int messageId)
    {
        var userId =
            GetCurrentUserId();

        var message =
            await _context.Messages
                .FirstOrDefaultAsync(m =>
                    m.Id == messageId &&
                    m.ChatId == chatId);

        if (message == null)
        {
            throw new HubException(
                "Message not found."
            );
        }

        var isMember =
            await _context.ChatMembers
                .AnyAsync(m =>
                    m.ChatId == chatId &&
                    m.UserId == userId);

        if (!isMember)
        {
            throw new HubException(
                "You are not a member of this chat."
            );
        }

        if (message.SenderId != userId)
        {
            throw new HubException(
                "You can delete for everyone only your own messages."
            );
        }

        _context.Messages.Remove(message);

        await _context.SaveChangesAsync();

        await Clients.Group(
                GetGroupName(chatId))
            .SendAsync(
                "MessageDeletedForEveryone",
                new
                {
                    ChatId = chatId,
                    MessageId = messageId
                }
            );
    }

    // ==========================================
    // DELETE MESSAGE FOR ME
    // ==========================================

    public async Task DeleteMessageForMe(
        int chatId,
        int messageId)
    {
        var userId =
            GetCurrentUserId();

        var message =
            await _context.Messages
                .FirstOrDefaultAsync(m =>
                    m.Id == messageId &&
                    m.ChatId == chatId);

        if (message == null)
        {
            throw new HubException(
                "Message not found."
            );
        }

        var isMember =
            await _context.ChatMembers
                .AnyAsync(m =>
                    m.ChatId == chatId &&
                    m.UserId == userId);

        if (!isMember)
        {
            throw new HubException(
                "You are not a member of this chat."
            );
        }

        if (message.SenderId == userId)
        {
            throw new HubException(
                "Your own messages must be deleted for everyone."
            );
        }

        var alreadyDeleted =
            await _context.DeletedMessages
                .AnyAsync(d =>
                    d.MessageId == messageId &&
                    d.UserId == userId);

        if (!alreadyDeleted)
        {
            var deletedMessage =
                new DeletedMessage
                {
                    MessageId = messageId,
                    UserId = userId,
                    DeletedAt =
                        DateTime.UtcNow
                };

            _context.DeletedMessages.Add(
                deletedMessage);

            await _context.SaveChangesAsync();
        }

        await Clients.Caller.SendAsync(
            "MessageDeletedForMe",
            new
            {
                ChatId = chatId,
                MessageId = messageId
            }
        );
    }

    private int GetCurrentUserId()
    {
        var userId =
            Context.User?.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (userId == null)
        {
            throw new HubException(
                "User is not authenticated."
            );
        }

        return int.Parse(userId);
    }

    private static string GetGroupName(
        int chatId)
    {
        return $"chat-{chatId}";
    }
}