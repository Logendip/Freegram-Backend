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

    public DbSet<DeletedMessage> DeletedMessages { get; set; }

    public DbSet<ChatRequest> ChatRequests { get; set; }


    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);


        // ==========================================
        // DeletedMessage
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
        // ChatRequest
        // ==========================================

        // Один chat не може мати два одночасних
        // запити.
        modelBuilder.Entity<ChatRequest>()
            .HasIndex(r => r.ChatId)
            .IsUnique();


        // ChatRequest -> Chat
        modelBuilder.Entity<ChatRequest>()
            .HasOne(r => r.Chat)
            .WithMany()
            .HasForeignKey(r => r.ChatId)
            .OnDelete(DeleteBehavior.Cascade);


        // ChatRequest -> Sender
        modelBuilder.Entity<ChatRequest>()
            .HasOne(r => r.Sender)
            .WithMany(u => u.SentChatRequests)
            .HasForeignKey(r => r.SenderId)
            .OnDelete(DeleteBehavior.Cascade);


        // ChatRequest -> Receiver
        modelBuilder.Entity<ChatRequest>()
            .HasOne(r => r.Receiver)
            .WithMany(u => u.ReceivedChatRequests)
            .HasForeignKey(r => r.ReceiverId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}