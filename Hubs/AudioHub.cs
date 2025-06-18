using Microsoft.AspNetCore.SignalR;
using FoodRecommender.Services;
using FoodRecommender.Models;
using System.Text.Json;

namespace FoodRecommender.Hubs;

public class AudioHub : Hub
{
    private readonly AudioProcessingService _audioProcessingService;

    public AudioHub(AudioProcessingService audioProcessingService)
    {
        _audioProcessingService = audioProcessingService;
    }

    public override async Task OnConnectedAsync()
    {
        await Clients.Caller.SendAsync("Connected", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await base.OnDisconnectedAsync(exception);
    }

    public async Task SendAudioChunk(string audioData)
    {
        try
        {
            var audioMessage = JsonSerializer.Deserialize<AudioMessage>(audioData);
            if (audioMessage == null)
            {
                await Clients.Caller.SendAsync("Error", "Invalid audio message format");
                return;
            }

            var sessionId = string.IsNullOrEmpty(audioMessage.SessionId) ? Context.ConnectionId : audioMessage.SessionId;
            
            // Convert int array to byte array and add audio chunk to processing service
            _audioProcessingService.AddAudioChunk(sessionId, audioMessage.GetAudioBytes());

            // If this is the last chunk, process the complete audio
            if (audioMessage.IsLast)
            {
                await Clients.Caller.SendAsync("ProcessingStarted", sessionId);
                
                var response = await _audioProcessingService.ProcessCompleteAudioAsync(sessionId);
                
                // Stream the audio response back in chunks
                await StreamAudioResponse(response);
            }
        }
        catch (Exception ex)
        {
            await Clients.Caller.SendAsync("Error", $"Error processing audio: {ex.Message}");
        }
    }

    // New binary method for efficient audio transfer
    public async Task SendAudioBinary(byte[] audioData, bool isLast, string sessionId)
    {
        try
        {
            var actualSessionId = string.IsNullOrEmpty(sessionId) ? Context.ConnectionId : sessionId;
            
            Console.WriteLine($"🔌 [AudioHub] Received binary audio chunk - Session: {actualSessionId}, Size: {audioData.Length} bytes, IsLast: {isLast}");
            
            // Direct binary processing - no JSON conversion needed!
            _audioProcessingService.AddAudioChunk(actualSessionId, audioData);

            // If this is the last chunk, process the complete audio
            if (isLast)
            {
                Console.WriteLine($"🎬 [AudioHub] Received final chunk, starting complete audio processing for session: {actualSessionId}");
                await Clients.Caller.SendAsync("ProcessingStarted", actualSessionId);
                
                var response = await _audioProcessingService.ProcessCompleteAudioAsync(actualSessionId);
                
                Console.WriteLine($"📤 [AudioHub] Starting binary audio response streaming for session: {actualSessionId}");
                // Stream the audio response back as binary
                await StreamAudioResponseBinary(response);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [AudioHub] Error in SendAudioBinary for session {sessionId}: {ex.Message}");
            await Clients.Caller.SendAsync("Error", $"Error processing audio: {ex.Message}");
        }
    }

    private async Task StreamAudioResponse(FoodRecommendationResponse response)
    {
        try
        {
            // Send the text response first
            await Clients.Caller.SendAsync("TextResponse", response.ResponseText);

            if (response.AudioData.Length > 0)
            {
                // Stream audio data in chunks
                var chunks = _audioProcessingService.CreateAudioChunks(response.AudioData);
                var chunkIndex = 0;
                var totalChunks = (int)Math.Ceiling((double)response.AudioData.Length / 4096);

                foreach (var chunk in chunks)
                {
                    var audioChunk = new
                    {
                        SessionId = response.SessionId,
                        AudioData = chunk.Select(b => (int)b).ToArray(),
                        ChunkIndex = chunkIndex,
                        TotalChunks = totalChunks,
                        IsLast = chunkIndex == totalChunks - 1
                    };

                    await Clients.Caller.SendAsync("AudioChunk", JsonSerializer.Serialize(audioChunk));
                    chunkIndex++;

                    // Small delay to prevent overwhelming the client
                    await Task.Delay(50);
                }
            }

            await Clients.Caller.SendAsync("ProcessingComplete", response.SessionId);
        }
        catch (Exception ex)
        {
            await Clients.Caller.SendAsync("Error", $"Error streaming response: {ex.Message}");
        }
    }

    private async Task StreamAudioResponseBinary(FoodRecommendationResponse response)
    {
        try
        {
            // Send the text response first
            await Clients.Caller.SendAsync("TextResponse", response.ResponseText);

            if (response.AudioData.Length > 0)
            {
                // Send metadata about the incoming binary audio
                await Clients.Caller.SendAsync("AudioMetadata", new
                {
                    SessionId = response.SessionId,
                    TotalSize = response.AudioData.Length,
                    ChunkSize = 4096
                });

                // Stream binary audio data in chunks
                var chunks = _audioProcessingService.CreateAudioChunks(response.AudioData);
                var chunkIndex = 0;

                foreach (var chunk in chunks)
                {
                    // Send each chunk as pure binary data
                    await Clients.Caller.SendAsync("AudioChunkBinary", chunk, chunkIndex, chunkIndex == response.AudioData.Length / 4096);
                    chunkIndex++;

                    // Small delay to prevent overwhelming the client
                    await Task.Delay(50);
                }
            }

            await Clients.Caller.SendAsync("ProcessingComplete", response.SessionId);
        }
        catch (Exception ex)
        {
            await Clients.Caller.SendAsync("Error", $"Error streaming response: {ex.Message}");
        }
    }

    public async Task StartListening(string sessionId)
    {
        await Clients.Caller.SendAsync("ListeningStarted", sessionId);
    }

    public async Task StopListening(string sessionId)
    {
        await Clients.Caller.SendAsync("ListeningStopped", sessionId);
    }
} 