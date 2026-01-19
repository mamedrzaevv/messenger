using System.Security.Claims;
using LanChat.Data;
using LanChat.Hubs;
using LanChat.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("Default");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddIdentity<AppUser, IdentityRole>(options =>
    {
        options.Password.RequireDigit = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireLowercase = false;
        options.Password.RequiredLength = 6;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddAuthentication();
builder.Services.AddAuthorization();
builder.Services.AddSignalR();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();

app.MapHub<ChatHub>("/chat");

app.MapPost("/api/auth/register", async (
    UserManager<AppUser> userManager,
    SignInManager<AppUser> signInManager,
    RegisterRequest request) =>
{
    if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.Password))
    {
        return Results.BadRequest(new { error = "Username and password required." });
    }

    var user = new AppUser { UserName = request.UserName.Trim() };
    var result = await userManager.CreateAsync(user, request.Password);
    if (!result.Succeeded)
    {
        return Results.BadRequest(new { error = string.Join(", ", result.Errors.Select(e => e.Description)) });
    }

    await signInManager.SignInAsync(user, isPersistent: true);
    return Results.Ok(new { user.UserName, user.Id });
});

app.MapPost("/api/auth/login", async (
    SignInManager<AppUser> signInManager,
    UserManager<AppUser> userManager,
    LoginRequest request) =>
{
    var user = await userManager.FindByNameAsync(request.UserName ?? string.Empty);
    if (user is null)
    {
        return Results.BadRequest(new { error = "Invalid credentials." });
    }

    var result = await signInManager.PasswordSignInAsync(user, request.Password ?? string.Empty, true, false);
    if (!result.Succeeded)
    {
        return Results.BadRequest(new { error = "Invalid credentials." });
    }

    return Results.Ok(new { user.UserName, user.Id });
});

app.MapPost("/api/auth/logout", async (SignInManager<AppUser> signInManager) =>
{
    await signInManager.SignOutAsync();
    return Results.Ok();
});

app.MapGet("/api/me", (ClaimsPrincipal user) =>
{
    var name = user.Identity?.Name;
    var id = user.FindFirstValue(ClaimTypes.NameIdentifier);
    return Results.Ok(new { id, userName = name });
}).RequireAuthorization();

app.MapGet("/api/users/search", async (
    ClaimsPrincipal user,
    UserManager<AppUser> userManager,
    string query) =>
{
    var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
    if (userId is null)
    {
        return Results.Unauthorized();
    }

    var cleanQuery = query?.Trim();
    if (string.IsNullOrWhiteSpace(cleanQuery))
    {
        return Results.Ok(Array.Empty<object>());
    }

    var users = await userManager.Users
        .Where(u => u.Id != userId && u.UserName != null)
        .Where(u => EF.Functions.ILike(u.UserName!, $"%{cleanQuery}%"))
        .OrderBy(u => u.UserName)
        .Select(u => new { u.Id, userName = u.UserName })
        .Take(10)
        .ToListAsync();

    return Results.Ok(users);
}).RequireAuthorization();

app.MapGet("/api/chats", async (
    ClaimsPrincipal user,
    ApplicationDbContext db) =>
{
    var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
    if (userId is null)
    {
        return Results.Unauthorized();
    }

    var chats = await db.Chats
        .Where(chat => chat.Members.Any(member => member.UserId == userId))
        .Select(chat => new
        {
            chat.Id,
            chat.Title,
            MemberCount = chat.Members.Count,
            OtherUserName = chat.Members
                .Where(member => member.UserId != userId)
                .Select(member => member.User!.UserName)
                .FirstOrDefault(),
            LastMessageAt = chat.Messages
                .OrderByDescending(message => message.SentAt)
                .Select(message => (DateTimeOffset?)message.SentAt)
                .FirstOrDefault()
        })
        .OrderByDescending(chat => chat.LastMessageAt)
        .ToListAsync();

    var response = chats.Select(chat => new
    {
        chat.Id,
        Title = chat.MemberCount == 2 && !string.IsNullOrWhiteSpace(chat.OtherUserName)
            ? chat.OtherUserName
            : chat.Title,
        chat.LastMessageAt
    });

    return Results.Ok(response);
}).RequireAuthorization();

app.MapPost("/api/chats", async (
    ClaimsPrincipal user,
    ApplicationDbContext db,
    UserManager<AppUser> userManager,
    CreateChatRequest request) =>
{
    var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
    if (userId is null)
    {
        return Results.Unauthorized();
    }

    var title = string.IsNullOrWhiteSpace(request.Title)
        ? "Новый чат"
        : request.Title.Trim();

    var chat = new Chat { Title = title };
    db.Chats.Add(chat);

    var members = new List<ChatMember>
    {
        new ChatMember { ChatId = chat.Id, UserId = userId }
    };

    if (request.UserNames is not null)
    {
        foreach (var name in request.UserNames.Select(n => n?.Trim()).Where(n => !string.IsNullOrWhiteSpace(n)))
        {
            var otherUser = await userManager.FindByNameAsync(name!);
            if (otherUser is not null && otherUser.Id != userId)
            {
                members.Add(new ChatMember { ChatId = chat.Id, UserId = otherUser.Id });
            }
        }
    }

    db.ChatMembers.AddRange(members.DistinctBy(m => m.UserId));
    await db.SaveChangesAsync();

    return Results.Ok(new { chat.Id, chat.Title });
}).RequireAuthorization();

app.MapGet("/api/chats/{chatId:guid}/messages", async (
    ClaimsPrincipal user,
    ApplicationDbContext db,
    Guid chatId) =>
{
    var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
    if (userId is null)
    {
        return Results.Unauthorized();
    }

    var isMember = await db.ChatMembers
        .AnyAsync(member => member.ChatId == chatId && member.UserId == userId);
    if (!isMember)
    {
        return Results.Forbid();
    }

    var messages = await db.Messages
        .Where(message => message.ChatId == chatId)
        .OrderByDescending(message => message.SentAt)
        .Take(50)
        .Select(message => new
        {
            message.Id,
            message.ChatId,
            message.Text,
            message.SentAt,
            message.UserId,
            UserName = message.User!.UserName
        })
        .OrderBy(message => message.SentAt)
        .ToListAsync();

    return Results.Ok(messages);
}).RequireAuthorization();

app.MapFallbackToFile("index.html");

app.Run();

record RegisterRequest(string UserName, string Password);
record LoginRequest(string UserName, string Password);
record CreateChatRequest(string Title, List<string>? UserNames);
