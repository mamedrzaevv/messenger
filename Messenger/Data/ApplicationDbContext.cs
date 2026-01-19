using LanChat.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace LanChat.Data;

public class ApplicationDbContext : IdentityDbContext<AppUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Chat> Chats => Set<Chat>();
    public DbSet<ChatMember> ChatMembers => Set<ChatMember>();
    public DbSet<Message> Messages => Set<Message>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Chat>(entity =>
        {
            entity.HasKey(chat => chat.Id);
            entity.Property(chat => chat.Title).HasMaxLength(120);
        });

        builder.Entity<ChatMember>(entity =>
        {
            entity.HasKey(member => member.Id);
            entity.HasIndex(member => new { member.ChatId, member.UserId }).IsUnique();
        });

        builder.Entity<Message>(entity =>
        {
            entity.HasKey(message => message.Id);
            entity.Property(message => message.Text).HasMaxLength(1000);
        });
    }
}
