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
            
            // Add audio chunk to processing service
            _audioProcessingService.AddAudioChunk(sessionId, audioMessage.AudioData);

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
                        AudioData = chunk,
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

    public async Task StartListening(string sessionId)
    {
        await Clients.Caller.SendAsync("ListeningStarted", sessionId);
    }

    public async Task StopListening(string sessionId)
    {
        await Clients.Caller.SendAsync("ListeningStopped", sessionId);
    }
} 