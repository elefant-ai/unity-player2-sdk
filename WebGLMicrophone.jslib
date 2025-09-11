mergeInto(LibraryManager.library, {

  // Get AudioWorklet processor code as string
  WebGLMicrophone_GetWorkletCode: function() {
    return `
      class WebGLMicrophoneProcessor extends AudioWorkletProcessor {
        constructor() {
          try {
            super();
            this.bufferSize = 4096;
            this.buffer = new Float32Array(this.bufferSize);
            this.bufferIndex = 0;

            // Bind the sendAudioData method to preserve this context
            this.sendAudioData = this.sendAudioData.bind(this);

            console.log('AudioWorkletProcessor constructor completed, port available:', !!this.port);
          } catch (error) {
            console.error('AudioWorkletProcessor constructor error:', error);
            throw error;
          }
        }

        process(inputs, outputs, parameters) {
          const input = inputs[0];
          if (input && input.length > 0) {
            const inputData = input[0];
            for (let i = 0; i < inputData.length; i++) {
              this.buffer[this.bufferIndex++] = inputData[i];
              if (this.bufferIndex >= this.bufferSize) {
                this.sendAudioData();
                this.bufferIndex = 0;
              }
            }
          }
          return true;
        }

        sendAudioData() {
          try {
            const uint8Array = new Uint8Array(this.buffer.buffer);
            let binaryString = '';
            for (let i = 0; i < uint8Array.length; i++) {
              binaryString += String.fromCharCode(uint8Array[i]);
            }
            // Use btoa if available, otherwise fallback to manual encoding
            const base64Data = typeof btoa !== 'undefined' ? btoa(binaryString) : this.base64Encode(binaryString);

            // Check if port is available before using it
            if (this.port && typeof this.port.postMessage === 'function') {
              this.port.postMessage({
                type: 'audioData',
                base64Data: base64Data
              });
            } else {
              console.warn('AudioWorkletProcessor: MessagePort not available');
            }
          } catch (error) {
            console.error('AudioWorkletProcessor sendAudioData error:', error);
          }
        }

        base64Encode(str) {
          // Fallback base64 encoding for worklet context
          const chars = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/=';
          let result = '';
          let i = 0;
          
          while (i < str.length) {
            const a = str.charCodeAt(i++);
            const b = i < str.length ? str.charCodeAt(i++) : 0;
            const c = i < str.length ? str.charCodeAt(i++) : 0;
            
            const bitmap = (a << 16) | (b << 8) | c;
            
            result += chars.charAt((bitmap >> 18) & 63);
            result += chars.charAt((bitmap >> 12) & 63);
            result += i - 2 < str.length ? chars.charAt((bitmap >> 6) & 63) : '=';
            result += i - 1 < str.length ? chars.charAt(bitmap & 63) : '=';
          }
          
          return result;
        }
      }

      try {
        registerProcessor('webgl-microphone-processor', WebGLMicrophoneProcessor);
        console.log('WebGLMicrophoneProcessor registered successfully');
      } catch (error) {
        console.error('Failed to register WebGLMicrophoneProcessor:', error);
        throw error;
      }
    `;
  },

  // WebGL Microphone API for Unity  
  WebGLMicrophone_Init: function(gameObjectNamePtr, callbackMethodNamePtr) {
    var gameObjectName = UTF8ToString(gameObjectNamePtr);
    var callbackMethodName = UTF8ToString(callbackMethodNamePtr);

    if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
      console.warn("WebGLMicrophone: getUserMedia not supported");
      Module.SendMessage(gameObjectName, callbackMethodName, '0');
      return false;
    }

    // Helper function to initialize legacy ScriptProcessorNode (to avoid code duplication)
    var initScriptProcessor = function(stream, context, source, goName, cbName) {
      try {
        // Initialize recording state flag
        window.webGLMicrophoneRecording = false;
        window.webGLMicrophoneProcessor = context.createScriptProcessor(4096, 1, 1);
        
        window.webGLMicrophoneProcessor.onaudioprocess = function(event) {
          if (!window.webGLMicrophoneRecording) return;
          
          var inputBuffer = event.inputBuffer;
          var inputData = inputBuffer.getChannelData(0);
          var floatArray = new Float32Array(inputData);
          var uint8Array = new Uint8Array(floatArray.buffer);
          var binaryString = '';
          for (var i = 0; i < uint8Array.length; i++) {
            binaryString += String.fromCharCode(uint8Array[i]);
          }
          var base64Data = btoa(binaryString);
          Module.SendMessage(goName, 'OnWebGLAudioData', base64Data);
        };

        source.connect(window.webGLMicrophoneProcessor);
        window.webGLMicrophoneProcessor.connect(context.destination);
        Module.SendMessage(goName, cbName, '1');
        console.log("WebGLMicrophone: Initialized successfully with ScriptProcessorNode");
        return true;
      } catch (error) {
        console.error('Failed to initialize ScriptProcessorNode:', error);
        Module.SendMessage(goName, cbName, '0');
        return false;
      }
    };

    // Check if AudioWorklet is supported
    if (!window.AudioContext || !('audioWorklet' in AudioContext.prototype)) {
      console.warn("WebGLMicrophone: AudioWorklet not supported, falling back to ScriptProcessorNode");
      navigator.mediaDevices.getUserMedia({ audio: true })
        .then(function(stream) {
          window.webGLMicrophoneStream = stream;
          window.webGLMicrophoneContext = new (window.AudioContext || window.webkitAudioContext)();
          window.webGLMicrophoneSource = window.webGLMicrophoneContext.createMediaStreamSource(stream);
          initScriptProcessor(stream, window.webGLMicrophoneContext, window.webGLMicrophoneSource, gameObjectName, callbackMethodName);
        })
        .catch(function(error) {
          console.error("WebGLMicrophone: Failed to get microphone access:", error);
          Module.SendMessage(gameObjectName, callbackMethodName, '0');
        });
      return true;
    }

    // Request microphone permission and initialize with AudioWorklet
    navigator.mediaDevices.getUserMedia({ audio: true })
      .then(function(stream) {
        // Capture variables in closure
        var capturedGameObjectName = gameObjectName;
        var capturedCallbackMethodName = callbackMethodName;

        window.webGLMicrophoneStream = stream;
        window.webGLMicrophoneContext = new (window.AudioContext || window.webkitAudioContext)();
        window.webGLMicrophoneSource = window.webGLMicrophoneContext.createMediaStreamSource(stream);

        // Initialize AudioWorklet with inline worklet code
        var workletCode = `
          class WebGLMicrophoneProcessor extends AudioWorkletProcessor {
            constructor() {
              try {
                super();
                this.bufferSize = 4096;
                this.buffer = new Float32Array(this.bufferSize);
                this.bufferIndex = 0;
                this.sendAudioData = this.sendAudioData.bind(this);
                console.log('AudioWorkletProcessor constructor completed, port available:', !!this.port);
              } catch (error) {
                console.error('AudioWorkletProcessor constructor error:', error);
                throw error;
              }
            }

            process(inputs, outputs, parameters) {
              const input = inputs[0];
              if (input && input.length > 0) {
                const inputData = input[0];
                for (let i = 0; i < inputData.length; i++) {
                  this.buffer[this.bufferIndex++] = inputData[i];
                  if (this.bufferIndex >= this.bufferSize) {
                    this.sendAudioData();
                    this.bufferIndex = 0;
                  }
                }
              }
              return true;
            }

            sendAudioData() {
              try {
                const uint8Array = new Uint8Array(this.buffer.buffer);
                let binaryString = '';
                for (let i = 0; i < uint8Array.length; i++) {
                  binaryString += String.fromCharCode(uint8Array[i]);
                }
                const base64Data = typeof btoa !== 'undefined' ? btoa(binaryString) : this.base64Encode(binaryString);

                if (this.port && typeof this.port.postMessage === 'function') {
                  this.port.postMessage({
                    type: 'audioData',
                    base64Data: base64Data
                  });
                } else {
                  console.warn('AudioWorkletProcessor: MessagePort not available');
                }
              } catch (error) {
                console.error('AudioWorkletProcessor sendAudioData error:', error);
              }
            }

            base64Encode(str) {
              const chars = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/=';
              let result = '';
              let i = 0;
              
              while (i < str.length) {
                const a = str.charCodeAt(i++);
                const b = i < str.length ? str.charCodeAt(i++) : 0;
                const c = i < str.length ? str.charCodeAt(i++) : 0;
                
                const bitmap = (a << 16) | (b << 8) | c;
                
                result += chars.charAt((bitmap >> 18) & 63);
                result += chars.charAt((bitmap >> 12) & 63);
                result += i - 2 < str.length ? chars.charAt((bitmap >> 6) & 63) : '=';
                result += i - 1 < str.length ? chars.charAt(bitmap & 63) : '=';
              }
              
              return result;
            }
          }

          try {
            registerProcessor('webgl-microphone-processor', WebGLMicrophoneProcessor);
            console.log('WebGLMicrophoneProcessor registered successfully');
          } catch (error) {
            console.error('Failed to register WebGLMicrophoneProcessor:', error);
            throw error;
          }
        `;
        var blob = new Blob([workletCode], { type: 'application/javascript' });
        var workletUrl = URL.createObjectURL(blob);

        return window.webGLMicrophoneContext.audioWorklet.addModule(workletUrl)
          .then(function() {
            console.log('AudioWorklet module loaded successfully');
            URL.revokeObjectURL(workletUrl);
          })
          .catch(function(error) {
            console.error('Failed to load AudioWorklet module:', error);
            URL.revokeObjectURL(workletUrl);
            throw error;
          })
          .then(function() {
            try {
              console.log('Creating AudioWorkletNode...');
              window.webGLMicrophoneProcessor = new AudioWorkletNode(window.webGLMicrophoneContext, 'webgl-microphone-processor');
              console.log('AudioWorkletNode created successfully');

              // Initialize recording state flag
              window.webGLMicrophoneRecording = false;
              
              // Set up message handler for audio data from worklet
              window.webGLMicrophoneProcessor.port.onmessage = function(event) {
                if (event.data.type === 'audioData' && window.webGLMicrophoneRecording) {
                  // Send audio data to Unity via SendMessage
                  Module.SendMessage(capturedGameObjectName, 'OnWebGLAudioData', event.data.base64Data);
                }
              };

              console.log('Connecting AudioWorkletNode...');
              window.webGLMicrophoneSource.connect(window.webGLMicrophoneProcessor);
              window.webGLMicrophoneProcessor.connect(window.webGLMicrophoneContext.destination);
              console.log('AudioWorkletNode connected successfully');

              // Confirm initialization
              Module.SendMessage(capturedGameObjectName, capturedCallbackMethodName, '1');
              console.log("WebGLMicrophone: Initialized successfully with AudioWorklet");
            } catch (error) {
              console.error('Error creating/connecting AudioWorkletNode:', error);
              throw error;
            }
          })
          .catch(function(innerError) {
            console.error('AudioWorklet initialization failed, falling back to ScriptProcessorNode:', innerError);
            // Fallback to ScriptProcessorNode using the existing stream and context
            initScriptProcessor(window.webGLMicrophoneStream, window.webGLMicrophoneContext, window.webGLMicrophoneSource, capturedGameObjectName, capturedCallbackMethodName);
          });
      })
      .catch(function(error) {
        console.error("WebGLMicrophone: Failed to get microphone access:", error);
        Module.SendMessage(gameObjectName, callbackMethodName, '0');
      });

    return true;
  },

  // Legacy implementation using deprecated ScriptProcessorNode for fallback
  WebGLMicrophone_InitLegacy: function(gameObjectName, callbackMethodName) {
    // Request microphone permission and initialize
    navigator.mediaDevices.getUserMedia({ audio: true })
      .then(function(stream) {
        // Capture variables in closure
        var capturedGameObjectName = gameObjectName;
        var capturedCallbackMethodName = callbackMethodName;

        window.webGLMicrophoneStream = stream;
        window.webGLMicrophoneContext = new (window.AudioContext || window.webkitAudioContext)();
        window.webGLMicrophoneSource = window.webGLMicrophoneContext.createMediaStreamSource(stream);
        
        // Initialize recording state flag
        window.webGLMicrophoneRecording = false;
        window.webGLMicrophoneProcessor = window.webGLMicrophoneContext.createScriptProcessor(4096, 1, 1);

        window.webGLMicrophoneProcessor.onaudioprocess = function(event) {
          if (!window.webGLMicrophoneRecording) return;
          
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
          Module.SendMessage(capturedGameObjectName, 'OnWebGLAudioData', base64Data);
        };

        window.webGLMicrophoneSource.connect(window.webGLMicrophoneProcessor);
        window.webGLMicrophoneProcessor.connect(window.webGLMicrophoneContext.destination);

        // Confirm initialization
        Module.SendMessage(capturedGameObjectName, capturedCallbackMethodName, '1');
        console.log("WebGLMicrophone: Initialized successfully with legacy ScriptProcessorNode");
      })
      .catch(function(error) {
        console.error("WebGLMicrophone: Failed to get microphone access:", error);
        Module.SendMessage(gameObjectName, callbackMethodName, '0');
      });

    return true;
  },

  WebGLMicrophone_StartRecording: function() {
    if (window.webGLMicrophoneProcessor && window.webGLMicrophoneContext) {
      // Enable recording flag to start sending audio data
      window.webGLMicrophoneRecording = true;
      
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
      // Disable recording flag to stop sending audio data
      window.webGLMicrophoneRecording = false;
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
      // Handle both AudioWorkletNode and ScriptProcessorNode
      if (window.webGLMicrophoneProcessor.disconnect) {
        window.webGLMicrophoneProcessor.disconnect();
      }

      // If it's an AudioWorkletNode, close the port
      if (window.webGLMicrophoneProcessor.port) {
        window.webGLMicrophoneProcessor.port.close();
      }
    }

    if (window.webGLMicrophoneContext) {
      // Close AudioContext if possible
      if (window.webGLMicrophoneContext.close) {
        window.webGLMicrophoneContext.close().catch(function(error) {
          console.warn("WebGLMicrophone: Error closing AudioContext:", error);
        });
      }
    }

    window.webGLMicrophoneStream = null;
    window.webGLMicrophoneContext = null;
    window.webGLMicrophoneSource = null;
    window.webGLMicrophoneProcessor = null;
    window.webGLMicrophoneRecording = false;

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
