var builder = WebApplication.CreateBuilder(args);

// 🔹 Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSignalR(); // ✅ Register SignalR

// ✅ Enable CORS for React frontend at http://localhost:5173
builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPolicy", builder =>
        builder.WithOrigins("http://localhost:5173") // ✅ Ensure correct frontend URL
               .AllowAnyMethod()
               .AllowAnyHeader()
               .AllowCredentials()); // ✅ Needed for WebSockets authentication
});

var app = builder.Build();

// 🔹 Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors("CorsPolicy"); // ✅ Apply the CORS policy before routing
app.UseRouting();          // ✅ Enable routing

app.UseAuthorization();

app.MapControllers(); // ✅ Maps API controllers
app.MapHub<ChatHub>("/chatHub"); // ✅ Correctly map SignalR Hub

app.Run();
