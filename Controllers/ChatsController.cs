
using Freegram.Data;
using Freegram.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Freegram.Hubs;
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
    // GET CURRENT USER ID
    // ==========================================

    private int GetCurrentUserId()
    {
        var userId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (userId == null)
        {
            throw new UnauthorizedAccessException(
                "User is not authenticated."
            );
        }

        return int.Parse(userId);
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
                .Include(c => c.Members)
                    .ThenInclude(m => m.User)
                .Where(c =>
                    c.Members.Any(m =>
                        m.UserId ==
                        currentUserId))
                .OrderByDescending(c =>
                    c.CreatedAt)
                .Select(c => new
                {
                    c.Id,
                    c.Name,
                    c.IsGroup,
                    c.CreatedAt,

                    Members =
                        c.Members
                            .Where(m =>
                                m.User != null)
                            .Select(m => new
                            {
                                m.User.Id,
                                m.User.Nickname
                            })
                            .ToList()
                })
                .ToListAsync();

        return Ok(chats);
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
                    m.UserId ==
                    currentUserId);

        if (!isMember)
        {
            return Forbid();
        }

        var messages =
            await _context.Messages
                .Include(m => m.Sender)
                .Where(m =>
                    m.ChatId == chatId &&
                    !m.DeletedByUsers.Any(d =>
                        d.UserId ==
                        currentUserId))
                .OrderBy(m =>
                    m.CreatedAt)
                .Select(m => new
                {
                    m.Id,
                    m.ChatId,
                    m.Content,
                    m.CreatedAt,

                    Sender = new
                    {
                        m.Sender.Id,
                        m.Sender.Nickname
                    }
                })
                .ToListAsync();

        return Ok(messages);
    }


    // ==========================================
    // SEND MESSAGE
    // ==========================================

    [HttpPost("{chatId}/messages")]
    public async Task<IActionResult> SendMessage(
        int chatId,
        [FromBody] string content)
    {
        var currentUserId =
            GetCurrentUserId();

        if (string.IsNullOrWhiteSpace(content))
        {
            return BadRequest(new
            {
                message =
                    "Message content is required."
            });
        }

        var isMember =
            await _context.ChatMembers
                .AnyAsync(m =>
                    m.ChatId == chatId &&
                    m.UserId ==
                    currentUserId);

        if (!isMember)
        {
            return Forbid();
        }

        var message =
            new Message
            {
                ChatId =
                    chatId,

                SenderId =
                    currentUserId,

                Content =
                    content.Trim(),

                CreatedAt =
                    DateTime.UtcNow
            };

        _context.Messages.Add(
            message);

        await _context.SaveChangesAsync();

        var sender =
            await _context.Users
                .Where(u =>
                    u.Id ==
                    currentUserId)
                .Select(u => new
                {
                    u.Id,
                    u.Nickname
                })
                .FirstAsync();

        return Ok(new
        {
            message.Id,
            message.ChatId,
            message.Content,
            message.CreatedAt,
            Sender = sender
        });
    }


    // ==========================================
    // CREATE PRIVATE CHAT / CHAT REQUEST
    // ==========================================

    [HttpPost("private/{userId}")]
    public async Task<IActionResult> CreatePrivateChat(
        int userId)
    {
        var currentUserId =
            GetCurrentUserId();

        if (currentUserId == userId)
        {
            return BadRequest(new
            {
                message =
                    "You cannot create a chat with yourself."
            });
        }

        var targetUser =
            await _context.Users
                .FirstOrDefaultAsync(u =>
                    u.Id == userId);

        if (targetUser == null)
        {
            return NotFound(new
            {
                message =
                    "User not found."
            });
        }


        // ==========================================
        // CHECK EXISTING ACTIVE CHAT
        // ==========================================

        var existingChat =
            await _context.Chats
                .Include(c => c.Members)
                    .ThenInclude(m => m.User)
                .Where(c =>
                    !c.IsGroup &&
                    c.Members.Any(m =>
                        m.UserId ==
                        currentUserId) &&
                    c.Members.Any(m =>
                        m.UserId ==
                        userId))
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
                    existingChat.CreatedAt,

                    Members =
                        existingChat.Members
                            .Where(m =>
                                m.User != null)
                            .Select(m => new
                            {
                                m.User.Id,
                                m.User.Nickname
                            })
                            .ToList()
                },

                pendingRequest = false
            });
        }


        // ==========================================
        // CHECK EXISTING REQUEST
        // ==========================================

        var existingRequest =
            await _context.ChatRequests
                .Include(r => r.Chat)
                    .ThenInclude(c => c.Members)
                        .ThenInclude(m => m.User)
                .FirstOrDefaultAsync(r =>
                    r.SenderId ==
                    currentUserId &&
                    r.ReceiverId ==
                    userId);

        if (existingRequest != null)
        {
            var pendingChat =
                existingRequest.Chat;

            return Ok(new
            {
                chat = new
                {
                    pendingChat.Id,
                    pendingChat.Name,
                    pendingChat.IsGroup,
                    pendingChat.CreatedAt,

                    Members =
                        pendingChat.Members
                            .Where(m =>
                                m.User != null)
                            .Select(m => new
                            {
                                m.User.Id,
                                m.User.Nickname
                            })
                            .ToList()
                },

                pendingRequest = true
            });
        }


        // ==========================================
        // CREATE CHAT
        // ==========================================

        var chat =
            new Chat
            {
                Name = null,
                IsGroup = false,
                CreatedAt =
                    DateTime.UtcNow
            };

        _context.Chats.Add(chat);

        await _context.SaveChangesAsync();


        // ==========================================
        // ADD SENDER AS MEMBER
        // ==========================================

        var senderMember =
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
            senderMember);


        // ==========================================
        // CREATE REQUEST
        // ==========================================

        var request =
            new ChatRequest
            {
                ChatId =
                    chat.Id,

                SenderId =
                    currentUserId,

                ReceiverId =
                    userId,

                CreatedAt =
                    DateTime.UtcNow
            };

        _context.ChatRequests.Add(
            request);

        await _context.SaveChangesAsync();


        // ==========================================
        // LOAD CHAT WITH MEMBERS
        // ==========================================

        var createdChat =
            await _context.Chats
                .Include(c => c.Members)
                    .ThenInclude(m => m.User)
                .FirstAsync(c =>
                    c.Id == chat.Id);


        // ==========================================
        // SEND REALTIME REQUEST
        // ==========================================

        await _hubContext.Clients
            .User(
                userId.ToString()
            )
            .SendAsync(
                "ChatRequestCreated",
                new
                {
                    RequestId =
                        request.Id,

                    ChatId =
                        chat.Id,

                    Sender = new
                    {
                        Id =
                            currentUserId,

                        Nickname =
                            User.FindFirstValue(
                                ClaimTypes.Name)
                    }
                }
            );


        // ==========================================
        // RETURN CHAT
        // ==========================================

        return Ok(new
        {
            chat = new
            {
                createdChat.Id,
                createdChat.Name,
                createdChat.IsGroup,
                createdChat.CreatedAt,

                Members =
                    createdChat.Members
                        .Where(m =>
                            m.User != null)
                        .Select(m => new
                        {
                            m.User.Id,
                            m.User.Nickname
                        })
                        .ToList()
            },

            pendingRequest = true
        });
    }


    // ==========================================
    // GET CHAT REQUESTS
    // ==========================================

    [HttpGet("requests")]
    public async Task<IActionResult> GetChatRequests()
    {
        var currentUserId =
            GetCurrentUserId();

        var requests =
            await _context.ChatRequests
                .Include(r => r.Sender)
                .Include(r => r.Chat)
                .Where(r =>
                    r.ReceiverId ==
                    currentUserId)
                .OrderByDescending(r =>
                    r.CreatedAt)
                .Select(r => new
                {
                    RequestId =
                        r.Id,

                    ChatId =
                        r.ChatId,

                    CreatedAt =
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
    // ACCEPT CHAT REQUEST
    // ==========================================

    [HttpPost("requests/{requestId}/accept")]
    public async Task<IActionResult> AcceptChatRequest(
        int requestId)
    {
        var currentUserId =
            GetCurrentUserId();

        var request =
            await _context.ChatRequests
                .Include(r => r.Chat)
                    .ThenInclude(c => c.Members)
                        .ThenInclude(m => m.User)
                .Include(r => r.Sender)
                .Include(r => r.Receiver)
                .FirstOrDefaultAsync(r =>
                    r.Id == requestId);

        if (request == null)
        {
            return NotFound(new
            {
                message =
                    "Chat request not found."
            });
        }

        if (request.ReceiverId !=
            currentUserId)
        {
            return Forbid();
        }


        // ==========================================
        // ADD RECEIVER TO CHAT
        // ==========================================

        var alreadyMember =
            await _context.ChatMembers
                .AnyAsync(m =>
                    m.ChatId ==
                    request.ChatId &&
                    m.UserId ==
                    currentUserId);

        if (!alreadyMember)
        {
            var newMember =
                new ChatMember
                {
                    ChatId =
                        request.ChatId,

                    UserId =
                        currentUserId,

                    JoinedAt =
                        DateTime.UtcNow
                };

            _context.ChatMembers.Add(
                newMember);

            await _context.SaveChangesAsync();
        }


        // ==========================================
        // DELETE REQUEST
        // ==========================================

        _context.ChatRequests.Remove(
            request);

        await _context.SaveChangesAsync();


        // ==========================================
        // LOAD CHAT AGAIN
        // ==========================================

        var chat =
            await _context.Chats
                .Include(c => c.Members)
                    .ThenInclude(m => m.User)
                .FirstOrDefaultAsync(c =>
                    c.Id ==
                    request.ChatId);

        if (chat == null)
        {
            return NotFound(new
            {
                message =
                    "Chat not found."
            });
        }


        // ==========================================
        // NOTIFY SENDER
        // ==========================================

        await _hubContext.Clients
            .User(
                request.SenderId
                    .ToString()
            )
            .SendAsync(
                "ChatRequestAccepted",
                new
                {
                    ChatId =
                        chat.Id,

                    UserId =
                        currentUserId
                }
            );


        // ==========================================
        // RETURN CHAT
        // ==========================================

        return Ok(new
        {
            message =
                "Chat request accepted.",

            chat = new
            {
                chat.Id,
                chat.Name,
                chat.IsGroup,
                chat.CreatedAt,

                Members =
                    chat.Members
                        .Where(m =>
                            m.User != null)
                        .Select(m => new
                        {
                            m.User.Id,
                            m.User.Nickname
                        })
                        .ToList()
            }
        });
    }


    // ==========================================
    // REJECT CHAT REQUEST
    // ==========================================

    [HttpDelete("requests/{requestId}")]
    public async Task<IActionResult> RejectChatRequest(
        int requestId)
    {
        var currentUserId =
            GetCurrentUserId();

        var request =
            await _context.ChatRequests
                .FirstOrDefaultAsync(r =>
                    r.Id == requestId);

        if (request == null)
        {
            return NotFound(new
            {
                message =
                    "Chat request not found."
            });
        }

        if (request.ReceiverId !=
            currentUserId)
        {
            return Forbid();
        }

        var chatId =
            request.ChatId;

        var senderId =
            request.SenderId;

        _context.ChatRequests.Remove(
            request);

        await _context.SaveChangesAsync();


        // ==========================================
        // DELETE TEMPORARY CHAT
        // ==========================================

        var chat =
            await _context.Chats
                .FirstOrDefaultAsync(c =>
                    c.Id == chatId);

        if (chat != null)
        {
            _context.Chats.Remove(chat);

            await _context.SaveChangesAsync();
        }


        // ==========================================
        // NOTIFY SENDER
        // ==========================================

        await _hubContext.Clients
            .User(
                senderId.ToString()
            )
            .SendAsync(
                "ChatDeleted",
                chatId
            );

        return Ok(new
        {
            message =
                "Chat request rejected."
        });
    }


    // ==========================================
    // DELETE CHAT
    // ==========================================

    [HttpDelete("{chatId}")]
    public async Task<IActionResult> DeleteChat(
        int chatId)
    {
        var currentUserId =
            GetCurrentUserId();

        var chat =
            await _context.Chats
                .Include(c => c.Members)
                .FirstOrDefaultAsync(c =>
                    c.Id == chatId);

        if (chat == null)
        {
            return NotFound(new
            {
                message =
                    "Chat not found."
            });
        }

        var isMember =
            chat.Members.Any(m =>
                m.UserId ==
                currentUserId);

        if (!isMember)
        {
            return Forbid();
        }


        // ==========================================
        // FIND OTHER MEMBER
        // ==========================================

        var otherMember =
            chat.Members
                .FirstOrDefault(m =>
                    m.UserId !=
                    currentUserId);

        var otherUserId =
            otherMember?.UserId;


        // ==========================================
        // CHECK PENDING REQUEST
        // ==========================================

        var pendingRequest =
            await _context.ChatRequests
                .FirstOrDefaultAsync(r =>
                    r.ChatId ==
                    chatId);


        // ==========================================
        // DELETE CHAT
        // ==========================================

        _context.Chats.Remove(chat);

        await _context.SaveChangesAsync();


        // ==========================================
        // NOTIFY OTHER USER
        // ==========================================

        if (otherUserId.HasValue)
        {
            await _hubContext.Clients
                .User(
                    otherUserId.Value
                        .ToString()
                )
                .SendAsync(
                    "ChatDeleted",
                    chatId
                );
        }
        else if (pendingRequest != null)
        {
            await _hubContext.Clients
                .User(
                    pendingRequest.ReceiverId
                        .ToString()
                )
                .SendAsync(
                    "ChatDeleted",
                    chatId
                );
        }

        return Ok(new
        {
            message =
                "Chat deleted."
        });
    }
}
