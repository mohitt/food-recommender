using OpenAI;
using OpenAI.Audio;
using OpenAI.Chat;
using FoodRecommender.Models;
using System.Text;

namespace FoodRecommender.Services;

public class OpenAIService
{
    private readonly OpenAIClient _openAIClient;
    private readonly AudioClient _audioClient;
    private readonly ChatClient _chatClient;

    public OpenAIService(IConfiguration configuration)
    {
        // Try to get API key from environment variable first, then configuration
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") 
                    ?? configuration["OpenAI:ApiKey"] 
                    ?? throw new InvalidOperationException("OpenAI API key not configured. Set OPENAI_API_KEY environment variable or configure in appsettings.json");
        
        _openAIClient = new OpenAIClient(apiKey);
        _audioClient = _openAIClient.GetAudioClient("whisper-1");
        _chatClient = _openAIClient.GetChatClient("gpt-4");
    }

    public async Task<string> TranscribeAudioAsync(byte[] audioData)
    {
        try
        {
            using var stream = new MemoryStream(audioData);
            var audioTranscription = await _audioClient.TranscribeAudioAsync(stream, "audio.wav");
            
            return audioTranscription.Value.Text;
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to transcribe audio: {ex.Message}", ex);
        }
    }

    public async Task<IntentRequest> AnalyzeIntentAsync(string transcribedText, string sessionId)
    {
        try
        {
            var systemPrompt = @"You are an AI assistant that analyzes user intent for a food recommendation system. 
            Analyze the following text and extract:
            1. Intent: Either 'finding cuisine near me' or 'find specific cuisine near me' or 'unknown'
            2. Zip code if mentioned
            3. Cuisine type if mentioned (e.g., Italian, Chinese, Mexican, etc.)
            
            Return your response in JSON format like this:
            {
                ""intent"": ""finding cuisine near me"",
                ""zipCode"": ""12345"",
                ""cuisineType"": ""Italian""
            }
            
            If no zip code or cuisine type is mentioned, set them to null.";

            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(systemPrompt),
                new UserChatMessage($"Analyze this text: {transcribedText}")
            };

            var response = await _chatClient.CompleteChatAsync(messages);
            var responseText = response.Value.Content[0].Text;

            // Parse the JSON response
            var intentData = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(responseText);
            
            return new IntentRequest
            {
                TranscribedText = transcribedText,
                Intent = intentData?.intent ?? "unknown",
                ZipCode = intentData?.zipCode,
                CuisineType = intentData?.cuisineType,
                SessionId = sessionId
            };
        }
        catch (Exception)
        {
            return new IntentRequest
            {
                TranscribedText = transcribedText,
                Intent = "unknown",
                SessionId = sessionId
            };
        }
    }

    public async Task<string> GenerateResponseTextAsync(List<YelpBusiness> businesses, string intent)
    {
        try
        {
            var businessInfo = string.Join("\n", businesses.Select(b => 
                $"• {b.Name} - Rating: {b.Rating}/5 ({b.ReviewCount} reviews) - {string.Join(", ", b.Categories.Select(c => c.Title))} - {b.Location.Address1}, {b.Location.City}, {b.Location.State}"));

            var prompt = $@"Based on the user's intent '{intent}' and the following restaurant information, 
            create a friendly, human-readable bulleted response recommending these restaurants:

            {businessInfo}

            Make it conversational and helpful. Highlight the top-rated places and mention key details like ratings and cuisine types.";

            var messages = new List<ChatMessage>
            {
                new UserChatMessage(prompt)
            };

            var response = await _chatClient.CompleteChatAsync(messages);
            return response.Value.Content[0].Text;
        }
        catch (Exception)
        {
            return "I found some great restaurants for you, but I'm having trouble formatting the response right now.";
        }
    }

    public async Task<byte[]> ConvertTextToSpeechAsync(string text)
    {
        try
        {
            var audioResponse = await _audioClient.GenerateSpeechAsync(text, GeneratedSpeechVoice.Alloy);
            
            using var memoryStream = new MemoryStream();
            await audioResponse.Value.ToStream().CopyToAsync(memoryStream);
            return memoryStream.ToArray();
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to convert text to speech: {ex.Message}", ex);
        }
    }
} 