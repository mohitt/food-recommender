using Newtonsoft.Json;

namespace FoodRecommender.Models;

public class YelpResponse
{
    [JsonProperty("businesses")]
    public List<YelpBusiness> Businesses { get; set; } = new();
}

public class YelpBusiness
{
    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;
    
    [JsonProperty("rating")]
    public float Rating { get; set; }
    
    [JsonProperty("review_count")]
    public int ReviewCount { get; set; }
    
    [JsonProperty("categories")]
    public List<YelpCategory> Categories { get; set; } = new();
    
    [JsonProperty("location")]
    public YelpLocation Location { get; set; } = new();
    
    [JsonProperty("phone")]
    public string Phone { get; set; } = string.Empty;
    
    [JsonProperty("price")]
    public string? Price { get; set; }
}

public class YelpCategory
{
    [JsonProperty("title")]
    public string Title { get; set; } = string.Empty;
}

public class YelpLocation
{
    [JsonProperty("address1")]
    public string Address1 { get; set; } = string.Empty;
    
    [JsonProperty("city")]
    public string City { get; set; } = string.Empty;
    
    [JsonProperty("state")]
    public string State { get; set; } = string.Empty;
    
    [JsonProperty("zip_code")]
    public string ZipCode { get; set; } = string.Empty;
} 