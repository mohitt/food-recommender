class FoodRecommenderApp {
    constructor() {
        this.connection = null;
        this.mediaRecorder = null;
        this.audioChunks = [];
        this.sessionId = null;
        this.isRecording = false;
        this.audioContext = null;
        this.responseAudioChunks = [];
        
        this.initializeElements();
        this.initializeSignalR();
        this.bindEvents();
    }

    initializeElements() {
        this.startBtn = document.getElementById('startBtn');
        this.stopBtn = document.getElementById('stopBtn');
        this.status = document.getElementById('status');
        this.connectionStatus = document.getElementById('connectionStatus');
        this.transcription = document.getElementById('transcription');
        this.recommendations = document.getElementById('recommendations');
        this.responseAudio = document.getElementById('responseAudio');
        this.audioStatus = document.getElementById('audioStatus');
    }

    async initializeSignalR() {
        try {
            this.connection = new signalR.HubConnectionBuilder()
                .withUrl("/audiohub")
                .withAutomaticReconnect()
                .build();

            this.setupSignalREventHandlers();
            
            await this.connection.start();
            this.updateConnectionStatus(true, "Connected to server");
            console.log("SignalR Connected.");
        } catch (err) {
            console.error("SignalR Connection Error: ", err);
            this.updateConnectionStatus(false, "Connection failed");
        }
    }

    setupSignalREventHandlers() {
        this.connection.on("Connected", (connectionId) => {
            this.sessionId = connectionId;
            console.log("Session ID:", connectionId);
        });

        this.connection.on("ProcessingStarted", (sessionId) => {
            console.log(`🔄 [Client] Server started processing for session: ${sessionId}`);
            this.updateStatus("processing", "Processing your request...");
            this.audioStatus.textContent = "Processing audio response...";
        });

        this.connection.on("TextResponse", (responseText) => {
            console.log(`📝 [Client] Received text response:`, responseText);
            this.transcription.textContent = "Processing...";
            this.recommendations.textContent = responseText;
        });

        this.connection.on("AudioChunk", (audioChunkData) => {
            this.handleAudioChunk(JSON.parse(audioChunkData));
        });

        // New binary audio chunk handler
        this.connection.on("AudioChunkBinary", (chunkData, chunkIndex, isLast) => {
            console.log(`📥 [Client] Received binary audio chunk ${chunkIndex}, size: ${chunkData.length} bytes, isLast: ${isLast}`);
            this.handleAudioChunkBinary(chunkData, chunkIndex, isLast);
        });

        this.connection.on("AudioMetadata", (metadata) => {
            console.log(`📊 [Client] Audio metadata received:`, metadata);
            this.audioMetadata = metadata;
            this.responseAudioChunks = []; // Reset for new binary audio
        });

        this.connection.on("ProcessingComplete", (sessionId) => {
            this.updateStatus("ready", "Ready to listen");
            this.audioStatus.textContent = "Audio response ready - click play to listen";
        });

        this.connection.on("Error", (error) => {
            console.error("Server Error:", error);
            this.updateStatus("ready", "Error occurred - ready to try again");
            this.audioStatus.textContent = "Error processing request";
        });

        this.connection.onreconnecting((error) => {
            this.updateConnectionStatus(false, "Reconnecting...");
        });

        this.connection.onreconnected((connectionId) => {
            this.updateConnectionStatus(true, "Reconnected");
            this.sessionId = connectionId;
        });

        this.connection.onclose((error) => {
            this.updateConnectionStatus(false, "Disconnected");
        });
    }

    bindEvents() {
        this.startBtn.addEventListener('click', () => this.startRecording());
        this.stopBtn.addEventListener('click', () => this.stopRecording());
    }

    async startRecording() {
        try {
            if (!this.connection || this.connection.state !== signalR.HubConnectionState.Connected) {
                alert("Not connected to server. Please refresh the page.");
                return;
            }

            const stream = await navigator.mediaDevices.getUserMedia({ 
                audio: {
                    echoCancellation: true,
                    noiseSuppression: true,
                    autoGainControl: true,
                    sampleRate: 44100
                } 
            });

            this.audioChunks = [];
            this.responseAudioChunks = [];
            
            // Clear previous responses
            this.transcription.textContent = "Listening...";
            this.recommendations.textContent = "Listening for your request...";
            this.audioStatus.textContent = "Recording audio...";

            this.mediaRecorder = new MediaRecorder(stream, {
                mimeType: 'audio/webm;codecs=opus'
            });

            this.mediaRecorder.ondataavailable = (event) => {
                if (event.data.size > 0) {
                    this.audioChunks.push(event.data);
                }
            };

            this.mediaRecorder.onstop = () => {
                this.processRecording();
            };

            this.mediaRecorder.start(100); // Collect data every 100ms
            this.isRecording = true;
            
            this.startBtn.disabled = true;
            this.stopBtn.disabled = false;
            
            this.updateStatus("recording", "Recording... Click stop when finished");

        } catch (error) {
            console.error('Error starting recording:', error);
            alert('Error accessing microphone. Please ensure you have granted microphone permissions.');
        }
    }

    stopRecording() {
        if (this.mediaRecorder && this.mediaRecorder.state !== 'inactive') {
            this.mediaRecorder.stop();
            
            // Stop all tracks to release the microphone
            this.mediaRecorder.stream.getTracks().forEach(track => track.stop());
        }
        
        this.isRecording = false;
        this.startBtn.disabled = false;
        this.stopBtn.disabled = true;
        
        this.updateStatus("processing", "Processing your audio...");
    }

    async processRecording() {
        if (this.audioChunks.length === 0) {
            console.log("⚠️ [Client] No audio chunks recorded");
            this.updateStatus("ready", "No audio recorded");
            return;
        }

        try {
            console.log(`🎙️ [Client] Processing ${this.audioChunks.length} audio chunks`);
            
            // Combine all audio chunks into a single blob
            const audioBlob = new Blob(this.audioChunks, { type: 'audio/webm' });
            
            console.log(`📦 [Client] Combined audio blob size: ${audioBlob.size} bytes`);
            
            // Convert to array buffer
            const arrayBuffer = await audioBlob.arrayBuffer();
            const audioData = new Uint8Array(arrayBuffer);

            console.log(`🔢 [Client] Audio data array size: ${audioData.length} bytes`);

            // Send audio data in chunks
            await this.sendAudioInChunks(audioData);

        } catch (error) {
            console.error('❌ [Client] Error processing recording:', error);
            this.updateStatus("ready", "Error processing audio");
        }
    }

    async sendAudioInChunks(audioData) {
        const chunkSize = 8192; // 8KB chunks
        const totalChunks = Math.ceil(audioData.length / chunkSize);

        console.log(`📡 [Client] Sending audio in ${totalChunks} chunks of ${chunkSize} bytes each`);

        for (let i = 0; i < totalChunks; i++) {
            const start = i * chunkSize;
            const end = Math.min(start + chunkSize, audioData.length);
            const chunk = audioData.slice(start, end);
            
            try {
                console.log(`📤 [Client] Sending chunk ${i + 1}/${totalChunks} (${chunk.length} bytes)${i === totalChunks - 1 ? ' - FINAL CHUNK' : ''}`);
                
                // Send binary data directly - much more efficient!
                await this.connection.invoke("SendAudioBinary", chunk, i === totalChunks - 1, this.sessionId);
                
                // Small delay between chunks to prevent overwhelming the server
                if (i < totalChunks - 1) {
                    await this.delay(50);
                }
            } catch (error) {
                console.error(`❌ [Client] Error sending audio chunk ${i + 1}:`, error);
                this.updateStatus("ready", "Error sending audio");
                return;
            }
        }
        
        console.log(`✅ [Client] All ${totalChunks} audio chunks sent successfully`);
    }

    // Legacy method for backward compatibility
    async sendAudioInChunksJSON(audioData) {
        const chunkSize = 8192; // 8KB chunks
        const totalChunks = Math.ceil(audioData.length / chunkSize);

        for (let i = 0; i < totalChunks; i++) {
            const start = i * chunkSize;
            const end = Math.min(start + chunkSize, audioData.length);
            const chunk = audioData.slice(start, end);
            
            const audioMessage = {
                Type: "audio",
                AudioData: Array.from(chunk),
                IsLast: i === totalChunks - 1,
                SessionId: this.sessionId
            };

            try {
                await this.connection.invoke("SendAudioChunk", JSON.stringify(audioMessage));
                
                // Small delay between chunks to prevent overwhelming the server
                if (i < totalChunks - 1) {
                    await this.delay(50);
                }
            } catch (error) {
                console.error('Error sending audio chunk:', error);
                this.updateStatus("ready", "Error sending audio");
                return;
            }
        }
    }

    handleAudioChunk(audioChunk) {
        // Store the audio chunk
        this.responseAudioChunks.push({
            data: new Uint8Array(audioChunk.AudioData),
            index: audioChunk.ChunkIndex,
            isLast: audioChunk.IsLast
        });

        // Update progress
        const progress = ((audioChunk.ChunkIndex + 1) / audioChunk.TotalChunks * 100).toFixed(0);
        this.audioStatus.textContent = `Receiving audio response: ${progress}%`;

        // If this is the last chunk, combine and play the audio
        if (audioChunk.IsLast) {
            this.combineAndPlayAudio();
        }
    }

    handleAudioChunkBinary(chunkData, chunkIndex, isLast) {
        // Store the binary audio chunk directly - no conversion needed!
        this.responseAudioChunks.push({
            data: new Uint8Array(chunkData),
            index: chunkIndex,
            isLast: isLast
        });

        // Update progress
        if (this.audioMetadata) {
            const totalChunks = Math.ceil(this.audioMetadata.TotalSize / this.audioMetadata.ChunkSize);
            const progress = ((chunkIndex + 1) / totalChunks * 100).toFixed(0);
            this.audioStatus.textContent = `Receiving binary audio: ${progress}%`;
        }

        // If this is the last chunk, combine and play the audio
        if (isLast) {
            this.combineAndPlayAudioBinary();
        }
    }

    async combineAndPlayAudio() {
        try {
            // Sort chunks by index to ensure proper order
            this.responseAudioChunks.sort((a, b) => a.index - b.index);

            // Calculate total length
            const totalLength = this.responseAudioChunks.reduce((total, chunk) => total + chunk.data.length, 0);
            
            // Combine all chunks
            const combinedAudio = new Uint8Array(totalLength);
            let offset = 0;
            
            for (const chunk of this.responseAudioChunks) {
                combinedAudio.set(chunk.data, offset);
                offset += chunk.data.length;
            }

            // Create blob and URL for audio playback
            const audioBlob = new Blob([combinedAudio], { type: 'audio/mpeg' });
            const audioUrl = URL.createObjectURL(audioBlob);
            
            // Set up the audio element
            this.responseAudio.src = audioUrl;
            this.responseAudio.load();
            
            // Auto-play the response (if browser allows)
            try {
                await this.responseAudio.play();
                this.audioStatus.textContent = "Playing audio response";
            } catch (playError) {
                console.log("Auto-play prevented by browser:", playError);
                this.audioStatus.textContent = "Audio response ready - click play to listen";
            }

            // Clean up old URL when audio ends
            this.responseAudio.addEventListener('ended', () => {
                URL.revokeObjectURL(audioUrl);
                this.audioStatus.textContent = "Audio response finished";
            }, { once: true });

        } catch (error) {
            console.error('Error combining audio chunks:', error);
            this.audioStatus.textContent = "Error playing audio response";
        }
    }

    async combineAndPlayAudioBinary() {
        try {
            console.log(`🔧 [Client] Combining ${this.responseAudioChunks.length} binary audio chunks`);
            
            // Sort chunks by index to ensure proper order
            this.responseAudioChunks.sort((a, b) => a.index - b.index);

            // Calculate total length
            const totalLength = this.responseAudioChunks.reduce((total, chunk) => total + chunk.data.length, 0);
            
            console.log(`📏 [Client] Total combined audio length: ${totalLength} bytes`);
            
            // Combine all binary chunks directly - much more efficient!
            const combinedAudio = new Uint8Array(totalLength);
            let offset = 0;
            
            for (const chunk of this.responseAudioChunks) {
                combinedAudio.set(chunk.data, offset);
                offset += chunk.data.length;
            }

            console.log(`🎵 [Client] Audio chunks combined successfully`);

            // Create blob and URL for audio playback
            const audioBlob = new Blob([combinedAudio], { type: 'audio/mpeg' });
            const audioUrl = URL.createObjectURL(audioBlob);
            
            console.log(`🔗 [Client] Created audio blob URL for playback`);
            
            // Set up the audio element
            this.responseAudio.src = audioUrl;
            this.responseAudio.load();
            
            // Auto-play the response (if browser allows)
            try {
                await this.responseAudio.play();
                console.log(`▶️ [Client] Started playing binary audio response`);
                this.audioStatus.textContent = "Playing binary audio response";
            } catch (playError) {
                console.log("⚠️ [Client] Auto-play prevented by browser:", playError);
                this.audioStatus.textContent = "Binary audio response ready - click play to listen";
            }

            // Clean up old URL when audio ends
            this.responseAudio.addEventListener('ended', () => {
                console.log(`🏁 [Client] Audio playback finished`);
                URL.revokeObjectURL(audioUrl);
                this.audioStatus.textContent = "Binary audio response finished";
            }, { once: true });

        } catch (error) {
            console.error('❌ [Client] Error combining binary audio chunks:', error);
            this.audioStatus.textContent = "Error playing binary audio response";
        }
    }

    updateStatus(type, message) {
        this.status.className = `status-indicator ${type}`;
        this.status.querySelector('.status-text').textContent = message;
    }

    updateConnectionStatus(connected, message) {
        this.connectionStatus.className = `connection-status ${connected ? 'connected' : 'disconnected'}`;
        this.connectionStatus.querySelector('.connection-text').textContent = message;
    }

    delay(ms) {
        return new Promise(resolve => setTimeout(resolve, ms));
    }
}

// Initialize the application when the page loads
document.addEventListener('DOMContentLoaded', () => {
    new FoodRecommenderApp();
});

// Handle page visibility changes
document.addEventListener('visibilitychange', () => {
    if (document.hidden) {
        // Page is hidden, could pause any ongoing operations
        console.log('Page hidden');
    } else {
        // Page is visible again
        console.log('Page visible');
    }
});

// Handle beforeunload to clean up resources
window.addEventListener('beforeunload', () => {
    if (window.foodRecommenderApp) {
        // Clean up any resources
        console.log('Page unloading, cleaning up...');
    }
}); 