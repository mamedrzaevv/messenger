using System.Security.Claims;
using LanChat.Data;
using LanChat.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace LanChat.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private readonly ApplicationDbContext _db;

    public ChatHub(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task JoinChat(string chatId)
    {
        if (!Guid.TryParse(chatId, out var parsedId))
        {
            return;
        }

        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
        {
            return;
        }

        var isMember = await _db.ChatMembers
            .AnyAsync(member => member.ChatId == parsedId && member.UserId == userId);
        if (!isMember)
        {
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, parsedId.ToString());
    }

    public async Task LeaveChat(string chatId)
    {
        if (Guid.TryParse(chatId, out var parsedId))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, parsedId.ToString());
        }
    }

    public async Task SendMessage(string chatId, string message)
    {
        var cleanMessage = (message ?? string.Empty).Trim();
        if (cleanMessage.Length == 0)
        {
            return;
        }

        if (cleanMessage.Length > 1000)
        {
            cleanMessage = cleanMessage[..1000];
        }

        if (!Guid.TryParse(chatId, out var parsedId))
        {
            return;
        }

        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        var userName = Context.User?.Identity?.Name ?? "user";
        if (userId is null)
        {
            return;
        }

        var isMember = await _db.ChatMembers
            .AnyAsync(member => member.ChatId == parsedId && member.UserId == userId);
        if (!isMember)
        {
            return;
        }

        var messageEntity = new Message
        {
            ChatId = parsedId,
            UserId = userId,
            Text = cleanMessage,
            SentAt = DateTimeOffset.UtcNow
        };

        _db.Messages.Add(messageEntity);
        await _db.SaveChangesAsync();

        await Clients.Group(parsedId.ToString())
            .SendAsync(
                "ReceiveMessage",
                new
                {
                    id = messageEntity.Id,
                    chatId = parsedId,
                    userId,
                    userName,
                    text = messageEntity.Text,
                    sentAt = messageEntity.SentAt
                });
    }
}
