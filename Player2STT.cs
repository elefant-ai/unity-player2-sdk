using UnityEngine;
using UnityEngine.Events;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NativeWebSocket;

#if UNITY_WEBGL && !UNITY_EDITOR
using Player2SDK.WebGL;
#endif

/// <summary>
/// WebSocket abstraction for cross-platform compatibility
/// </summary>
public interface IWebSocketConnection
{
    WebSocketState State { get; }
    event Action OnOpen;
    event Action<byte[]> OnMessage;
    event Action<string> OnError;
    event Action<WebSocketCloseCode> OnClose;

    Task Connect();
    Task Send(byte[] data);
    Task SendText(string text);
    Task Close();
    void DispatchMessageQueue();
}

/// <summary>
/// WebSocket implementation for non-WebGL platforms
/// </summary>
public class NativeWebSocketConnection : IWebSocketConnection
{
    private WebSocket webSocket;

    public WebSocketState State => webSocket?.State ?? WebSocketState.Closed;

    public event Action OnOpen;
    public event Action<byte[]> OnMessage;
    public event Action<string> OnError;
    public event Action<WebSocketCloseCode> OnClose;

    public NativeWebSocketConnection(string url, Dictionary<string, string> headers = null)
    {
        webSocket = new WebSocket(url, headers);

        webSocket.OnOpen += () => OnOpen?.Invoke();
        webSocket.OnMessage += (bytes) => OnMessage?.Invoke(bytes);
        webSocket.OnError += (error) => OnError?.Invoke(error);
        webSocket.OnClose += (code) => OnClose?.Invoke(code);
    }

    public Task Connect() => webSocket.Connect();
    public Task Send(byte[] data) => webSocket.Send(data);
    public Task SendText(string text) => webSocket.SendText(text);
    public Task Close() => webSocket.Close();
#if !UNITY_WEBGL
    public void DispatchMessageQueue() => webSocket?.DispatchMessageQueue();
#else
    public void DispatchMessageQueue() { }
#endif
}

#if UNITY_WEBGL && !UNITY_EDITOR
/// <summary>
/// WebSocket implementation for WebGL platform
/// </summary>
public class WebGLWebSocketConnection : IWebSocketConnection
{
    private WebSocket webSocket;

    public WebSocketState State => webSocket?.State ?? WebSocketState.Closed;

    public event Action OnOpen;
    public event Action<byte[]> OnMessage;
    public event Action<string> OnError;
    public event Action<WebSocketCloseCode> OnClose;

    public WebGLWebSocketConnection(string url)
    {
        webSocket = new WebSocket(url);

        webSocket.OnOpen += () => OnOpen?.Invoke();
        webSocket.OnMessage += (bytes) => OnMessage?.Invoke(bytes);
        webSocket.OnError += (error) => OnError?.Invoke(error);
        webSocket.OnClose += (code) => OnClose?.Invoke(code);
    }

    public Task Connect() => webSocket.Connect();
    public Task Send(byte[] data) => webSocket.Send(data);
    public Task SendText(string text) => webSocket.SendText(text);
    public Task Close() => webSocket.Close();

    // WebGL doesn't need DispatchMessageQueue - browser handles it automatically
    public void DispatchMessageQueue() { }
}
#endif

namespace player2_sdk
{
    /// <summary>
    /// Real-time Speech-to-Text using WebSocket streaming
    /// </summary>
    public class Player2STT : MonoBehaviour
    {
        [Header("STT Configuration")]
        [SerializeField] private bool sttEnabled = true;

        [SerializeField] private float heartbeatInterval = 5f;
        [SerializeField] private bool enableVAD = false;
        [SerializeField] private bool enableInterimResults = false;

        [Header("Audio Settings")]
        [SerializeField] private int sampleRate = 44100;
        [SerializeField] private int audioChunkDurationMs = 50;

        [Header("API Configuration")]
        [SerializeField] private NpcManager npcManager;

        [Header("Events")]
        public STTReceivedEvent OnSTTReceived;
        public STTFailedEvent OnSTTFailed;
        public UnityEvent OnListeningStarted;
        public UnityEvent OnListeningStopped;

        public bool Listening { get; private set; }
        private IWebSocketConnection webSocket;
        private AudioClip microphoneClip;
        private string microphoneDevice;

        private string currentTranscript = "";
        private bool audioStreamRunning;
        private int lastMicrophonePosition;
        private Coroutine audioStreamCoroutine;
        private Coroutine heartbeatCoroutine;

#if UNITY_WEBGL && !UNITY_EDITOR
        private WebGLMicrophoneManager webGLMicManager;
#endif
        private CancellationTokenSource connectionCts;



        #region Public Methods

        /// <summary>
        /// Begin listening for speech. If already listening, do nothing.
        /// </summary>
        public void StartSTT()
        {
            if (!sttEnabled || Listening) return;

            if (!HasApiConnection())
            {
                EstablishConnection();
                return;
            }

            StartSTTInternal();
            SetListening(true);
        }

        /// <summary>
        /// Stop listening for speech and close the streaming connection.
        /// </summary>
        public void StopSTT()
        {
            if (!Listening) return;

            StopSTTInternal();
            SetListening(false);
        }

        public void ToggleSTT()
        {
            if (Listening)
                StopSTT();
            else
                StartSTT();
        }

        private void Update()
        {
            webSocket?.DispatchMessageQueue();
        }

        private void OnDestroy()
        {
            StopAllTimers();
            CloseWebSocket();
            StopMicrophone();

            if (connectionCts != null)
            {
                connectionCts.Cancel();
                connectionCts.Dispose();
                connectionCts = null;
            }
        }

        /// <summary>
        /// Check if Speech-to-Text is supported on the current platform
        /// </summary>
#if UNITY_WEBGL && !UNITY_EDITOR
        public bool IsSTTSupported => webGLMicManager != null && webGLMicManager.IsInitialized;
#else
        public bool IsSTTSupported => true;
#endif

        #endregion

        #region WebGL Methods

#if UNITY_WEBGL && !UNITY_EDITOR
        /// <summary>
        /// Callback when WebGL microphone is initialized
        /// </summary>
        private void OnWebGLMicInitialized(bool success)
        {
            if (success)
            {
                Debug.Log("Player2STT: WebGL microphone initialized successfully");
            }
            else
            {
                Debug.LogWarning("Player2STT: WebGL microphone initialization failed (expected in Unity Editor)");
            }
        }

        /// <summary>
        /// Callback when audio data is received from WebGL microphone
        /// </summary>
        private void OnWebGLAudioDataReceived(float[] audioData)
        {
            if (audioData == null || audioData.Length == 0)
                return;

            // Only send audio data if WebSocket is connected and ready
            if (webSocket == null || webSocket.State != WebSocketState.Open)
            {
                return;
            }

            // Process audio data similar to non-WebGL version
            byte[] audioBytes = ConvertAudioToBytes(audioData);

            if (audioBytes.Length > 0)
            {
                try
                {
                    _ = webSocket.Send(audioBytes);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Failed to send WebGL audio data: {ex.Message}");
                }
            }
        }
#endif

        #endregion

        #region Private Methods

        private void Start()
        {
            if (npcManager == null)
            {
                Debug.LogError("Player2STT requires an NpcManager reference. Please assign it in the inspector.", this);
                return;
            }

#if UNITY_WEBGL && !UNITY_EDITOR
            // Initialize WebGL microphone manager (only in actual WebGL builds)
            webGLMicManager = gameObject.AddComponent<WebGLMicrophoneManager>();
            webGLMicManager.OnAudioDataReceived += OnWebGLAudioDataReceived;
            webGLMicManager.OnInitialized += OnWebGLMicInitialized;
            webGLMicManager.Initialize();
#else
            // Use regular Unity microphone (in editor or non-WebGL builds)
            if (Microphone.devices.Length > 0)
            {
                microphoneDevice = Microphone.devices[0];
            }
            else
            {
                Debug.LogError("Player2STT: No microphone devices found!");
            }
#endif
        }

        private bool HasApiConnection()
        {
            bool hasConnection = npcManager != null && !string.IsNullOrEmpty(npcManager.apiKey);
            Debug.Log($"Player2STT: HasApiConnection check - npcManager: {npcManager != null}, apiKey: {!string.IsNullOrEmpty(npcManager?.apiKey)}, result: {hasConnection}");
            return hasConnection;
        }

        private void EstablishConnection()
        {
            if (npcManager == null)
            {
                Debug.LogError("NpcManager is not assigned to Player2STT. Cannot establish connection.");
            }
        }

        private void StartSTTInternal()
        {
            if (sttEnabled)
            {
                Debug.Log($"Player2STT: Starting STT. API key available: {!string.IsNullOrEmpty(npcManager?.apiKey)}");
                if (!string.IsNullOrEmpty(npcManager?.apiKey))
                {
                    Debug.Log($"Player2STT: API key starts with: {npcManager.apiKey.Substring(0, Math.Min(10, npcManager.apiKey.Length))}");
                }
                StartSTTWeb();
            }
        }

        private void StopSTTInternal()
        {
            StopSTTWeb();
        }

        private void StartSTTWeb()
        {
            if (audioStreamRunning)
            {
                StopAllTimers();
            }

            currentTranscript = "";

            if (!HasApiConnection())
            {
                EstablishConnection();
                return;
            }

            CloseWebSocket();
            InitializeWebSocket();

            // Set audioStreamRunning BEFORE starting microphone to avoid race condition
            audioStreamRunning = true;
            StartMicrophone();
        }

        private void SendSTTConfiguration()
        {
            if (webSocket?.State != WebSocketState.Open) return;

            try
            {
                var config = new
                {
                    type = "configure",
                    data = new
                    {
                        sample_rate = sampleRate,
                        encoding = "linear16",
                        channels = 1,
                        interim_results = enableInterimResults,
                        vad_events = enableVAD,
                        punctuate = true,
                        smart_format = true,
                        profanity_filter = false,
                        redact = new string[0],
                        diarize = false,
                        multichannel = false,
                        numerals = false,
                        search = new string[0],
                        replace = new string[0],
                        keywords = new string[0]
                    }
                };

                string configJson = JsonConvert.SerializeObject(config);
                _ = webSocket.SendText(configJson);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to send STT configuration: {ex.Message}");
            }
        }

        private void StopSTTWeb()
        {
            if (audioStreamRunning)
            {
                StopMicrophone();
                audioStreamRunning = false;
            }

            CleanupSTTSession();
        }

        private void InitializeWebSocket()
        {
            if (npcManager == null)
            {
                Debug.LogError("NpcManager is not assigned to Player2STT");
                return;
            }

            try
            {
                string baseUrl = npcManager.GetBaseUrl();
                string websocketUrl = baseUrl.Replace("http://", "ws://").Replace("https://", "wss://");

                var queryParams = new List<string>
                {
                    $"sample_rate={sampleRate}",
                    "encoding=linear16",
                    "channels=1",
                    $"vad_events={enableVAD.ToString().ToLower()}",
                    $"interim_results={enableInterimResults.ToString().ToLower()}",
                    "punctuate=true",
                    "smart_format=true"
                };

                // Add token to query parameters (works for both WebGL and native)
                if (!string.IsNullOrEmpty(npcManager.apiKey))
                {
                    queryParams.Add($"token={npcManager.apiKey}");
                    Debug.Log($"Player2STT: Adding token to query params: {npcManager.apiKey.Substring(0, Math.Min(10, npcManager.apiKey.Length))}...");
                }
                else
                {
                    Debug.LogError("Player2STT: API key is null or empty! Cannot authenticate WebSocket connection.");
                }

                string url = $"{websocketUrl}/stt/stream?{string.Join("&", queryParams)}";

#if UNITY_WEBGL && !UNITY_EDITOR
                webSocket = new WebGLWebSocketConnection(url);
#else
                webSocket = new NativeWebSocketConnection(url);
#endif

                webSocket.OnOpen += () => {
                    SendSTTConfiguration();
                    if (heartbeatCoroutine != null)
                        StopCoroutine(heartbeatCoroutine);
                    heartbeatCoroutine = StartCoroutine(HeartbeatLoop());

                    // Start microphone only after WebSocket is connected
#if UNITY_WEBGL && !UNITY_EDITOR
                    if (webGLMicManager != null && webGLMicManager.IsInitialized && audioStreamRunning)
                    {
                        webGLMicManager.StartRecording();
                    }
#endif
                };

                webSocket.OnMessage += (bytes) => {
                    try
                    {
                        string message = System.Text.Encoding.UTF8.GetString(bytes);
                        OnWebSocketText(message);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Error processing WebSocket message: {ex.Message}");
                    }
                };

                webSocket.OnError += (error) => {
                    Debug.LogError($"WebSocket error: {error}");
                    OnSTTFailed?.Invoke($"WebSocket error: {error}", -1);
                    SetListening(false);
                };

                webSocket.OnClose += (closeCode) => {
                    if (closeCode != WebSocketCloseCode.Normal)
                    {
                        OnSTTFailed?.Invoke($"WebSocket closed with code: {closeCode}", (int)closeCode);
                    }
                    SetListening(false);
                };

                _ = webSocket.Connect();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to initialize WebSocket: {ex.Message}");
                OnSTTFailed?.Invoke($"WebSocket initialization failed: {ex.Message}", -1);
            }
        }



        private IEnumerator HeartbeatLoop()
        {
            while (webSocket?.State == WebSocketState.Open)
            {
                yield return new WaitForSeconds(heartbeatInterval);

                if (webSocket?.State == WebSocketState.Open)
                {
                    try
                    {
                        var keepAlive = JsonConvert.SerializeObject(new { type = "KeepAlive" });
                        _ = webSocket.SendText(keepAlive);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"Failed to send heartbeat: {ex.Message}");
                        break;
                    }
                }
            }
        }



        private void CloseWebSocket()
        {
            try
            {
                if (heartbeatCoroutine != null)
                {
                    StopCoroutine(heartbeatCoroutine);
                    heartbeatCoroutine = null;
                }

                if (connectionCts != null)
                {
                    connectionCts.Cancel();
                    connectionCts.Dispose();
                    connectionCts = null;
                }

                if (webSocket != null)
                {
                    _ = webSocket.Close();
                    webSocket = null;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error closing WebSocket: {ex.Message}");
            }
        }

        private void StartMicrophone()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            // For WebGL, microphone recording is started when WebSocket connects
            // This prevents sending audio data before the connection is ready
            if (webGLMicManager != null && webGLMicManager.IsInitialized)
            {
                // WebGL microphone will be started when WebSocket OnOpen event fires
                Debug.Log("Player2STT: WebGL microphone ready, will start when WebSocket connects");
            }
            else
            {
                Debug.LogWarning("Player2STT: WebGL microphone not initialized");
            }
#else
            if (string.IsNullOrEmpty(microphoneDevice))
            {
                Debug.LogError("Cannot start microphone: no device selected");
                return;
            }

            StopMicrophone();

            microphoneClip = Microphone.Start(microphoneDevice, true, 10, sampleRate);
            lastMicrophonePosition = 0;

            if (audioStreamCoroutine != null)
                StopCoroutine(audioStreamCoroutine);
            audioStreamCoroutine = StartCoroutine(StreamAudioData());
#endif
        }

        private void StopMicrophone()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (webGLMicManager != null && webGLMicManager.IsRecording)
            {
                webGLMicManager.StopRecording();
            }
#else
            if (Microphone.IsRecording(microphoneDevice))
            {
                Microphone.End(microphoneDevice);
            }
#endif

            if (audioStreamCoroutine != null)
            {
                StopCoroutine(audioStreamCoroutine);
                audioStreamCoroutine = null;
            }
        }

        private IEnumerator StreamAudioData()
        {
            float chunkDuration = audioChunkDurationMs / 1000f;

#if UNITY_WEBGL && !UNITY_EDITOR
            Debug.LogWarning("Player2STT: Audio streaming is not supported in WebGL builds.");
            yield break;
#else
            if (!audioStreamRunning || !Microphone.IsRecording(microphoneDevice))
                yield break;

            while (audioStreamRunning && Microphone.IsRecording(microphoneDevice))
            {
                ProcessAudioChunk();
                yield return new WaitForSeconds(chunkDuration);
            }
#endif
        }

        private void ProcessAudioChunk()
        {
            if (microphoneClip == null || webSocket?.State != WebSocketState.Open)
                return;

#if UNITY_WEBGL && !UNITY_EDITOR
            Debug.LogWarning("Player2STT: Audio processing is not supported in WebGL builds.");
#else
            int currentPosition = Microphone.GetPosition(microphoneDevice);

            if (currentPosition == lastMicrophonePosition)
                return;

            int samplesToRead;
            if (currentPosition > lastMicrophonePosition)
            {
                samplesToRead = currentPosition - lastMicrophonePosition;
            }
            else
            {
                samplesToRead = (microphoneClip.samples - lastMicrophonePosition) + currentPosition;
            }

            if (samplesToRead > 0)
            {
                float[] audioData = new float[samplesToRead];

                if (currentPosition > lastMicrophonePosition)
                {
                    microphoneClip.GetData(audioData, lastMicrophonePosition);
                }
                else
                {
                    int firstPart = microphoneClip.samples - lastMicrophonePosition;
                    float[] firstPartData = new float[firstPart];
                    float[] secondPartData = new float[currentPosition];

                    microphoneClip.GetData(firstPartData, lastMicrophonePosition);
                    microphoneClip.GetData(secondPartData, 0);

                    Array.Copy(firstPartData, 0, audioData, 0, firstPart);
                    Array.Copy(secondPartData, 0, audioData, firstPart, currentPosition);
                }

                byte[] audioBytes = ConvertAudioToBytes(audioData);

                if (audioBytes.Length > 0)
                {
                    try
                    {
                        _ = webSocket.Send(audioBytes);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Failed to send audio data: {ex.Message}");
                    }
                }

                lastMicrophonePosition = currentPosition;
            }
#endif
        }

        private byte[] ConvertAudioToBytes(float[] audioData)
        {
            byte[] bytes = new byte[audioData.Length * 2];

            for (int i = 0; i < audioData.Length; i++)
            {
                float sample = Mathf.Clamp(audioData[i], -1f, 1f);
                short value = (short)(sample * 32767);

                bytes[i * 2] = (byte)(value & 0xFF);
                bytes[i * 2 + 1] = (byte)((value >> 8) & 0xFF);
            }

            return bytes;
        }

        private void SetListening(bool value)
        {
            if (Listening != value)
            {
                Listening = value;
                if (Listening)
                    OnListeningStarted?.Invoke();
                else
                    OnListeningStopped?.Invoke();
            }
        }



        private void StopAllTimers()
        {
            if (heartbeatCoroutine != null)
            {
                StopCoroutine(heartbeatCoroutine);
                heartbeatCoroutine = null;
            }

            if (audioStreamCoroutine != null)
            {
                StopCoroutine(audioStreamCoroutine);
                audioStreamCoroutine = null;
            }
        }

        private void CleanupSTTSession()
        {
            StopAllTimers();
            currentTranscript = "";
            CloseWebSocket();
            SetListening(false);
        }

        private void FinalizeCurrentUtterance()
        {
            if (!string.IsNullOrEmpty(currentTranscript))
            {
                OnSTTReceived?.Invoke(currentTranscript);
                currentTranscript = "";
            }
        }

        #endregion

        #region WebSocket Event Handlers

        private void OnWebSocketText(string message)
        {
            try
            {
                var response = JsonConvert.DeserializeObject<STTResponse>(message);
                ProcessSTTResponse(response);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error parsing STT response: {ex.Message}");
            }
        }



        private void ProcessSTTResponse(STTResponse response)
        {
            if (response?.type == null) return;

            switch (response.type.ToLower())
            {
                case "results":
                case "message":
                    HandleSTTResults(response);
                    break;

                case "utterance_end":
                    FinalizeCurrentUtterance();
                    break;

                case "error":
                    HandleSTTError(response);
                    break;

                case "close":
                    SetListening(false);
                    break;
            }
        }

        private void HandleSTTResults(STTResponse response)
        {
            if (response.data?.channel?.alternatives != null && response.data.channel.alternatives.Length > 0)
            {
                var bestAlternative = response.data.channel.alternatives
                    .OrderByDescending(alt => alt.confidence)
                    .First();

                if (bestAlternative != null && !string.IsNullOrEmpty(bestAlternative.transcript))
                {
                    string transcript = bestAlternative.transcript.Trim();
                    bool isFinal = response.data.is_final ?? false;

                    if (isFinal)
                    {
                        currentTranscript = transcript;
                        Debug.Log($"STT: {transcript}");
                        OnSTTReceived?.Invoke(currentTranscript);
                    }
                    else if (enableInterimResults)
                    {
                        currentTranscript = transcript;
                    }
                }
            }
        }

        private void HandleSTTError(STTResponse response)
        {
            string errorMessage = response.data?.message ?? "Unknown STT error";
            int errorCode = response.data?.code ?? -1;

            string requestId = response.metadata?.request_id;
            string traceInfo = !string.IsNullOrEmpty(requestId) ? $" (Request-Id: {requestId})" : "";

            Debug.LogError($"STT error: {errorMessage} (Code: {errorCode}){traceInfo}");
            OnSTTFailed?.Invoke(errorMessage, errorCode);
            SetListening(false);
        }

        #endregion
    }

    #region Data Classes

    [Serializable]
    public class STTReceivedEvent : UnityEvent<string> { }

    [Serializable]
    public class STTFailedEvent : UnityEvent<string, int> { }

    [Serializable]
    public class STTResponse
    {
        public string type;
        public STTData data;
        public STTMetadata metadata;
    }

    [Serializable]
    public class STTData
    {
        public STTChannel channel;
        public bool? is_final;
        public string message;
        public int? code;
        public float duration;
        public string[] warnings;
    }

    [Serializable]
    public class STTChannel
    {
        public STTAlternative[] alternatives;
    }

    [Serializable]
    public class STTAlternative
    {
        public string transcript;
        public float confidence;
        public STTWord[] words;
    }

    [Serializable]
    public class STTWord
    {
        public string word;
        public float start;
        public float end;
        public float confidence;
        public string punctuated_word;
    }

    [Serializable]
    public class STTMetadata
    {
        public string request_id;
        public string model_info;
        public float duration;
        public STTModelInfo model_details;
    }

    [Serializable]
    public class STTModelInfo
    {
        public string name;
        public string version;
        public string language;
    }

    #endregion
}
