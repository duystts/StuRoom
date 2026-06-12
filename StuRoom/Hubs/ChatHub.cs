using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using StuRoom.Data;
using StuRoom.Models;
using System.Security.Claims;

namespace StuRoom.Hubs;

[Authorize]
public class ChatHub(ApplicationDbContext db) : Hub
{
    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
    }

    public async Task SendMessage(string receiverId, string message)
    {
        var senderId = Context.UserIdentifier;
        if (string.IsNullOrEmpty(senderId) || string.IsNullOrEmpty(receiverId) || string.IsNullOrWhiteSpace(message))
            return;

        var chatMsg = new ChatMessage
        {
            SenderId = senderId,
            ReceiverId = receiverId,
            Message = message,
            CreatedAt = DateTime.UtcNow,
            IsRead = false
        };

        db.ChatMessages.Add(chatMsg);
        await db.SaveChangesAsync();

        // Send to receiver
        await Clients.User(receiverId).SendAsync("ReceiveMessage", senderId, message, chatMsg.CreatedAt);
        
        // Echo back to sender
        await Clients.Caller.SendAsync("MessageSent", receiverId, message, chatMsg.CreatedAt);
    }
}
