using Freegram.Data;
using Freegram.Hubs;
using Freegram.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Freegram.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ChatsController : ControllerBase
{
    private readonly FreegramDbContext _context;
    private readonly IHubContext<ChatHub> _hubContext;

    public ChatsController(
        FreegramDbContext context,
        IHubContext<ChatHub> hubContext)
    {
        _context = context;
        _hubContext = hubContext;
    }


    // ==========================================
    // GET CHATS
    // ==========================================

    [HttpGet]
    public async Task<IActionResult> GetChats()
    {
        var currentUserId =
            GetCurrentUserId();

        var chats =
            await _context.Chats
                .Where(c =>
                    c.Members.Any(m =>
                        m.UserId == currentUserId))
                .Include(c =>
                    c.Members)
                    .ThenInclude(m =>
                        m.User)
                .Include(c =>
                    c.Creator)
                .OrderByDescending(c =>
                    c.Messages
                        .OrderByDescending(m =>
                            m.CreatedAt)
                        .Select(m =>
                            (DateTime?)m.CreatedAt)
                        .FirstOrDefault()
                        ?? c.CreatedAt)
                .ToListAsync();

        var result =
            chats.Select(chat =>
                new
                {
                    chat.Id,
                    chat.Name,
                    chat.IsGroup,
                    chat.CreatorId,
                    chat.CreatedAt,

                    Members =
                        chat.Members.Select(member =>
                            new
                            {
                                member.UserId,
                                member.User.Nickname
                            }),

                    UnreadCount =
                        0
                });

        return Ok(result);
    }


    // ==========================================
    // GET MESSAGES
    // ==========================================

    [HttpGet("{chatId}/messages")]
    public async Task<IActionResult> GetMessages(
        int chatId)
    {
        var currentUserId =
            GetCurrentUserId();

        var isMember =
            await _context.ChatMembers
                .AnyAsync(m =>
                    m.ChatId == chatId &&
                    m.UserId == currentUserId);

        if (!isMember)
        {
            return Forbid();
        }

        var messages =
            await _context.Messages
                .Where(m =>
                    m.ChatId == chatId &&
                    !m.DeletedByUsers.Any(d =>
                        d.UserId == currentUserId))
                .Include(m =>
                    m.Sender)
                .Include(m =>
                    m.ReadByUsers)
                .OrderBy(m =>
                    m.CreatedAt)
                .ToListAsync();

        var result =
            messages.Select(m =>
                new
                {
                    m.Id,
                    m.ChatId,
                    m.Content,
                    m.CreatedAt,

                    Sender = new
                    {
                        m.Sender.Id,
                        m.Sender.Nickname
                    },

                    IsRead =
                        m.SenderId == currentUserId
                            ? m.ReadByUsers.Any(r =>
                                r.UserId != currentUserId)
                            : m.ReadByUsers.Any(r =>
                                r.UserId == currentUserId)
                });

        return Ok(result);
    }


    // ==========================================
    // SEND MESSAGE
    // ==========================================

    [HttpPost("{chatId}/messages")]
    public async Task<IActionResult> SendMessage(
        int chatId,
        [FromBody] SendMessageRequest request)
    {
        var currentUserId =
            GetCurrentUserId();

        if (string.IsNullOrWhiteSpace(
                request.Content))
        {
            return BadRequest(
                "Message content is required.");
        }

        var isMember =
            await _context.ChatMembers
                .AnyAsync(m =>
                    m.ChatId == chatId &&
                    m.UserId == currentUserId);

        if (!isMember)
        {
            return Forbid();
        }

        var message =
            new Message
            {
                ChatId = chatId,
                SenderId = currentUserId,
                Content = request.Content.Trim(),
                CreatedAt = DateTime.UtcNow
            };

        _context.Messages.Add(message);

        await _context.SaveChangesAsync();

        return Ok(message);
    }


    // ==========================================
    // CREATE PRIVATE CHAT REQUEST
    // ==========================================

    [HttpPost("private")]
    public async Task<IActionResult> CreatePrivateChat(
        [FromBody] CreatePrivateChatRequest request)
    {
        var currentUserId =
            GetCurrentUserId();

        if (request.UserId == currentUserId)
        {
            return BadRequest(
                "You cannot create a chat with yourself.");
        }

        var targetUser =
            await _context.Users
                .FirstOrDefaultAsync(u =>
                    u.Id == request.UserId);

        if (targetUser == null)
        {
            return NotFound(
                "User not found.");
        }

        // ==========================================
        // CHECK EXISTING PRIVATE CHAT
        // ==========================================

        var existingChat =
            await _context.Chats
                .Where(c =>
                    !c.IsGroup &&
                    c.Members.Count == 2 &&
                    c.Members.Any(m =>
                        m.UserId == currentUserId) &&
                    c.Members.Any(m =>
                        m.UserId == request.UserId))
                .Include(c =>
                    c.Members)
                    .ThenInclude(m =>
                        m.User)
                .FirstOrDefaultAsync();

        if (existingChat != null)
        {
            return Ok(new
            {
                chat = new
                {
                    existingChat.Id,
                    existingChat.Name,
                    existingChat.IsGroup,
                    existingChat.CreatorId,
                    existingChat.CreatedAt,

                    Members =
                        existingChat.Members.Select(m =>
                            new
                            {
                                m.UserId,
                                m.User.Nickname
                            })
                }
            });
        }

        // ==========================================
        // CHECK EXISTING PENDING REQUEST
        // ==========================================

        var existingRequest =
            await _context.ChatRequests
                .FirstOrDefaultAsync(r =>
                    r.SenderId == currentUserId &&
                    r.ReceiverId == request.UserId);

        if (existingRequest != null)
        {
            return BadRequest(
                "Chat request has already been sent.");
        }

        // ==========================================
        // CREATE REQUEST ONLY
        // ==========================================

        var chatRequest =
            new ChatRequest
            {
                ChatId = null,

                SenderId =
                    currentUserId,

                ReceiverId =
                    request.UserId,

                CreatedAt =
                    DateTime.UtcNow
            };

        _context.ChatRequests.Add(
            chatRequest);

        await _context.SaveChangesAsync();

        // ==========================================
        // SIGNALR → RECEIVER
        // ==========================================

        await _hubContext.Clients
            .User(
                request.UserId.ToString())
            .SendAsync(
                "ChatRequestCreated",
                new
                {
                    Id =
                        chatRequest.Id,

                    ChatId =
                        (int?)null,

                    Sender = new
                    {
                        Id =
                            currentUserId,

                        Nickname =
                            User.FindFirstValue(
                                ClaimTypes.Name)
                    },

                    CreatedAt =
                        chatRequest.CreatedAt
                });

        return Ok(new
        {
            requestId =
                chatRequest.Id,

            message =
                "Chat request sent."
        });
    }


    // ==========================================
    // CREATE GROUP CHAT
    // ==========================================

    [HttpPost("group")]
    public async Task<IActionResult> CreateGroupChat(
        [FromBody] CreateGroupChatRequest request)
    {
        var currentUserId =
            GetCurrentUserId();

        if (string.IsNullOrWhiteSpace(
                request.Name))
        {
            return BadRequest(
                "Group name is required.");
        }

        var requestedUserIds =
            request.UserIds?
                .Distinct()
                .ToList()
            ?? new List<int>();

        requestedUserIds =
            requestedUserIds
                .Where(id =>
                    id != currentUserId)
                .ToList();

        if (requestedUserIds.Count == 0)
        {
            return BadRequest(
                "Select at least one user.");
        }

        var existingUsers =
            await _context.Users
                .Where(u =>
                    requestedUserIds.Contains(u.Id))
                .Select(u =>
                    new
                    {
                        u.Id,
                        u.Nickname
                    })
                .ToListAsync();

        var existingUserIds =
            existingUsers
                .Select(u =>
                    u.Id)
                .ToList();

        if (existingUserIds.Count !=
            requestedUserIds.Count)
        {
            return BadRequest(
                "One or more users were not found.");
        }

        var creator =
            await _context.Users
                .FirstAsync(u =>
                    u.Id == currentUserId);

        var chat =
            new Chat
            {
                Name =
                    request.Name.Trim(),

                IsGroup =
                    true,

                CreatorId =
                    currentUserId,

                CreatedAt =
                    DateTime.UtcNow
            };

        _context.Chats.Add(chat);

        await _context.SaveChangesAsync();

        var creatorMember =
            new ChatMember
            {
                ChatId =
                    chat.Id,

                UserId =
                    currentUserId,

                JoinedAt =
                    DateTime.UtcNow
            };

        _context.ChatMembers.Add(
            creatorMember);

        var invitations =
            existingUserIds.Select(userId =>
                new GroupInvitation
                {
                    ChatId =
                        chat.Id,

                    InvitedUserId =
                        userId,

                    InvitedByUserId =
                        currentUserId,

                    CreatedAt =
                        DateTime.UtcNow
                })
                .ToList();

        _context.GroupInvitations.AddRange(
            invitations);

        await _context.SaveChangesAsync();

        foreach (var invitation in invitations)
        {
            await _hubContext.Clients
                .User(
                    invitation.InvitedUserId
                        .ToString())
                .SendAsync(
                    "GroupInvitationReceived",
                    new
                    {
                        InvitationId =
                            invitation.Id,

                        ChatId =
                            chat.Id,

                        ChatName =
                            chat.Name,

                        CreatedAt =
                            invitation.CreatedAt,

                        Sender = new
                        {
                            Id =
                                creator.Id,

                            Nickname =
                                creator.Nickname
                        }
                    });
        }

        var createdChat =
            await _context.Chats
                .Where(c =>
                    c.Id == chat.Id)
                .Include(c =>
                    c.Members)
                    .ThenInclude(m =>
                        m.User)
                .FirstAsync();

        var chatData =
            new
            {
                createdChat.Id,
                createdChat.Name,
                createdChat.IsGroup,
                createdChat.CreatorId,
                createdChat.CreatedAt,

                Members =
                    createdChat.Members.Select(m =>
                        new
                        {
                            m.UserId,
                            m.User.Nickname
                        }),

                UnreadCount =
                    0
            };

        return Ok(new
        {
            chat = chatData
        });
    }


    // ==========================================
    // ADD MEMBER TO EXISTING GROUP
    // ==========================================

    [HttpPost("group/{chatId}/members")]
    public async Task<IActionResult>
        AddGroupMember(
            int chatId,
            [FromBody] AddGroupMemberRequest request)
    {
        var currentUserId =
            GetCurrentUserId();

        var chat =
            await _context.Chats
                .Include(c =>
                    c.Members)
                    .ThenInclude(m =>
                        m.User)
                .FirstOrDefaultAsync(c =>
                    c.Id == chatId);

        if (chat == null)
        {
            return NotFound(
                "Chat not found.");
        }

        if (!chat.IsGroup)
        {
            return BadRequest(
                "This chat is not a group.");
        }

        if (chat.CreatorId != currentUserId)
        {
            return Forbid();
        }

        if (request.UserId == currentUserId)
        {
            return BadRequest(
                "The group creator is already a member.");
        }

        var targetUser =
            await _context.Users
                .FirstOrDefaultAsync(u =>
                    u.Id == request.UserId);

        if (targetUser == null)
        {
            return NotFound(
                "User not found.");
        }

        var alreadyMember =
            chat.Members.Any(m =>
                m.UserId == request.UserId);

        if (alreadyMember)
        {
            return BadRequest(
                "User is already a member of this group.");
        }

        var pendingInvitation =
            await _context.GroupInvitations
                .FirstOrDefaultAsync(i =>
                    i.ChatId == chatId &&
                    i.InvitedUserId == request.UserId);

        if (pendingInvitation != null)
        {
            _context.GroupInvitations.Remove(
                pendingInvitation);
        }

        var member =
            new ChatMember
            {
                ChatId =
                    chatId,

                UserId =
                    request.UserId,

                JoinedAt =
                    DateTime.UtcNow
            };

        _context.ChatMembers.Add(
            member);

        await _context.SaveChangesAsync();

        var updatedChat =
            await _context.Chats
                .Where(c =>
                    c.Id == chatId)
                .Include(c =>
                    c.Members)
                    .ThenInclude(m =>
                        m.User)
                .FirstAsync();

        var chatData =
            new
            {
                updatedChat.Id,
                updatedChat.Name,
                updatedChat.IsGroup,
                updatedChat.CreatorId,
                updatedChat.CreatedAt,

                Members =
                    updatedChat.Members.Select(m =>
                        new
                        {
                            m.UserId,
                            m.User.Nickname
                        }),

                UnreadCount =
                    0
            };

        var addedUser =
            new
            {
                Id =
                    targetUser.Id,

                Nickname =
                    targetUser.Nickname
            };

        await _hubContext.Clients
            .User(
                request.UserId.ToString())
            .SendAsync(
                "GroupMemberAdded",
                new
                {
                    Chat = chatData,
                    User = addedUser
                });

        var existingMemberIds =
            updatedChat.Members
                .Where(m =>
                    m.UserId != request.UserId)
                .Select(m =>
                    m.UserId.ToString())
                .ToList();

        if (existingMemberIds.Count > 0)
        {
            await _hubContext.Clients
                .Users(existingMemberIds)
                .SendAsync(
                    "GroupMemberAdded",
                    new
                    {
                        ChatId =
                            chatId,

                        User =
                            addedUser
                    });
        }

        return Ok(new
        {
            chat = chatData
        });
    }


    // ==========================================
    // GET PRIVATE CHAT REQUESTS
    // ==========================================

    [HttpGet("requests")]
    public async Task<IActionResult> GetChatRequests()
    {
        var currentUserId =
            GetCurrentUserId();

        var requests =
            await _context.ChatRequests
                .Where(r =>
                    r.ReceiverId ==
                    currentUserId)
                .Include(r =>
                    r.Sender)
                .OrderByDescending(r =>
                    r.CreatedAt)
                .Select(r =>
                    new
                    {
                        r.Id,
                        r.ChatId,
                        r.CreatedAt,

                        Sender = new
                        {
                            r.Sender.Id,
                            r.Sender.Nickname
                        }
                    })
                .ToListAsync();

        return Ok(requests);
    }


    // ==========================================
    // ACCEPT PRIVATE CHAT REQUEST
    // ==========================================

    [HttpPost("requests/{requestId}/accept")]
    public async Task<IActionResult> AcceptChatRequest(
        int requestId)
    {
        var currentUserId =
            GetCurrentUserId();

        var request =
            await _context.ChatRequests
                .Include(r =>
                    r.Sender)
                .FirstOrDefaultAsync(r =>
                    r.Id == requestId &&
                    r.ReceiverId ==
                    currentUserId);

        if (request == null)
        {
            return NotFound(new
            {
                message =
                    "Chat request not found."
            });
        }

        // ==========================================
        // CHECK EXISTING PRIVATE CHAT
        // ==========================================

        var existingChat =
            await _context.Chats
                .Where(c =>
                    !c.IsGroup &&
                    c.Members.Count == 2 &&
                    c.Members.Any(m =>
                        m.UserId == request.SenderId) &&
                    c.Members.Any(m =>
                        m.UserId == currentUserId))
                .Include(c =>
                    c.Members)
                    .ThenInclude(m =>
                        m.User)
                .FirstOrDefaultAsync();

        Chat chat;

        if (existingChat != null)
        {
            chat =
                existingChat;
        }
        else
        {
            // ==========================================
            // CREATE CHAT ONLY AFTER ACCEPT
            // ==========================================

            chat =
                new Chat
                {
                    IsGroup = false,

                    CreatorId =
                        request.SenderId,

                    CreatedAt =
                        DateTime.UtcNow
                };

            _context.Chats.Add(chat);

            await _context.SaveChangesAsync();

            // ==========================================
            // ADD SENDER
            // ==========================================

            _context.ChatMembers.Add(
                new ChatMember
                {
                    ChatId =
                        chat.Id,

                    UserId =
                        request.SenderId,

                    JoinedAt =
                        DateTime.UtcNow
                });

            // ==========================================
            // ADD RECEIVER
            // ==========================================

            _context.ChatMembers.Add(
                new ChatMember
                {
                    ChatId =
                        chat.Id,

                    UserId =
                        currentUserId,

                    JoinedAt =
                        DateTime.UtcNow
                });
        }

        // ==========================================
        // REMOVE REQUEST
        // ==========================================

        _context.ChatRequests.Remove(
            request);

        await _context.SaveChangesAsync();

        // ==========================================
        // LOAD CHAT
        // ==========================================

        chat =
            await _context.Chats
                .Where(c =>
                    c.Id == chat.Id)
                .Include(c =>
                    c.Members)
                    .ThenInclude(m =>
                        m.User)
                .FirstAsync();

        var chatData =
            new
            {
                chat.Id,
                chat.Name,
                chat.IsGroup,
                chat.CreatorId,
                chat.CreatedAt,

                Members =
                    chat.Members.Select(m =>
                        new
                        {
                            m.UserId,
                            m.User.Nickname
                        }),

                UnreadCount =
                    0
            };

        // ==========================================
        // SIGNALR → SENDER
        // ==========================================

        await _hubContext.Clients
            .User(
                request.SenderId.ToString())
            .SendAsync(
                "ChatRequestAccepted",
                new
                {
                    ChatId =
                        chat.Id,

                    UserId =
                        currentUserId,

                    Chat =
                        chatData
                });

        return Ok(new
        {
            chat = chatData
        });
    }


    // ==========================================
    // REJECT PRIVATE CHAT REQUEST
    // ==========================================

    [HttpDelete("requests/{requestId}")]
    public async Task<IActionResult> RejectChatRequest(
        int requestId)
    {
        var currentUserId =
            GetCurrentUserId();

        var request =
            await _context.ChatRequests
                .Include(r =>
                    r.Receiver)
                .FirstOrDefaultAsync(r =>
                    r.Id == requestId &&
                    r.ReceiverId ==
                    currentUserId);

        if (request == null)
        {
            return NotFound(new
            {
                message =
                    "Chat request not found."
            });
        }

        var receiverNickname =
            request.Receiver.Nickname;

        var senderId =
            request.SenderId;

        _context.ChatRequests.Remove(
            request);

        await _context.SaveChangesAsync();

        // ==========================================
        // SIGNALR → SENDER
        // ==========================================

        await _hubContext.Clients
            .User(
                senderId.ToString())
            .SendAsync(
                "ChatRequestRejected",
                new
                {
                    RequestId =
                        requestId,

                    UserId =
                        currentUserId,

                    Nickname =
                        receiverNickname,

                    Message =
                        $"{receiverNickname} відхилив(ла) ваше запрошення."
                });

        return Ok(new
        {
            message =
                "Chat request rejected.",

            requestId =
                requestId
        });
    }


    // ==========================================
    // GET GROUP INVITATIONS
    // ==========================================

    [HttpGet("group-invitations")]
    public async Task<IActionResult>
        GetGroupInvitations()
    {
        var currentUserId =
            GetCurrentUserId();

        var invitations =
            await _context.GroupInvitations
                .Where(i =>
                    i.InvitedUserId ==
                    currentUserId)
                .Include(i =>
                    i.Chat)
                .Include(i =>
                    i.InvitedByUser)
                .OrderByDescending(i =>
                    i.CreatedAt)
                .Select(i =>
                    new
                    {
                        InvitationId =
                            i.Id,

                        ChatId =
                            i.ChatId,

                        ChatName =
                            i.Chat.Name,

                        CreatedAt =
                            i.CreatedAt,

                        Sender = new
                        {
                            Id =
                                i.InvitedByUser.Id,

                            Nickname =
                                i.InvitedByUser.Nickname
                        }
                    })
                .ToListAsync();

        return Ok(invitations);
    }


    // ==========================================
    // ACCEPT GROUP INVITATION
    // ==========================================

    [HttpPost(
        "group-invitations/{invitationId}/accept")]
    public async Task<IActionResult>
        AcceptGroupInvitation(
            int invitationId)
    {
        var currentUserId =
            GetCurrentUserId();

        var invitation =
            await _context.GroupInvitations
                .Include(i =>
                    i.Chat)
                    .ThenInclude(c =>
                        c.Members)
                .FirstOrDefaultAsync(i =>
                    i.Id == invitationId &&
                    i.InvitedUserId ==
                    currentUserId);

        if (invitation == null)
        {
            return NotFound(
                new
                {
                    message =
                        "Group invitation not found."
                });
        }

        if (invitation.Chat == null)
        {
            _context.GroupInvitations.Remove(
                invitation);

            await _context.SaveChangesAsync();

            return NotFound(
                new
                {
                    message =
                        "Group no longer exists."
                });
        }

        if (!invitation.Chat.IsGroup)
        {
            _context.GroupInvitations.Remove(
                invitation);

            await _context.SaveChangesAsync();

            return BadRequest(
                new
                {
                    message =
                        "This chat is not a group."
                });
        }

        var alreadyMember =
            invitation.Chat.Members.Any(m =>
                m.UserId == currentUserId);

        if (!alreadyMember)
        {
            _context.ChatMembers.Add(
                new ChatMember
                {
                    ChatId =
                        invitation.ChatId,

                    UserId =
                        currentUserId,

                    JoinedAt =
                        DateTime.UtcNow
                });
        }

        _context.GroupInvitations.Remove(
            invitation);

        await _context.SaveChangesAsync();

        var chat =
            await _context.Chats
                .Where(c =>
                    c.Id == invitation.ChatId)
                .Include(c =>
                    c.Members)
                    .ThenInclude(m =>
                        m.User)
                .FirstOrDefaultAsync();

        if (chat == null)
        {
            return NotFound(
                new
                {
                    message =
                        "Group no longer exists."
                });
        }

        var chatData =
            new
            {
                chat.Id,
                chat.Name,
                chat.IsGroup,
                chat.CreatorId,
                chat.CreatedAt,

                Members =
                    chat.Members.Select(m =>
                        new
                        {
                            m.UserId,
                            m.User.Nickname
                        }),

                UnreadCount =
                    0
            };

        if (chat.CreatorId.HasValue)
        {
            var user =
                await _context.Users
                    .Where(u =>
                        u.Id == currentUserId)
                    .Select(u =>
                        new
                        {
                            u.Id,
                            u.Nickname
                        })
                    .FirstAsync();

            await _hubContext.Clients
                .User(
                    chat.CreatorId.Value
                        .ToString())
                .SendAsync(
                    "GroupInvitationAccepted",
                    new
                    {
                        ChatId =
                            chat.Id,

                        User =
                            user
                    });
        }

        return Ok(new
        {
            chat = chatData
        });
    }


    // ==========================================
    // IGNORE GROUP INVITATION
    // ==========================================

    [HttpDelete(
        "group-invitations/{invitationId}")]
    public async Task<IActionResult>
        IgnoreGroupInvitation(
            int invitationId)
    {
        var currentUserId =
            GetCurrentUserId();

        var invitation =
            await _context.GroupInvitations
                .FirstOrDefaultAsync(i =>
                    i.Id == invitationId &&
                    i.InvitedUserId ==
                    currentUserId);

        if (invitation == null)
        {
            return NotFound(
                new
                {
                    message =
                        "Group invitation not found."
                });
        }

        _context.GroupInvitations.Remove(
            invitation);

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message =
                "Group invitation rejected.",

            invitationId =
                invitationId
        });
    }


    // ==========================================
    // REMOVE MEMBER FROM GROUP
    // ==========================================

    [HttpDelete(
        "group/{chatId}/members/{userId}")]
    public async Task<IActionResult>
        RemoveGroupMember(
            int chatId,
            int userId)
    {
        var currentUserId =
            GetCurrentUserId();

        var chat =
            await _context.Chats
                .Include(c =>
                    c.Members)
                .FirstOrDefaultAsync(c =>
                    c.Id == chatId);

        if (chat == null)
        {
            return NotFound(
                "Chat not found.");
        }

        if (!chat.IsGroup)
        {
            return BadRequest(
                "This chat is not a group.");
        }

        if (chat.CreatorId != currentUserId)
        {
            return Forbid();
        }

        if (userId == chat.CreatorId)
        {
            return BadRequest(
                "The group creator cannot be removed.");
        }

        var member =
            chat.Members
                .FirstOrDefault(m =>
                    m.UserId == userId);

        if (member == null)
        {
            return NotFound(
                "User is not a member of this group.");
        }

        _context.ChatMembers.Remove(
            member);

        var invitation =
            await _context.GroupInvitations
                .FirstOrDefaultAsync(i =>
                    i.ChatId == chatId &&
                    i.InvitedUserId == userId);

        if (invitation != null)
        {
            _context.GroupInvitations.Remove(
                invitation);
        }

        await _context.SaveChangesAsync();

        await _hubContext.Clients
            .User(userId.ToString())
            .SendAsync(
                "GroupMemberRemoved",
                new
                {
                    ChatId = chatId,
                    UserId = userId
                });

        var remainingMemberIds =
            chat.Members
                .Where(m =>
                    m.UserId != userId)
                .Select(m =>
                    m.UserId.ToString())
                .ToList();

        if (remainingMemberIds.Count > 0)
        {
            await _hubContext.Clients
                .Users(remainingMemberIds)
                .SendAsync(
                    "GroupMemberRemoved",
                    new
                    {
                        ChatId = chatId,
                        UserId = userId
                    });
        }

        return Ok(new
        {
            message =
                "User removed from group."
        });
    }


    // ==========================================
    // DELETE CHAT / GROUP
    // ==========================================

    [HttpDelete("{chatId}")]
    public async Task<IActionResult> DeleteChat(
        int chatId)
    {
        var currentUserId =
            GetCurrentUserId();

        var chat =
            await _context.Chats
                .Include(c =>
                    c.Members)
                .FirstOrDefaultAsync(c =>
                    c.Id == chatId);

        if (chat == null)
        {
            return NotFound(
                "Chat not found.");
        }

        var isMember =
            chat.Members.Any(m =>
                m.UserId == currentUserId);

        if (!isMember)
        {
            return Forbid();
        }

        if (chat.IsGroup &&
            chat.CreatorId != currentUserId)
        {
            return Forbid();
        }

        var memberUserIds =
            chat.Members
                .Select(m =>
                    m.UserId.ToString())
                .ToList();

        _context.Chats.Remove(chat);

        await _context.SaveChangesAsync();

        if (memberUserIds.Count > 0)
        {
            await _hubContext.Clients
                .Users(memberUserIds)
                .SendAsync(
                    "ChatDeleted",
                    new
                    {
                        ChatId = chatId
                    });
        }

        return Ok(new
        {
            message =
                "Chat deleted."
        });
    }


    // ==========================================
    // CURRENT USER
    // ==========================================

    private int GetCurrentUserId()
    {
        var userId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (userId == null)
        {
            throw new UnauthorizedAccessException();
        }

        return int.Parse(userId);
    }
}


// ==========================================
// REQUEST MODELS
// ==========================================

public class SendMessageRequest
{
    public string Content { get; set; } =
        string.Empty;
}


public class CreatePrivateChatRequest
{
    public int UserId { get; set; }
}


public class CreateGroupChatRequest
{
    public string Name { get; set; } =
        string.Empty;

    public List<int> UserIds { get; set; } =
        new List<int>();
}


public class AddGroupMemberRequest
{
    public int UserId { get; set; }
}