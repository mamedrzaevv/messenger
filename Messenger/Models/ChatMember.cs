namespace LanChat.Models;

public class ChatMember
{
    public int Id { get; set; }
    public Guid ChatId { get; set; }
    public string UserId { get; set; } = string.Empty;

    public Chat? Chat { get; set; }
    public AppUser? User { get; set; }
}
