mergeInto(LibraryManager.library, {

  // WebGL Microphone API for Unity
  WebGLMicrophone_Init: function(gameObjectNamePtr, callbackMethodNamePtr) {
    var gameObjectName = UTF8ToString(gameObjectNamePtr);
    var callbackMethodName = UTF8ToString(callbackMethodNamePtr);

    if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
      console.warn("WebGLMicrophone: getUserMedia not supported");
      SendMessage(gameObjectName, callbackMethodName, '0');
      return false;
    }

    // Request microphone permission and initialize
    navigator.mediaDevices.getUserMedia({ audio: true })
      .then(function(stream) {
        window.webGLMicrophoneStream = stream;
        window.webGLMicrophoneContext = new (window.AudioContext || window.webkitAudioContext)();
        window.webGLMicrophoneSource = window.webGLMicrophoneContext.createMediaStreamSource(stream);
        window.webGLMicrophoneProcessor = window.webGLMicrophoneContext.createScriptProcessor(4096, 1, 1);

        window.webGLMicrophoneProcessor.onaudioprocess = function(event) {
          var inputBuffer = event.inputBuffer;
          var inputData = inputBuffer.getChannelData(0);

          // Convert Float32Array to base64 properly
          var floatArray = new Float32Array(inputData);
          var uint8Array = new Uint8Array(floatArray.buffer);
          var binaryString = '';
          for (var i = 0; i < uint8Array.length; i++) {
            binaryString += String.fromCharCode(uint8Array[i]);
          }
          var base64Data = btoa(binaryString);

          // Send audio data to Unity via SendMessage
          SendMessage(gameObjectName, 'OnWebGLAudioData', base64Data);
        };

        window.webGLMicrophoneSource.connect(window.webGLMicrophoneProcessor);
        window.webGLMicrophoneProcessor.connect(window.webGLMicrophoneContext.destination);

        // Confirm initialization
        SendMessage(gameObjectName, callbackMethodName, '1');
        console.log("WebGLMicrophone: Initialized successfully");
      })
      .catch(function(error) {
        console.error("WebGLMicrophone: Failed to get microphone access:", error);
        SendMessage(gameObjectName, callbackMethodName, '0');
      });

    return true;
  },

  WebGLMicrophone_StartRecording: function() {
    if (window.webGLMicrophoneProcessor && window.webGLMicrophoneContext) {
      // Ensure AudioContext is running (required by modern browsers)
      if (window.webGLMicrophoneContext.state === 'suspended') {
        window.webGLMicrophoneContext.resume().then(function() {
          console.log("WebGLMicrophone: AudioContext resumed");
        }).catch(function(error) {
          console.error("WebGLMicrophone: Failed to resume AudioContext:", error);
        });
      }
      return true;
    }
    return false;
  },

  WebGLMicrophone_StopRecording: function() {
    if (window.webGLMicrophoneProcessor && window.webGLMicrophoneContext) {
      window.webGLMicrophoneContext.suspend();
      return true;
    }
    return false;
  },

  WebGLMicrophone_Dispose: function() {
    if (window.webGLMicrophoneStream) {
      window.webGLMicrophoneStream.getTracks().forEach(function(track) {
        track.stop();
      });
    }

    if (window.webGLMicrophoneSource) {
      window.webGLMicrophoneSource.disconnect();
    }

    if (window.webGLMicrophoneProcessor) {
      window.webGLMicrophoneProcessor.disconnect();
    }

    window.webGLMicrophoneStream = null;
    window.webGLMicrophoneContext = null;
    window.webGLMicrophoneSource = null;
    window.webGLMicrophoneProcessor = null;
    window.webGLMicrophoneCallback = null;

    console.log("WebGLMicrophone: Disposed");
  },

  WebGLMicrophone_IsSupported: function() {
    return !!(navigator.mediaDevices && navigator.mediaDevices.getUserMedia);
  },

  // WebGL Audio Playback API for Unity
  PlayWebGLAudio: function(identifierPtr, base64AudioPtr) {
    var identifier = UTF8ToString(identifierPtr);
    var base64Audio = UTF8ToString(base64AudioPtr);

    try {
      // Convert base64 to blob
      var binaryString = atob(base64Audio);
      var bytes = new Uint8Array(binaryString.length);
      for (var i = 0; i < binaryString.length; i++) {
        bytes[i] = binaryString.charCodeAt(i);
      }

      var blob = new Blob([bytes], { type: 'audio/mpeg' });
      var audioUrl = URL.createObjectURL(blob);

      // Create and play audio element
      var audio = new Audio(audioUrl);
      audio.crossOrigin = 'anonymous';

      // Clean up blob URL after audio loads
      audio.onloadeddata = function() {
        URL.revokeObjectURL(audioUrl);
      };

      // Play the audio
      var playPromise = audio.play();

      if (playPromise !== undefined) {
        playPromise.then(function() {
          console.log("WebGLAudio: Playing audio for " + identifier);
        }).catch(function(error) {
          console.error("WebGLAudio: Failed to play audio for " + identifier + ":", error);
        });
      }

    } catch (error) {
      console.error("WebGLAudio: Error playing audio for " + identifier + ":", error);
    }
  }

});
