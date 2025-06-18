namespace FoodRecommender.WebSockets;

public enum MessageType : byte
{
    // Client to Server
    AudioChunk = 0x01,
    AudioEnd = 0x02,
    
    // Server to Client
    ProcessingStarted = 0x10,
    TextResponse = 0x11,
    AudioResponseStart = 0x12,
    AudioResponseChunk = 0x13,
    AudioResponseEnd = 0x14,
    ProcessingComplete = 0x15,
    Error = 0xFF
} 