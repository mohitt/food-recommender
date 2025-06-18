class FoodRecommenderApp {
    constructor() {
        this.websocket = null;
        this.mediaRecorder = null;
        this.audioChunks = [];
        this.sessionId = this.generateSessionId();
        this.isRecording = false;
        this.audioContext = null;
        this.responseAudioChunks = [];
        this.isProcessing = false;
        
        this.initializeElements();
        this.initializeWebSocket();
        this.bindEvents();
    }

    generateSessionId() {
        return 'session_' + Math.random().toString(36).substr(2, 9) + '_' + Date.now();
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

    async initializeWebSocket() {
        try {
            // Create WebSocket connection
            const protocol = window.location.protocol === 'https:' ? 'wss:' : 'ws:';
            const wsUrl = `${protocol}//${window.location.host}/ws`;
            
            console.log("🔌 Connecting to WebSocket:", wsUrl);
            
            this.websocket = new WebSocket(wsUrl);
            this.websocket.binaryType = 'arraybuffer';

            this.websocket.onopen = () => {
                console.log("🔌 WebSocket connection established");
                this.updateConnectionStatus(true, "Connected to server");
            };

            this.websocket.onclose = (event) => {
                console.log("🔌 WebSocket connection closed:", event.code, event.reason);
                this.updateConnectionStatus(false, "Connection lost");
                
                // Try to reconnect after a delay
                setTimeout(() => {
                    if (this.websocket.readyState === WebSocket.CLOSED) {
                        console.log("🔄 Attempting to reconnect...");
                        this.initializeWebSocket();
                    }
                }, 3000);
            };

            this.websocket.onerror = (error) => {
                console.error("❌ WebSocket error:", error);
                this.updateConnectionStatus(false, "Connection error");
            };

            this.websocket.onmessage = (event) => {
                if (event.data instanceof ArrayBuffer) {
                    this.handleBinaryMessage(new Uint8Array(event.data));
                }
            };

        } catch (error) {
            console.error("❌ WebSocket connection failed:", error);
            this.updateConnectionStatus(false, "Failed to connect");
        }
    }

    handleBinaryMessage(data) {
        try {
            const { messageType, sessionId, payload } = this.parseBinaryMessage(data);
            
            console.log(`📨 [Client] Received message type: 0x${messageType.toString(16).padStart(2, '0')} for session: ${sessionId}`);

            switch (messageType) {
                case 0x10: // ProcessingStarted
                    console.log("🔄 [Client] Processing started on server");
                    this.updateStatus("processing", "Processing your request...");
                    this.audioStatus.textContent = "Processing audio response...";
                    this.isProcessing = true;
                    break;

                case 0x11: // TextResponse
                    const textLength = new DataView(payload.buffer, payload.byteOffset, 4).getUint32(0, true);
                    const textResponse = new TextDecoder().decode(payload.slice(4, 4 + textLength));
                    console.log("📝 [Client] Received text response:", textResponse);
                    this.transcription.textContent = "Processing complete";
                    this.recommendations.textContent = textResponse;
                    break;

                case 0x12: // AudioResponseStart
                    console.log("🎵 [Client] Audio response starting...");
                    this.responseAudioChunks = [];
                    this.audioStatus.textContent = "Receiving audio response...";
                    break;

                case 0x13: // AudioResponseChunk
                    const { chunkIndex, isLast, audioData } = this.parseAudioChunk(payload);
                    console.log(`🎵 [Client] Received audio chunk ${chunkIndex} (${audioData.length} bytes), isLast: ${isLast}`);
                    this.handleAudioChunk(audioData, chunkIndex, isLast);
                    break;

                case 0x14: // AudioResponseEnd
                    console.log("🎵 [Client] Audio response completed");
                    this.combineAndPlayAudio();
                    break;

                case 0x15: // ProcessingComplete
                    console.log("✅ [Client] Processing completed on server");
                    this.updateStatus("ready", "Ready to listen");
                    this.isProcessing = false;
                    break;

                case 0xFF: // Error
                    const errorLength = new DataView(payload.buffer, payload.byteOffset, 4).getUint32(0, true);
                    const errorMessage = new TextDecoder().decode(payload.slice(4, 4 + errorLength));
                    console.error("❌ [Client] Server error:", errorMessage);
                    this.updateStatus("ready", "Error occurred - ready to try again");
                    this.audioStatus.textContent = "Error processing request";
                    this.isProcessing = false;
                    break;

                default:
                    console.warn("⚠️ [Client] Unknown message type:", messageType);
                    break;
            }
        } catch (error) {
            console.error("❌ [Client] Error handling binary message:", error);
        }
    }

    parseBinaryMessage(data) {
        const view = new DataView(data.buffer, data.byteOffset, data.byteLength);
        let offset = 0;

        // Read message type
        const messageType = view.getUint8(offset);
        offset += 1;

        // Read session ID length and data
        const sessionIdLength = view.getUint32(offset, true);
        offset += 4;
        const sessionIdBytes = data.slice(offset, offset + sessionIdLength);
        const sessionId = new TextDecoder().decode(sessionIdBytes);
        offset += sessionIdLength;

        // Read remaining payload
        const payload = data.slice(offset);

        return { messageType, sessionId, payload };
    }

    parseAudioChunk(payload) {
        const view = new DataView(payload.buffer, payload.byteOffset, payload.byteLength);
        let offset = 0;

        // Read chunk index
        const chunkIndex = view.getUint32(offset, true);
        offset += 4;

        // Read is last flag
        const isLast = view.getUint8(offset) === 1;
        offset += 1;

        // Read audio data length
        const audioLength = view.getUint32(offset, true);
        offset += 4;

        // Read audio data
        const audioData = payload.slice(offset, offset + audioLength);

        return { chunkIndex, isLast, audioData };
    }

    createBinaryMessage(messageType, sessionId, additionalData = null) {
        const sessionBytes = new TextEncoder().encode(sessionId);
        const baseLength = 1 + 4 + sessionBytes.length;
        const totalLength = baseLength + (additionalData ? additionalData.length : 0);
        
        const message = new ArrayBuffer(totalLength);
        const view = new DataView(message);
        const uint8View = new Uint8Array(message);
        
        let offset = 0;
        
        // Write message type
        view.setUint8(offset, messageType);
        offset += 1;
        
        // Write session ID length and data
        view.setUint32(offset, sessionBytes.length, true);
        offset += 4;
        uint8View.set(sessionBytes, offset);
        offset += sessionBytes.length;
        
        // Write additional data if present
        if (additionalData) {
            uint8View.set(new Uint8Array(additionalData), offset);
        }
        
        return message;
    }

    createAudioChunkMessage(audioData) {
        const sessionBytes = new TextEncoder().encode(this.sessionId);
        const totalLength = 1 + 4 + sessionBytes.length + 4 + audioData.length;
        
        const message = new ArrayBuffer(totalLength);
        const view = new DataView(message);
        const uint8View = new Uint8Array(message);
        
        let offset = 0;
        
        // Write message type (AudioChunk = 0x01)
        view.setUint8(offset, 0x01);
        offset += 1;
        
        // Write session ID length and data
        view.setUint32(offset, sessionBytes.length, true);
        offset += 4;
        uint8View.set(sessionBytes, offset);
        offset += sessionBytes.length;
        
        // Write audio data length
        view.setUint32(offset, audioData.length, true);
        offset += 4;
        
        // Write audio data
        uint8View.set(audioData, offset);
        
        return message;
    }

    bindEvents() {
        this.startBtn.addEventListener('click', () => this.startRecording());
        this.stopBtn.addEventListener('click', () => this.stopRecording());
    }

    async startRecording() {
        try {
            if (!this.websocket || this.websocket.readyState !== WebSocket.OPEN) {
                alert("Not connected to server. Please refresh the page.");
                return;
            }

            if (this.isProcessing) {
                alert("Please wait for the current request to complete.");
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
            console.error('❌ [Client] Error starting recording:', error);
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
                
                // Create and send binary audio chunk message
                const message = this.createAudioChunkMessage(chunk);
                this.websocket.send(message);
                
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
        
        // Send audio end message
        const endMessage = this.createBinaryMessage(0x02, this.sessionId); // AudioEnd = 0x02
        this.websocket.send(endMessage);
        console.log(`📤 [Client] Sent audio end message`);
    }

    handleAudioChunk(audioData, chunkIndex, isLast) {
        // Store the binary audio chunk directly
        this.responseAudioChunks.push({
            data: new Uint8Array(audioData),
            index: chunkIndex,
            isLast: isLast
        });

        // Update progress
        const progress = this.responseAudioChunks.length;
        this.audioStatus.textContent = `Receiving audio chunk ${progress}...`;
    }

    async combineAndPlayAudio() {
        try {
            console.log(`🔧 [Client] Combining ${this.responseAudioChunks.length} audio chunks`);
            
            // Sort chunks by index to ensure proper order
            this.responseAudioChunks.sort((a, b) => a.index - b.index);

            // Calculate total length
            const totalLength = this.responseAudioChunks.reduce((total, chunk) => total + chunk.data.length, 0);
            
            console.log(`📏 [Client] Total combined audio length: ${totalLength} bytes`);
            
            // Combine all chunks
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
                console.log(`▶️ [Client] Started playing audio response`);
                this.audioStatus.textContent = "Playing audio response";
            } catch (playError) {
                console.log("⚠️ [Client] Auto-play prevented by browser:", playError);
                this.audioStatus.textContent = "Audio response ready - click play to listen";
            }

            // Clean up old URL when audio ends
            this.responseAudio.addEventListener('ended', () => {
                console.log(`🏁 [Client] Audio playback finished`);
                URL.revokeObjectURL(audioUrl);
                this.audioStatus.textContent = "Audio response finished";
            }, { once: true });

        } catch (error) {
            console.error('❌ [Client] Error combining audio chunks:', error);
            this.audioStatus.textContent = "Error playing audio response";
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
        console.log('🔍 [Client] Page hidden');
    } else {
        console.log('🔍 [Client] Page visible');
    }
});

// Handle beforeunload to clean up resources
window.addEventListener('beforeunload', () => {
    console.log('🧹 [Client] Page unloading, cleaning up...');
}); 