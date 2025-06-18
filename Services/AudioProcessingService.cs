using FoodRecommender.Models;
using System.Collections.Concurrent;

namespace FoodRecommender.Services;

public class AudioProcessingService
{
    private readonly ConcurrentDictionary<string, List<byte[]>> _audioChunks = new();
    private readonly OpenAIService _openAIService;
    private readonly YelpService _yelpService;

    public AudioProcessingService(OpenAIService openAIService, YelpService yelpService)
    {
        _openAIService = openAIService;
        _yelpService = yelpService;
    }

    public void AddAudioChunk(string sessionId, byte[] audioChunk)
    {
        var chunkCount = _audioChunks.AddOrUpdate(
            sessionId,
            new List<byte[]> { audioChunk },
            (key, existing) =>
            {
                existing.Add(audioChunk);
                return existing;
            }).Count;
            
        Console.WriteLine($"📦 [AudioProcessor] Added audio chunk #{chunkCount} for session {sessionId} - Size: {audioChunk.Length} bytes");
    }

        public async Task<FoodRecommendationResponse> ProcessCompleteAudioAsync(string sessionId)
    {
        try
        {
            Console.WriteLine($"🔄 [AudioProcessor] Starting complete audio processing for session: {sessionId}");
            
            if (!_audioChunks.TryGetValue(sessionId, out var chunks))
            {
                Console.WriteLine($"❌ [AudioProcessor] No audio chunks found for session: {sessionId}");
                return await CreateErrorResponse(sessionId, "No audio data found for session");
            }

            Console.WriteLine($"📦 [AudioProcessor] Found {chunks.Count} audio chunks for session: {sessionId}");

            // Combine all audio chunks
            var combinedAudio = CombineAudioChunks(chunks);
            Console.WriteLine($"🔗 [AudioProcessor] Combined audio chunks - Total size: {combinedAudio.Length} bytes");
            
            // Transcribe audio using OpenAI Whisper
            var transcribedText = await _openAIService.TranscribeAudioAsync(combinedAudio);
            
            if (string.IsNullOrWhiteSpace(transcribedText))
            {
                Console.WriteLine($"❌ [AudioProcessor] Transcription returned empty text for session: {sessionId}");
                return await CreateErrorResponse(sessionId, "Could not transcribe audio");
            }

            // Analyze intent using OpenAI GPT
            var intentRequest = await _openAIService.AnalyzeIntentAsync(transcribedText, sessionId);
            
            // Process based on intent
            Console.WriteLine($"🎯 [AudioProcessor] Processing intent: {intentRequest.Intent}");
            var responseText = await ProcessIntentAsync(intentRequest);
            Console.WriteLine($"💬 [AudioProcessor] Generated response text: \"{responseText.Substring(0, Math.Min(100, responseText.Length))}{(responseText.Length > 100 ? "..." : "")}\"");
            
            // Convert response to speech
            var audioData = await _openAIService.ConvertTextToSpeechAsync(responseText);

            // Clean up chunks
            _audioChunks.TryRemove(sessionId, out _);
            Console.WriteLine($"🧹 [AudioProcessor] Cleaned up audio chunks for session: {sessionId}");

            Console.WriteLine($"✅ [AudioProcessor] Complete audio processing finished successfully for session: {sessionId}");
            
            return new FoodRecommendationResponse
            {
                SessionId = sessionId,
                ResponseText = responseText,
                AudioData = audioData,
                IsComplete = true
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [AudioProcessor] Error processing audio for session {sessionId}: {ex.Message}");
            _audioChunks.TryRemove(sessionId, out _);
            return await CreateErrorResponse(sessionId, $"Error processing audio: {ex.Message}");
        }
    }

    private async Task<string> ProcessIntentAsync(IntentRequest intentRequest)
    {
        try
        {
            if (string.IsNullOrEmpty(intentRequest.ZipCode))
            {
                return "I'd be happy to help you find restaurants! Could you please provide your zip code so I can find places near you?";
            }

            List<YelpBusiness> restaurants;

            switch (intentRequest.Intent)
            {
                case "finding cuisine near me":
                    restaurants = await _yelpService.GetTopRatedRestaurantsAsync(intentRequest.ZipCode);
                    break;

                case "find specific cuisine near me":
                    if (string.IsNullOrEmpty(intentRequest.CuisineType))
                    {
                        return "I heard you're looking for a specific cuisine. Could you please specify what type of food you're in the mood for?";
                    }
                    restaurants = await _yelpService.GetCuisineSpecificRestaurantsAsync(intentRequest.ZipCode, intentRequest.CuisineType);
                    break;

                default:
                    return "Really sorry, can't help you with this. I can help you find restaurants near you if you provide your zip code!";
            }

            if (restaurants.Count == 0)
            {
                return "I couldn't find any restaurants matching your criteria in that area. Please try a different location or cuisine type.";
            }

            // Generate human-readable response using OpenAI
            return await _openAIService.GenerateResponseTextAsync(restaurants, intentRequest.Intent);
        }
        catch (Exception)
        {
            return "Really sorry, can't help you with this";
        }
    }

    private byte[] CombineAudioChunks(List<byte[]> chunks)
    {
        var totalLength = chunks.Sum(chunk => chunk.Length);
        var combinedAudio = new byte[totalLength];
        var offset = 0;

        foreach (var chunk in chunks)
        {
            Buffer.BlockCopy(chunk, 0, combinedAudio, offset, chunk.Length);
            offset += chunk.Length;
        }

        return combinedAudio;
    }

    private async Task<FoodRecommendationResponse> CreateErrorResponse(string sessionId, string errorMessage)
    {
        try
        {
            var audioData = await _openAIService.ConvertTextToSpeechAsync("Really sorry, can't help you with this");
            
            return new FoodRecommendationResponse
            {
                SessionId = sessionId,
                ResponseText = "Really sorry, can't help you with this",
                AudioData = audioData,
                IsComplete = true
            };
        }
        catch
        {
            return new FoodRecommendationResponse
            {
                SessionId = sessionId,
                ResponseText = "Really sorry, can't help you with this",
                AudioData = Array.Empty<byte>(),
                IsComplete = true
            };
        }
    }

    public async Task<FoodRecommendationResponse> ProcessAudioToSpeech(byte[] audioData)
    {
        var sessionId = Guid.NewGuid().ToString();
        
        try
        {
            Console.WriteLine($"🔄 [AudioProcessor] Starting audio to speech processing for session: {sessionId}");
            Console.WriteLine($"📦 [AudioProcessor] Processing WebM audio data - Size: {audioData.Length} bytes");
            
            // Transcribe audio using OpenAI Whisper
            var transcribedText = await _openAIService.TranscribeAudioAsync(audioData);
            
            if (string.IsNullOrWhiteSpace(transcribedText))
            {
                Console.WriteLine($"❌ [AudioProcessor] Transcription returned empty text for session: {sessionId}");
                return await CreateErrorResponse(sessionId, "Could not transcribe audio");
            }

            // Analyze intent using OpenAI GPT
            var intentRequest = await _openAIService.AnalyzeIntentAsync(transcribedText, sessionId);
            
            // Process based on intent
            Console.WriteLine($"🎯 [AudioProcessor] Processing intent: {intentRequest.Intent}");
            var responseText = await ProcessIntentAsync(intentRequest);
            Console.WriteLine($"💬 [AudioProcessor] Generated response text: \"{responseText.Substring(0, Math.Min(100, responseText.Length))}{(responseText.Length > 100 ? "..." : "")}\"");
            
            // Convert response to speech
            var audioResponseData = await _openAIService.ConvertTextToSpeechAsync(responseText);

            Console.WriteLine($"✅ [AudioProcessor] Audio to speech processing finished successfully for session: {sessionId}");
            
            return new FoodRecommendationResponse
            {
                SessionId = sessionId,
                ResponseText = responseText,
                AudioData = audioResponseData,
                IsComplete = true
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [AudioProcessor] Error processing audio to speech for session {sessionId}: {ex.Message}");
            return await CreateErrorResponse(sessionId, $"Error processing audio: {ex.Message}");
        }
    }

    public IEnumerable<byte[]> CreateAudioChunks(byte[] audioData, int chunkSize = 4096)
    {
        for (int i = 0; i < audioData.Length; i += chunkSize)
        {
            var remainingBytes = Math.Min(chunkSize, audioData.Length - i);
            var chunk = new byte[remainingBytes];
            Buffer.BlockCopy(audioData, i, chunk, 0, remainingBytes);
            yield return chunk;
        }
    }
} 