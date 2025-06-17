namespace FoodRecommender.Models;

public class IntentRequest
{
    public string TranscribedText { get; set; } = string.Empty;
    public string Intent { get; set; } = string.Empty;
    public string? ZipCode { get; set; }
    public string? CuisineType { get; set; }
    public string SessionId { get; set; } = string.Empty;
} 