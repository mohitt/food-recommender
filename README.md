# 🍽️ Food Recommender - AI-Powered Restaurant Finder

A real-time voice-powered application that helps users find restaurants using AI. Speak your request, get personalized restaurant recommendations, and hear the response back as audio!

## ✨ Features

- **🎤 Voice Input**: Capture audio from the browser using Web Audio API
- **🔊 Real-time Streaming**: Chunked audio streaming via WebSocket (SignalR)
- **🤖 AI-Powered**: Uses OpenAI Whisper for speech-to-text, GPT-4 for intent analysis
- **🗣️ Voice Response**: Text-to-speech using OpenAI TTS API
- **🍕 Restaurant Search**: Integrates with Yelp API for restaurant recommendations
- **📱 Modern UI**: Responsive, beautiful user interface
- **⚡ Real-time**: Live processing and streaming of audio data

## 🏗️ Architecture

```
Frontend (Browser)
    ↓ Audio Capture & WebSocket
Backend (.NET Core)
    ↓ Speech-to-Text
OpenAI Whisper API
    ↓ Intent Analysis
OpenAI GPT-4 API
    ↓ Restaurant Search
Yelp API
    ↓ Response Generation
OpenAI GPT-4 API
    ↓ Text-to-Speech
OpenAI TTS API
    ↓ Audio Streaming
Frontend (Browser)
```

## 🚀 Getting Started

### Prerequisites

- .NET 8.0 SDK
- Modern web browser with microphone support
- OpenAI API key
- Yelp API key

### 1. Clone and Setup

```bash
# Navigate to the project directory
cd FoodRecommender

# Restore dependencies
dotnet restore
```

### 2. Configure API Keys

Edit `appsettings.json` and add your API keys:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "OpenAI": {
    "ApiKey": "sk-your-openai-api-key-here"
  },
  "Yelp": {
    "ApiKey": "your-yelp-api-key-here"
  }
}
```

#### Getting API Keys

**OpenAI API Key:**
1. Go to [OpenAI API Keys](https://platform.openai.com/api-keys)
2. Create a new API key
3. Make sure you have credits/billing set up

**Yelp API Key:**
1. Go to [Yelp Developers](https://www.yelp.com/developers)
2. Create an app
3. Get your API key from the app dashboard

### 3. Run the Application

```bash
# Start the application
dotnet run
```

The application will be available at:
- **HTTP**: `http://localhost:5000`
- **HTTPS**: `https://localhost:5001`

### 4. Using the Application

1. **Open your browser** and navigate to the application
2. **Allow microphone permissions** when prompted
3. **Click "Start Recording"** and speak your request
4. **Include your zip code** (e.g., "Find restaurants near 10001")
5. **Optionally specify cuisine** (e.g., "Find Italian restaurants near 90210")
6. **Click "Stop Recording"** when finished
7. **Listen to the AI response** with restaurant recommendations

## 🎯 Supported Intents

The application recognizes these types of requests:

### 1. General Restaurant Search
- *"Find restaurants near 10001"*
- *"Show me good places to eat in 90210"*
- *"What restaurants are near 60601?"*

### 2. Specific Cuisine Search
- *"Find Italian restaurants near 10001"*
- *"Show me Chinese food in 90210"*
- *"I want Mexican restaurants near 60601"*

### 3. Example Requests
```
✅ "Find restaurants near 10001"
✅ "Show me Italian restaurants in 90210"
✅ "I want good Chinese food near 60601"
✅ "Find pizza places near 10002"
❌ "What's the weather like?" (unsupported intent)
```

## 🛠️ Technical Details

### Backend Components

- **AudioHub**: SignalR hub for real-time WebSocket communication
- **OpenAIService**: Handles Whisper, GPT-4, and TTS integration
- **YelpService**: Manages restaurant search via Yelp API
- **AudioProcessingService**: Processes chunked audio streams

### Frontend Components

- **Audio Recording**: Uses MediaRecorder API for high-quality audio capture
- **WebSocket Communication**: SignalR client for real-time data transfer
- **Chunked Streaming**: Efficiently handles large audio files
- **Audio Playback**: Seamless playback of AI-generated responses

### Data Flow

1. **Audio Capture**: Browser captures microphone input
2. **Chunked Upload**: Audio sent in 8KB chunks via WebSocket
3. **Speech-to-Text**: OpenAI Whisper transcribes audio
4. **Intent Analysis**: GPT-4 extracts location and cuisine preferences
5. **Restaurant Search**: Yelp API finds matching restaurants
6. **Response Generation**: GPT-4 creates human-friendly response
7. **Text-to-Speech**: OpenAI TTS converts response to audio
8. **Chunked Download**: Audio response streamed back in chunks
9. **Playback**: Browser plays the AI response

## 🔧 Configuration Options

### Audio Settings

The application uses optimized audio settings:

```javascript
{
    echoCancellation: true,
    noiseSuppression: true,
    autoGainControl: true,
    sampleRate: 44100
}
```

### Chunking Configuration

- **Upload chunks**: 8KB for efficient transmission
- **Download chunks**: 4KB for smooth playback
- **Processing delay**: 50ms between chunks

## 🎨 Customization

### Styling
- Modify `wwwroot/styles.css` for custom styling
- Responsive design works on mobile and desktop
- Modern gradient backgrounds and animations

### OpenAI Models
- **Whisper**: `whisper-1` for speech-to-text
- **GPT**: `gpt-4` for intent analysis and response generation
- **TTS**: `tts-1` with `alloy` voice

### Yelp Search
- Results limited to 10 restaurants
- Sorted by rating (highest first)
- Includes name, rating, reviews, categories, and location

## 🚨 Troubleshooting

### Common Issues

**"Microphone not accessible"**
- Ensure browser has microphone permissions
- Check if another application is using the microphone
- Try refreshing the page

**"Not connected to server"**
- Check if the application is running
- Verify firewall settings
- Try refreshing the page

**"API Error"**
- Verify API keys are correctly configured
- Check API key permissions and credits
- Monitor API rate limits

**"No restaurants found"**
- Ensure zip code is valid
- Try a different location
- Check if Yelp has data for that area

### Debug Mode

For development, additional logging is available in the browser console:

```bash
# Run in development mode
dotnet run --environment Development
```

## 📝 API Costs

Approximate costs per request:
- **Whisper**: ~$0.006 per minute of audio
- **GPT-4**: ~$0.01-0.03 per request
- **TTS**: ~$0.015 per 1000 characters
- **Yelp**: Free tier available

## 🔒 Security Notes

- API keys should never be exposed in client-side code
- Consider implementing rate limiting for production
- Use HTTPS in production environments
- Validate all user inputs

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Test thoroughly
5. Submit a pull request

## 📄 License

This project is open source and available under the MIT License.

## 🆘 Support

For questions or issues:
1. Check the troubleshooting section
2. Review the console for error messages
3. Ensure all API keys are properly configured
4. Verify internet connectivity

---

**Enjoy discovering amazing restaurants with AI! 🍽️✨** 