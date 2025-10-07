using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace player2_sdk
{
    /// <summary>
    ///     WebGL-compatible microphone manager that uses browser APIs
    /// </summary>
    public class WebGLMicrophoneManager : MonoBehaviour
    {
        private bool isInitialized;

        /// <summary>
        ///     Check if microphone is currently recording
        /// </summary>
        public bool IsRecording { get; private set; }

        /// <summary>
        ///     Check if microphone is initialized
        /// </summary>
        public bool IsInitialized =>
#if UNITY_WEBGL && !UNITY_EDITOR
            isInitialized;
#else
            false;
#endif

        private void Awake()
        {
            // Ensure this persists across scene loads
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            Dispose();
        }

        [DllImport("__Internal")]
        private static extern bool WebGLMicrophone_Init(string gameObjectName, string callbackMethodName);

        [DllImport("__Internal")]
        private static extern bool WebGLMicrophone_StartRecording();

        [DllImport("__Internal")]
        private static extern bool WebGLMicrophone_StopRecording();

        [DllImport("__Internal")]
        private static extern void WebGLMicrophone_Dispose();

        [DllImport("__Internal")]
        private static extern bool WebGLMicrophone_IsSupported();

        public event Action<float[]> OnAudioDataReceived;
        public event Action<bool> OnInitialized;

        /// <summary>
        ///     Initialize the WebGL microphone
        /// </summary>
        public void Initialize()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (!WebGLMicrophone_IsSupported())
            {
                Debug.LogError("WebGL Microphone: Browser does not support microphone access");
                OnInitialized?.Invoke(false);
                return;
            }

            WebGLMicrophone_Init(gameObject.name, "OnWebGLInitCallback");
#else
            Debug.LogWarning(
                "WebGL Microphone: Not supported in Unity Editor. Microphone functionality will be disabled.");
            OnInitialized?.Invoke(false);
#endif
        }

        /// <summary>
        ///     Start recording audio
        /// </summary>
        public void StartRecording()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (!isInitialized)
            {
                Debug.LogWarning("WebGL Microphone: Not initialized yet");
                return;
            }

            if (WebGLMicrophone_StartRecording())
            {
                IsRecording = true;
                Debug.Log("WebGL Microphone: Recording started");
            }
            else
            {
                Debug.LogError("WebGL Microphone: Failed to start recording");
            }
#else
            Debug.LogWarning("WebGL Microphone: Not supported in Unity Editor");
#endif
        }

        /// <summary>
        ///     Stop recording audio
        /// </summary>
        public void StopRecording()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (WebGLMicrophone_StopRecording())
            {
                IsRecording = false;
                Debug.Log("WebGL Microphone: Recording stopped");
            }
#else
            Debug.LogWarning("WebGL Microphone: Not supported in Unity Editor");
#endif
        }

        /// <summary>
        ///     Clean up resources
        /// </summary>
        public void Dispose()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            WebGLMicrophone_Dispose();
#endif
            isInitialized = false;
            IsRecording = false;
        }

        // Callback from JavaScript via SendMessage
        private void OnWebGLInitCallback(string success)
        {
            isInitialized = success == "1";
            Debug.Log($"WebGL Microphone: Initialization {(isInitialized ? "successful" : "failed")}");
            OnInitialized?.Invoke(isInitialized);
        }

        // Called from JavaScript when audio data is available (via SendMessage)
        private void OnWebGLAudioData(string base64Data)
        {
            if (string.IsNullOrEmpty(base64Data))
                return;

            try
            {
                // Decode base64 to bytes (these are the raw bytes of Float32Array)
                var bytes = Convert.FromBase64String(base64Data);

                // Convert bytes to float array (4 bytes per float)
                var floatCount = bytes.Length / 4;
                var audioData = new float[floatCount];

                for (var i = 0; i < floatCount; i++)
                {
                    // Convert 4 bytes to float (little-endian)
                    var byteIndex = i * 4;
                    if (BitConverter.IsLittleEndian)
                    {
                        audioData[i] = BitConverter.ToSingle(bytes, byteIndex);
                    }
                    else
                    {
                        // Handle big-endian systems by reversing byte order
                        var floatBytes = new byte[4];
                        Array.Copy(bytes, byteIndex, floatBytes, 0, 4);
                        Array.Reverse(floatBytes);
                        audioData[i] = BitConverter.ToSingle(floatBytes, 0);
                    }
                }

                OnAudioDataReceived?.Invoke(audioData);
            }
            catch (Exception ex)
            {
                Debug.LogError($"WebGL Microphone: Error processing audio data: {ex.Message}");
            }
        }
    }
}