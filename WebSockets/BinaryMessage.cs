using System.Text;

namespace FoodRecommender.WebSockets;

public static class BinaryMessage
{
    public static byte[] CreateAudioChunkMessage(byte[] audioData, string sessionId)
    {
        var sessionBytes = Encoding.UTF8.GetBytes(sessionId);
        var message = new byte[1 + 4 + sessionBytes.Length + 4 + audioData.Length];
        
        var offset = 0;
        message[offset++] = (byte)MessageType.AudioChunk;
        
        // Session ID length + data
        BitConverter.GetBytes(sessionBytes.Length).CopyTo(message, offset);
        offset += 4;
        sessionBytes.CopyTo(message, offset);
        offset += sessionBytes.Length;
        
        // Audio data length + data
        BitConverter.GetBytes(audioData.Length).CopyTo(message, offset);
        offset += 4;
        audioData.CopyTo(message, offset);
        
        return message;
    }
    
    public static byte[] CreateAudioEndMessage(string sessionId)
    {
        var sessionBytes = Encoding.UTF8.GetBytes(sessionId);
        var message = new byte[1 + 4 + sessionBytes.Length];
        
        var offset = 0;
        message[offset++] = (byte)MessageType.AudioEnd;
        
        // Session ID length + data
        BitConverter.GetBytes(sessionBytes.Length).CopyTo(message, offset);
        offset += 4;
        sessionBytes.CopyTo(message, offset);
        
        return message;
    }
    
    public static byte[] CreateTextResponseMessage(string text, string sessionId)
    {
        var sessionBytes = Encoding.UTF8.GetBytes(sessionId);
        var textBytes = Encoding.UTF8.GetBytes(text);
        var message = new byte[1 + 4 + sessionBytes.Length + 4 + textBytes.Length];
        
        var offset = 0;
        message[offset++] = (byte)MessageType.TextResponse;
        
        // Session ID length + data
        BitConverter.GetBytes(sessionBytes.Length).CopyTo(message, offset);
        offset += 4;
        sessionBytes.CopyTo(message, offset);
        offset += sessionBytes.Length;
        
        // Text length + data
        BitConverter.GetBytes(textBytes.Length).CopyTo(message, offset);
        offset += 4;
        textBytes.CopyTo(message, offset);
        
        return message;
    }
    
    public static byte[] CreateAudioResponseChunkMessage(byte[] audioData, string sessionId, int chunkIndex, bool isLast)
    {
        var sessionBytes = Encoding.UTF8.GetBytes(sessionId);
        var message = new byte[1 + 4 + sessionBytes.Length + 4 + 1 + 4 + audioData.Length];
        
        var offset = 0;
        message[offset++] = (byte)MessageType.AudioResponseChunk;
        
        // Session ID length + data
        BitConverter.GetBytes(sessionBytes.Length).CopyTo(message, offset);
        offset += 4;
        sessionBytes.CopyTo(message, offset);
        offset += sessionBytes.Length;
        
        // Chunk index
        BitConverter.GetBytes(chunkIndex).CopyTo(message, offset);
        offset += 4;
        
        // Is last flag
        message[offset++] = (byte)(isLast ? 1 : 0);
        
        // Audio data length + data
        BitConverter.GetBytes(audioData.Length).CopyTo(message, offset);
        offset += 4;
        audioData.CopyTo(message, offset);
        
        return message;
    }
    
    public static byte[] CreateSimpleMessage(MessageType messageType, string sessionId)
    {
        var sessionBytes = Encoding.UTF8.GetBytes(sessionId);
        var message = new byte[1 + 4 + sessionBytes.Length];
        
        var offset = 0;
        message[offset++] = (byte)messageType;
        
        // Session ID length + data
        BitConverter.GetBytes(sessionBytes.Length).CopyTo(message, offset);
        offset += 4;
        sessionBytes.CopyTo(message, offset);
        
        return message;
    }
    
    public static byte[] CreateErrorMessage(string errorText, string sessionId)
    {
        var sessionBytes = Encoding.UTF8.GetBytes(sessionId);
        var errorBytes = Encoding.UTF8.GetBytes(errorText);
        var message = new byte[1 + 4 + sessionBytes.Length + 4 + errorBytes.Length];
        
        var offset = 0;
        message[offset++] = (byte)MessageType.Error;
        
        // Session ID length + data
        BitConverter.GetBytes(sessionBytes.Length).CopyTo(message, offset);
        offset += 4;
        sessionBytes.CopyTo(message, offset);
        offset += sessionBytes.Length;
        
        // Error text length + data
        BitConverter.GetBytes(errorBytes.Length).CopyTo(message, offset);
        offset += 4;
        errorBytes.CopyTo(message, offset);
        
        return message;
    }
    
    public static (MessageType messageType, string sessionId, byte[] data) ParseMessage(byte[] message)
    {
        if (message.Length < 1)
            throw new ArgumentException("Message too short");
            
        var messageType = (MessageType)message[0];
        var offset = 1;
        
        // Read session ID
        if (message.Length < offset + 4)
            throw new ArgumentException("Invalid message format");
            
        var sessionIdLength = BitConverter.ToInt32(message, offset);
        offset += 4;
        
        if (message.Length < offset + sessionIdLength)
            throw new ArgumentException("Invalid message format");
            
        var sessionId = Encoding.UTF8.GetString(message, offset, sessionIdLength);
        offset += sessionIdLength;
        
        // Read remaining data
        var remainingData = new byte[message.Length - offset];
        Array.Copy(message, offset, remainingData, 0, remainingData.Length);
        
        return (messageType, sessionId, remainingData);
    }
    
    public static (byte[] audioData, int dataLength) ParseAudioChunkData(byte[] data)
    {
        if (data.Length < 4)
            throw new ArgumentException("Invalid audio chunk data");
            
        var audioLength = BitConverter.ToInt32(data, 0);
        var audioData = new byte[audioLength];
        Array.Copy(data, 4, audioData, 0, audioLength);
        
        return (audioData, audioLength);
    }
} 