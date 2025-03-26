using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Threading.Tasks;

[Route("api/chat")]
[ApiController]
public class ChatController : ControllerBase
{
    private readonly string _connectionString = "Server=UZAIFA-KHAN\\MSSQLSERVER01;Database=ChatAppDB;Integrated Security=True;TrustServerCertificate=True;";

    // 📌 Register a new user
    [HttpPost("register")]
    public async Task<IActionResult> RegisterUser([FromBody] UserRegistrationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username))
            return BadRequest("Username cannot be empty.");

        try
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                // Check if user already exists
                string checkQuery = "SELECT COUNT(*) FROM Users WHERE Username = @Username";
                using (SqlCommand checkCmd = new SqlCommand(checkQuery, conn))
                {
                    checkCmd.Parameters.AddWithValue("@Username", request.Username);
                    int count = (int)await checkCmd.ExecuteScalarAsync();
                    if (count > 0)
                        return Conflict("Username already exists.");
                }

                // Insert new user
                string query = "INSERT INTO Users (Username) VALUES (@Username)";
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Username", request.Username);
                    await cmd.ExecuteNonQueryAsync();
                }
            }

            return Ok(new { message = "User registered successfully", username = request.Username });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Database error: {ex.Message}");
        }
    }

    // 📌 Login
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] UserLoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username))
            return BadRequest("Username cannot be empty.");

        try
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                // Check if user exists
                string query = "SELECT Username FROM Users WHERE Username = @Username";
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Username", request.Username);
                    var result = await cmd.ExecuteScalarAsync();
                    if (result == null)
                        return NotFound("User not found.");
                }
            }

            return Ok(new { message = "Login successful", username = request.Username });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Database error: {ex.Message}");
        }
    }

    // 📌 Get contacts
    [HttpGet("contacts")]
    public async Task<IActionResult> GetContacts(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
            return BadRequest("Username cannot be empty.");

        List<string> contacts = new List<string>();

        try
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                string query = @"
                    SELECT DISTINCT Recipient AS Contact 
                    FROM Messages 
                    WHERE Sender = @Username
                    UNION
                    SELECT DISTINCT Sender AS Contact 
                    FROM Messages 
                    WHERE Recipient = @Username";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Username", username);

                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            contacts.Add(reader["Contact"].ToString());
                        }
                    }
                }
            }

            return Ok(contacts);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Database error: {ex.Message}");
        }
    }

    // 📌 Send or Reply to a Message
    [HttpPost("send")]
    public async Task<IActionResult> SendMessage([FromForm] MessageRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Sender) || string.IsNullOrWhiteSpace(request.Recipient))
            return BadRequest("Sender and recipient cannot be empty.");

        if (string.IsNullOrWhiteSpace(request.Message) && request.Image == null)
            return BadRequest("Message or image cannot be empty.");

        string? imageUrl = null; // ✅ Allow imageUrl to be null

        try
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                // Check if the recipient exists in the database
                string checkRecipientQuery = "SELECT COUNT(*) FROM Users WHERE Username = @Recipient";
                using (SqlCommand checkRecipientCmd = new SqlCommand(checkRecipientQuery, conn))
                {
                    checkRecipientCmd.Parameters.AddWithValue("@Recipient", request.Recipient);
                    int recipientCount = (int)await checkRecipientCmd.ExecuteScalarAsync();

                    if (recipientCount == 0)
                    {
                        return NotFound("Recipient does not exist.");
                    }
                }

                // Handle image upload (only if user provides an image)
                if (request.Image != null)
                {
                    string uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images");

                    // Ensure the images directory exists
                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                    }

                    var fileName = Guid.NewGuid().ToString() + Path.GetExtension(request.Image.FileName);
                    var filePath = Path.Combine(uploadsFolder, fileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await request.Image.CopyToAsync(stream);
                    }

                    imageUrl = $"/images/{fileName}"; // ✅ Assign the image URL
                }

                // Insert the message into the database
                string insertMessageQuery = "INSERT INTO Messages (Sender, Recipient, Message, ImageUrl, Timestamp) VALUES (@Sender, @Recipient, @Message, @ImageUrl, @Timestamp)";
                using (SqlCommand cmd = new SqlCommand(insertMessageQuery, conn))
                {
                    cmd.Parameters.AddWithValue("@Sender", request.Sender);
                    cmd.Parameters.AddWithValue("@Recipient", request.Recipient);
                    cmd.Parameters.AddWithValue("@Message", request.Message ?? string.Empty);
                    cmd.Parameters.AddWithValue("@ImageUrl", (object?)imageUrl ?? DBNull.Value); // ✅ Store NULL if no image
                    cmd.Parameters.AddWithValue("@Timestamp", DateTime.UtcNow);

                    await cmd.ExecuteNonQueryAsync();
                }
            }

            return Ok(new { message = "Message sent successfully", imageUrl });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Database error: {ex.Message}");
        }
    }


    [HttpGet("message-count/{username}")]
    public async Task<IActionResult> GetMessageCount(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
            return BadRequest("Username cannot be empty.");

        try
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                string query = "SELECT COUNT(*) FROM Messages WHERE Recipient = @Username";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Username", username);
                    int messageCount = (int)await cmd.ExecuteScalarAsync();
                    return Ok(new { count = messageCount });
                }
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, "Internal Server Error: " + ex.Message);
        }
    }



    // 📌 Search users
    [HttpGet("search")]
    public async Task<IActionResult> SearchUsers(string query, string currentUser)
    {
        if (string.IsNullOrWhiteSpace(query))
            return BadRequest("Search query cannot be empty.");

        if (string.IsNullOrWhiteSpace(currentUser))
            return BadRequest("Current user cannot be empty.");

        List<string> users = new List<string>();

        try
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                string querySql = @"
                    SELECT Username 
                    FROM Users 
                    WHERE Username LIKE @Query 
                    AND Username != @CurrentUser";

                using (SqlCommand cmd = new SqlCommand(querySql, conn))
                {
                    cmd.Parameters.AddWithValue("@Query", $"%{query}%");
                    cmd.Parameters.AddWithValue("@CurrentUser", currentUser);

                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            users.Add(reader["Username"].ToString());
                        }
                    }
                }
            }

            return Ok(users);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Database error: {ex.Message}");
        }
    }

    // 📌 Get all messages between sender and recipient
    [HttpGet("messages")]
    public async Task<IActionResult> GetMessages(string sender, string recipient)
    {
        if (string.IsNullOrWhiteSpace(sender) || string.IsNullOrWhiteSpace(recipient))
            return BadRequest("Sender and recipient cannot be empty.");

        List<object> messages = new List<object>();

        try
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                string query = @"
                    SELECT Sender, Recipient, Message, ImageUrl, Timestamp 
                    FROM Messages 
                    WHERE (Sender = @Sender AND Recipient = @Recipient) 
                       OR (Sender = @Recipient AND Recipient = @Sender) 
                    ORDER BY Timestamp ASC";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Sender", sender);
                    cmd.Parameters.AddWithValue("@Recipient", recipient);

                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            messages.Add(new
                            {
                                Sender = reader["Sender"].ToString(),
                                Recipient = reader["Recipient"].ToString(),
                                Message = reader["Message"].ToString(),
                                ImageUrl = reader["ImageUrl"] == DBNull.Value ? null : reader["ImageUrl"].ToString(),
                                Timestamp = ((DateTime)reader["Timestamp"]).ToString("yyyy-MM-dd HH:mm:ss")
                            });
                        }
                    }
                }
            }

            return Ok(messages);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Database error: {ex.Message}");
        }
    }
}

// DTO for User Registration
public class UserRegistrationRequest
{
    public string Username { get; set; }
}

// DTO for Sending Messages
public class MessageRequest
{
    public string Sender { get; set; }
    public string Recipient { get; set; }
    public string Message { get; set; }

    [FromForm] // ✅ Ensures form-data binding but allows null
    public IFormFile? Image { get; set; }
}

// DTO for User Login
public class UserLoginRequest
{
    public string Username { get; set; }
}