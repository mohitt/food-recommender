using FoodRecommender.Services;
using FoodRecommender.WebSockets;
using DotNetEnv;

// Load environment variables from .env files
// .env.local takes precedence over .env
if (File.Exists(".env.local"))
{
    Env.Load(".env.local");
}
else if (File.Exists(".env"))
{
    Env.Load(".env");
}

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddHttpClient();

// Register our services
builder.Services.AddScoped<OpenAIService>();
builder.Services.AddScoped<YelpService>();
builder.Services.AddScoped<AudioProcessingService>();
builder.Services.AddSingleton<FoodRecommender.WebSockets.WebSocketManager>();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseRouting();
app.UseCors();

// Enable WebSocket support
app.UseWebSockets();

// Serve static files
app.UseDefaultFiles();
app.UseStaticFiles();

// WebSocket endpoint
app.Use(async (context, next) =>
{
    if (context.Request.Path == "/ws")
    {
        if (context.WebSockets.IsWebSocketRequest)
        {
            var webSocketManager = context.RequestServices.GetRequiredService<FoodRecommender.WebSockets.WebSocketManager>();
            await webSocketManager.HandleWebSocketAsync(context);
        }
        else
        {
            context.Response.StatusCode = 400;
        }
    }
    else
    {
        await next(context);
    }
});

// Simple health check endpoint
app.MapGet("/health", () => "Food Recommender API is running!");

app.Run();
