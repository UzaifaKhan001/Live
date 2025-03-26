using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public class ChatHub : Hub
{
    private readonly string _connectionString = "Server=UZAIFA-KHAN\\MSSQLSERVER01;Database=ChatAppDB;Integrated Security=True;TrustServerCertificate=True;";

    private static ConcurrentDictionary<string, string> _connectedUsers = new ConcurrentDictionary<string, string>();

    public override async Task OnConnectedAsync()
    {
        Console.WriteLine($"User connected: {Context.ConnectionId}");
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (_connectedUsers.TryRemove(Context.ConnectionId, out string username))
        {
            Console.WriteLine($"{username} disconnected");
        }
        await base.OnDisconnectedAsync(exception);
    }

    public async Task RegisterUser(string username)
    {
        if (!_connectedUsers.Any(u => u.Value == username))
        {
            _connectedUsers[Context.ConnectionId] = username;
            Console.WriteLine($"{username} registered with Connection ID: {Context.ConnectionId}");

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                string query = "INSERT INTO Users (Username, ConnectionId) VALUES (@Username, @ConnectionId)";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Username", username);
                    cmd.Parameters.AddWithValue("@ConnectionId", Context.ConnectionId);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }
    }

    public async Task SendMessage(string sender, string recipient, string message, bool isImage, string imageUrl)
    {
        if (string.IsNullOrWhiteSpace(sender) || string.IsNullOrWhiteSpace(recipient))
        {
            return;
        }

        try
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                string query = "INSERT INTO Messages (Sender, Recipient, Message, IsImage, ImageUrl, Timestamp) VALUES (@Sender, @Recipient, @Message, @IsImage, @ImageUrl, @Timestamp)";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Sender", sender);
                    cmd.Parameters.AddWithValue("@Recipient", recipient);
                    cmd.Parameters.AddWithValue("@Message", message ?? string.Empty);
                    cmd.Parameters.AddWithValue("@IsImage", isImage);
                    cmd.Parameters.AddWithValue("@ImageUrl", imageUrl ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@Timestamp", DateTime.UtcNow);
                    await cmd.ExecuteNonQueryAsync();
                }
            }

            var recipientConnectionId = _connectedUsers.FirstOrDefault(x => x.Value == recipient).Key;
            var senderConnectionId = _connectedUsers.FirstOrDefault(x => x.Value == sender).Key;

            if (!string.IsNullOrEmpty(recipientConnectionId))
            {
                await Clients.Client(recipientConnectionId).SendAsync("ReceiveMessage", sender, message, isImage, imageUrl);
            }

            if (!string.IsNullOrEmpty(senderConnectionId))
            {
                await Clients.Client(senderConnectionId).SendAsync("ReceiveMessage", sender, message, isImage, imageUrl);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Database error: {ex.Message}");
        }
    }
}