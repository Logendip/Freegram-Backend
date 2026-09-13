using Freegram.Models;
using Microsoft.EntityFrameworkCore;

namespace Freegram.Data;

public class FreegramDbContext : DbContext
{
    public FreegramDbContext(
        DbContextOptions<FreegramDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users { get; set; }

    public DbSet<Chat> Chats { get; set; }

    public DbSet<ChatMember> ChatMembers { get; set; }

    public DbSet<Message> Messages { get; set; }

    public DbSet<MessageRead> MessageReads { get; set; }

    public DbSet<DeletedMessage> DeletedMessages { get; set; }

    public DbSet<ChatRequest> ChatRequests { get; set; }

    public DbSet<GroupInvitation> GroupInvitations { get; set; }


    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);


        // ==========================================
        // CHAT -> CREATOR
        // ==========================================

        modelBuilder.Entity<Chat>()
            .HasOne(c => c.Creator)
            .WithMany(u => u.CreatedChats)
            .HasForeignKey(c => c.CreatorId)
            .OnDelete(DeleteBehavior.Restrict);


        // ==========================================
        // MESSAGE READ
        // ==========================================

        modelBuilder.Entity<MessageRead>()
            .HasIndex(r => new
            {
                r.MessageId,
                r.UserId
            })
            .IsUnique();

        modelBuilder.Entity<MessageRead>()
            .HasOne(r => r.Message)
            .WithMany(m => m.ReadByUsers)
            .HasForeignKey(r => r.MessageId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<MessageRead>()
            .HasOne(r => r.User)
            .WithMany(u => u.ReadMessages)
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);


        // ==========================================
        // DELETED MESSAGE
        // ==========================================

        modelBuilder.Entity<DeletedMessage>()
            .HasIndex(d => new
            {
                d.MessageId,
                d.UserId
            })
            .IsUnique();

        modelBuilder.Entity<DeletedMessage>()
            .HasOne(d => d.Message)
            .WithMany(m => m.DeletedByUsers)
            .HasForeignKey(d => d.MessageId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DeletedMessage>()
            .HasOne(d => d.User)
            .WithMany(u => u.DeletedMessages)
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.Cascade);


        // ==========================================
        // CHAT REQUEST
        // ==========================================

        modelBuilder.Entity<ChatRequest>()
            .HasIndex(r => r.ChatId)
            .IsUnique();

        modelBuilder.Entity<ChatRequest>()
            .HasOne(r => r.Chat)
            .WithMany()
            .HasForeignKey(r => r.ChatId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ChatRequest>()
            .HasOne(r => r.Sender)
            .WithMany(u => u.SentChatRequests)
            .HasForeignKey(r => r.SenderId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ChatRequest>()
            .HasOne(r => r.Receiver)
            .WithMany(u => u.ReceivedChatRequests)
            .HasForeignKey(r => r.ReceiverId)
            .OnDelete(DeleteBehavior.Cascade);


        // ==========================================
        // GROUP INVITATION
        // ==========================================

        modelBuilder.Entity<GroupInvitation>()
            .HasIndex(i => new
            {
                i.ChatId,
                i.InvitedUserId
            })
            .IsUnique();

        modelBuilder.Entity<GroupInvitation>()
            .HasOne(i => i.Chat)
            .WithMany()
            .HasForeignKey(i => i.ChatId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<GroupInvitation>()
            .HasOne(i => i.InvitedUser)
            .WithMany()
            .HasForeignKey(i => i.InvitedUserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<GroupInvitation>()
            .HasOne(i => i.InvitedByUser)
            .WithMany()
            .HasForeignKey(i => i.InvitedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}