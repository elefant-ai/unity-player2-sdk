mergeInto(LibraryManager.library, {

  // WebGL Microphone API for Unity
  WebGLMicrophone_Init: function(gameObjectNamePtr, callbackMethodNamePtr) {
    var gameObjectName = Pointer_stringify(gameObjectNamePtr);
    var callbackMethodName = Pointer_stringify(callbackMethodNamePtr);

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
  }

});
