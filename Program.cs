using FoodRecommender.Hubs;
using FoodRecommender.Services;
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
builder.Services.AddSignalR();
builder.Services.AddHttpClient();

// Register our services
builder.Services.AddScoped<OpenAIService>();
builder.Services.AddScoped<YelpService>();
builder.Services.AddScoped<AudioProcessingService>();

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

// Serve static files
app.UseDefaultFiles();
app.UseStaticFiles();

// Map SignalR hub
app.MapHub<AudioHub>("/audiohub");

// Simple health check endpoint
app.MapGet("/health", () => "Food Recommender API is running!");

app.Run();
