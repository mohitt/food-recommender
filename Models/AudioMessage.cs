namespace FoodRecommender.Models;

public class AudioMessage
{
    public string Type { get; set; } = string.Empty;
    public int[] AudioData { get; set; } = Array.Empty<int>();
    public bool IsLast { get; set; }
    public string SessionId { get; set; } = string.Empty;
    
    // Helper method to convert to byte array
    public byte[] GetAudioBytes()
    {
        return AudioData.Select(i => (byte)i).ToArray();
    }
} 