namespace FoodRecommender.Models;

public class AudioMessage
{
    public string Type { get; set; } = string.Empty;
    public byte[] AudioData { get; set; } = Array.Empty<byte>();
    public bool IsLast { get; set; }
    public string SessionId { get; set; } = string.Empty;
} 