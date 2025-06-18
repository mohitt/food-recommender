using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using FoodRecommender.Services;

namespace FoodRecommender.WebSockets;

public class WebSocketManager
{
    private readonly ConcurrentDictionary<string, WebSocket> _connections = new();
    private readonly ConcurrentDictionary<string, List<byte>> _audioBuffers = new();
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<WebSocketManager> _logger;

    public WebSocketManager(IServiceProvider serviceProvider, ILogger<WebSocketManager> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task HandleWebSocketAsync(HttpContext context)
    {
        var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        var sessionId = Guid.NewGuid().ToString();
        
        _connections[sessionId] = webSocket;
        _audioBuffers[sessionId] = new List<byte>();
        
        _logger.LogInformation("🔌 WebSocket connection established for session: {SessionId}", sessionId);
        
        try
        {
            await HandleWebSocketMessages(webSocket, sessionId);
        }
        catch (WebSocketException ex)
        {
            _logger.LogWarning("🔌 WebSocket connection closed: {Message}", ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error handling WebSocket connection: {Message}", ex.Message);
        }
        finally
        {
            _connections.TryRemove(sessionId, out _);
            _audioBuffers.TryRemove(sessionId, out _);
            _logger.LogInformation("🔌 WebSocket connection removed for session: {SessionId}", sessionId);
        }
    }

    private async Task HandleWebSocketMessages(WebSocket webSocket, string sessionId)
    {
        var buffer = new byte[1024 * 4]; // 4KB buffer
        
        while (webSocket.State == WebSocketState.Open)
        {
            var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            
            if (result.MessageType == WebSocketMessageType.Close)
            {
                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                break;
            }
            
            if (result.MessageType == WebSocketMessageType.Binary)
            {
                var messageBytes = new byte[result.Count];
                Array.Copy(buffer, 0, messageBytes, 0, result.Count);
                
                await ProcessBinaryMessage(messageBytes, sessionId);
            }
        }
    }

    private async Task ProcessBinaryMessage(byte[] messageBytes, string sessionId)
    {
        try
        {
            var (messageType, messagSessionId, data) = BinaryMessage.ParseMessage(messageBytes);
            
            _logger.LogInformation("📨 Received message type: {MessageType} for session: {SessionId}", 
                messageType, sessionId);
            
            switch (messageType)
            {
                case MessageType.AudioChunk:
                    await HandleAudioChunk(data, sessionId);
                    break;
                    
                case MessageType.AudioEnd:
                    await HandleAudioEnd(sessionId);
                    break;
                    
                default:
                    _logger.LogWarning("⚠️ Unknown message type: {MessageType}", messageType);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error processing binary message: {Message}", ex.Message);
            await SendErrorMessage(sessionId, $"Error processing message: {ex.Message}");
        }
    }

    private async Task HandleAudioChunk(byte[] data, string sessionId)
    {
        try
        {
            var (audioData, audioLength) = BinaryMessage.ParseAudioChunkData(data);
            
            if (!_audioBuffers.ContainsKey(sessionId))
            {
                _audioBuffers[sessionId] = new List<byte>();
            }
            
            _audioBuffers[sessionId].AddRange(audioData);
            
            _logger.LogInformation("🎵 Received audio chunk: {Length} bytes, total: {TotalLength} bytes for session: {SessionId}", 
                audioLength, _audioBuffers[sessionId].Count, sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error handling audio chunk: {Message}", ex.Message);
            await SendErrorMessage(sessionId, $"Error handling audio chunk: {ex.Message}");
        }
    }

    private async Task HandleAudioEnd(string sessionId)
    {
        try
        {
            _logger.LogInformation("🎵 Audio stream ended for session: {SessionId}", sessionId);
            
            if (!_audioBuffers.TryGetValue(sessionId, out var audioBuffer) || audioBuffer.Count == 0)
            {
                _logger.LogWarning("⚠️ No audio data received for session: {SessionId}", sessionId);
                await SendErrorMessage(sessionId, "No audio data received");
                return;
            }

            // Send processing started message
            await SendMessage(sessionId, BinaryMessage.CreateSimpleMessage(MessageType.ProcessingStarted, sessionId));
            
            // Process the audio through the pipeline
            using var scope = _serviceProvider.CreateScope();
            var audioProcessingService = scope.ServiceProvider.GetRequiredService<AudioProcessingService>();
            
            var audioBytes = audioBuffer.ToArray();
            _logger.LogInformation("🔄 Starting audio processing pipeline with {Length} bytes", audioBytes.Length);
            
            // Process audio and get response
            var response = await audioProcessingService.ProcessAudioToSpeech(audioBytes);
            
            if (response?.AudioData != null)
            {
                _logger.LogInformation("📢 Sending text response: {Text}", response.ResponseText ?? "No text response");
                
                // Send text response first
                if (!string.IsNullOrEmpty(response.ResponseText))
                {
                    var textMessage = BinaryMessage.CreateTextResponseMessage(response.ResponseText, sessionId);
                    await SendMessage(sessionId, textMessage);
                }
                
                // Send audio response start
                await SendMessage(sessionId, BinaryMessage.CreateSimpleMessage(MessageType.AudioResponseStart, sessionId));
                
                // Send audio in chunks
                await SendAudioResponse(sessionId, response.AudioData);
                
                // Send audio response end
                await SendMessage(sessionId, BinaryMessage.CreateSimpleMessage(MessageType.AudioResponseEnd, sessionId));
                
                _logger.LogInformation("✅ Audio processing pipeline completed for session: {SessionId}", sessionId);
            }
            else
            {
                await SendErrorMessage(sessionId, "Failed to process audio");
            }
            
            // Send processing complete
            await SendMessage(sessionId, BinaryMessage.CreateSimpleMessage(MessageType.ProcessingComplete, sessionId));
            
            // Clear the audio buffer
            _audioBuffers[sessionId].Clear();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error processing audio end: {Message}", ex.Message);
            await SendErrorMessage(sessionId, $"Error processing audio: {ex.Message}");
        }
    }

    private async Task SendAudioResponse(string sessionId, byte[] audioData)
    {
        const int chunkSize = 4096; // 4KB chunks
        var totalChunks = (int)Math.Ceiling((double)audioData.Length / chunkSize);
        
        _logger.LogInformation("📢 Sending audio response: {Length} bytes in {Chunks} chunks", 
            audioData.Length, totalChunks);
        
        for (int i = 0; i < totalChunks; i++)
        {
            var start = i * chunkSize;
            var length = Math.Min(chunkSize, audioData.Length - start);
            var chunk = new byte[length];
            Array.Copy(audioData, start, chunk, 0, length);
            
            var isLast = i == totalChunks - 1;
            var message = BinaryMessage.CreateAudioResponseChunkMessage(chunk, sessionId, i, isLast);
            
            await SendMessage(sessionId, message);
            
            _logger.LogDebug("📢 Sent audio chunk {ChunkIndex}/{TotalChunks} ({Length} bytes) for session: {SessionId}", 
                i + 1, totalChunks, length, sessionId);
        }
    }

    private async Task SendErrorMessage(string sessionId, string errorMessage)
    {
        try
        {
            var message = BinaryMessage.CreateErrorMessage(errorMessage, sessionId);
            await SendMessage(sessionId, message);
            _logger.LogError("❌ Sent error message to session {SessionId}: {Error}", sessionId, errorMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to send error message: {Message}", ex.Message);
        }
    }

    private async Task SendMessage(string sessionId, byte[] message)
    {
        if (_connections.TryGetValue(sessionId, out var webSocket) && 
            webSocket.State == WebSocketState.Open)
        {
            await webSocket.SendAsync(
                new ArraySegment<byte>(message), 
                WebSocketMessageType.Binary, 
                true, 
                CancellationToken.None);
        }
        else
        {
            _logger.LogWarning("⚠️ WebSocket not available for session: {SessionId}", sessionId);
        }
    }
} 