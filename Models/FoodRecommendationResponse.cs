namespace FoodRecommender.Models;

public class FoodRecommendationResponse
{
    public string SessionId { get; set; } = string.Empty;
    public string ResponseText { get; set; } = string.Empty;
    public byte[] AudioData { get; set; } = Array.Empty<byte>();
    public bool IsComplete { get; set; }
    public string Type { get; set; } = "response";
} 