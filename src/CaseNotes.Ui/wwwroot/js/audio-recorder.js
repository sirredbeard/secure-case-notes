// Audio Recording Module - HIPAA Compliant (In-Memory Only)
let mediaRecorder;
let audioChunks = [];

window.startRecording = async function() {
    try {
        // Request microphone permission
        const stream = await navigator.mediaDevices.getUserMedia({ 
            audio: {
                channelCount: 1, // Mono
                sampleRate: 16000, // 16kHz for Whisper
                echoCancellation: true,
                noiseSuppression: true
            } 
        });
        
        // Create MediaRecorder
        mediaRecorder = new MediaRecorder(stream, {
            mimeType: 'audio/webm;codecs=opus'
        });
        
        audioChunks = [];
        
        // Collect audio data
        mediaRecorder.ondataavailable = (event) => {
            if (event.data.size > 0) {
                audioChunks.push(event.data);
            }
        };
        
        // Start recording
        mediaRecorder.start(100); // Collect data every 100ms
        
        console.log('Recording started');
    } catch (error) {
        console.error('Error starting recording:', error);
        alert('Failed to access microphone. Please check permissions.');
        throw error;
    }
};

window.stopRecording = async function() {
    return new Promise((resolve, reject) => {
        if (!mediaRecorder || mediaRecorder.state === 'inactive') {
            reject(new Error('No active recording'));
            return;
        }
        
        mediaRecorder.onstop = async () => {
            try {
                // Create audio blob from chunks
                const audioBlob = new Blob(audioChunks, { type: 'audio/webm' });
                
                // Stop all audio tracks
                mediaRecorder.stream.getTracks().forEach(track => track.stop());
                
                // Convert blob to array buffer, then to byte array
                const arrayBuffer = await audioBlob.arrayBuffer();
                const byteArray = new Uint8Array(arrayBuffer);
                
                console.log(`Recording stopped. Audio size: ${byteArray.length} bytes`);
                
                // HIPAA: Audio is only in memory, never written to disk
                // Clear the chunks array
                audioChunks = [];
                
                resolve(Array.from(byteArray));
            } catch (error) {
                console.error('Error processing recording:', error);
                reject(error);
            }
        };
        
        // Stop the recorder
        mediaRecorder.stop();
    });
};

// Utility function to convert WebM to WAV if needed
// This is a simplified version - production would use a library like lamejs or similar
window.convertToWav = async function(webmBlob) {
    // For now, we'll return the WebM blob as-is
    // In production, you'd use the Web Audio API or a library to convert
    return webmBlob;
};
