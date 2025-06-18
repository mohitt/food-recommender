using FoodRecommender.Models;
using Newtonsoft.Json;

namespace FoodRecommender.Services;

public class YelpService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;

    public YelpService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        
        // Try to get API key from environment variable first, then configuration
        _apiKey = Environment.GetEnvironmentVariable("YELP_API_KEY") 
                 ?? configuration["Yelp:ApiKey"] 
                 ?? throw new InvalidOperationException("Yelp API key not configured. Set YELP_API_KEY environment variable or configure in appsettings.json");
        
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
    }

    public async Task<List<YelpBusiness>> SearchRestaurantsAsync(string zipCode, string? cuisineType = null)
    {
        try
        {
            var term = string.IsNullOrEmpty(cuisineType) ? "restaurants" : $"{cuisineType} restaurants";
            var url = $"https://api.yelp.com/v3/businesses/search?term={Uri.EscapeDataString(term)}&location={zipCode}&sort_by=rating&limit=10";

            Console.WriteLine($"🍕 [Yelp] Searching restaurants - Term: '{term}', Location: {zipCode}");
            Console.WriteLine($"🌐 [Yelp] API URL: {url}");

            var response = await _httpClient.GetAsync(url);
            
            Console.WriteLine($"📡 [Yelp] API Response Status: {response.StatusCode}");
            
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"❌ [Yelp] API error: {response.StatusCode} - {response.ReasonPhrase}");
                throw new Exception($"Yelp API error: {response.StatusCode}");
            }

            var content = await response.Content.ReadAsStringAsync();
            var yelpResponse = JsonConvert.DeserializeObject<YelpResponse>(content);
            var businesses = yelpResponse?.Businesses ?? new List<YelpBusiness>();

            Console.WriteLine($"🏪 [Yelp] Found {businesses.Count} restaurants");
            
            foreach (var business in businesses.Take(3)) // Log first 3 for brevity
            {
                Console.WriteLine($"   - {business.Name} (Rating: {business.Rating}/5, Reviews: {business.ReviewCount})");
            }
            
            if (businesses.Count > 3)
            {
                Console.WriteLine($"   ... and {businesses.Count - 3} more restaurants");
            }

            return businesses;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [Yelp] Failed to search restaurants: {ex.Message}");
            throw new Exception($"Failed to search restaurants: {ex.Message}", ex);
        }
    }

    public async Task<List<YelpBusiness>> GetTopRatedRestaurantsAsync(string zipCode)
    {
        return await SearchRestaurantsAsync(zipCode);
    }

    public async Task<List<YelpBusiness>> GetCuisineSpecificRestaurantsAsync(string zipCode, string cuisineType)
    {
        return await SearchRestaurantsAsync(zipCode, cuisineType);
    }
} 