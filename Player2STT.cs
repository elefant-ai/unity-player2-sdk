using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NativeWebSocket;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Events;

namespace player2_sdk
{
    /// <summary>
    ///     WebSocket abstraction for cross-platform compatibility
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
    ///     WebSocket implementation for non-WebGL platforms
    /// </summary>
    public class NativeWebSocketConnection : IWebSocketConnection
    {
        private readonly WebSocket webSocket;

        public WebSocketState State => webSocket?.State ?? WebSocketState.Closed;

        public event Action OnOpen;
        public event Action<byte[]> OnMessage;
        public event Action<string> OnError;
        public event Action<WebSocketCloseCode> OnClose;

        public NativeWebSocketConnection(string url, Dictionary<string, string> headers = null)
        {
            webSocket = new WebSocket(url, headers);

            webSocket.OnOpen += () => OnOpen?.Invoke();
            webSocket.OnMessage += bytes => OnMessage?.Invoke(bytes);
            webSocket.OnError += error => OnError?.Invoke(error);
            webSocket.OnClose += code => OnClose?.Invoke(code);
        }

        public Task Connect()
        {
            return webSocket.Connect();
        }

        public Task Send(byte[] data)
        {
            return webSocket.Send(data);
        }

        public Task SendText(string text)
        {
            return webSocket.SendText(text);
        }

        public Task Close()
        {
            return webSocket.Close();
        }
#if !UNITY_WEBGL
        public void DispatchMessageQueue()
        {
            webSocket?.DispatchMessageQueue();
        }
#else
        public void DispatchMessageQueue()
        {
        }
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
    /// <summary>
    ///     Real-time Speech-to-Text using WebSocket streaming
    /// </summary>
    public class Player2STT : MonoBehaviour
    {
        [Header("STT Configuration")] [SerializeField]
        private bool sttEnabled = true;

        [SerializeField] private float heartbeatInterval = 5f;
        [SerializeField] private bool enableVAD;
        [SerializeField] private bool enableInterimResults;

        [Header("Reconnection Settings")] [SerializeField]
        private bool enableAutoReconnection = true;

        [SerializeField] private int maxReconnectionAttempts = 5;
        [SerializeField] private float baseReconnectionDelay = 1f;

        [Header("Audio Settings")] [SerializeField]
        private int sampleRate = 44100;

        [SerializeField] private int audioChunkDurationMs = 50;

        [Header("API Configuration")] [SerializeField]
        private NpcManager npcManager;

        [Header("Events")] public STTReceivedEvent OnSTTReceived;
        public STTFailedEvent OnSTTFailed;
        public UnityEvent OnListeningStarted;
        public UnityEvent OnListeningStopped;

        public bool Listening { get; private set; }

        /// <summary>
        ///     Check if the system is currently attempting to reconnect
        /// </summary>
        public bool IsReconnecting => reconnectionCoroutine != null;

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

        // Reconnection fields
        private bool shouldBeListening;
        private int reconnectionAttempts;
        private Coroutine reconnectionCoroutine;


        #region Public Methods

        /// <summary>
        ///     Begin listening for speech. If already listening, do nothing.
        /// </summary>
        public void StartSTT()
        {
            if (!sttEnabled) return;

            shouldBeListening = true;
            reconnectionAttempts = 0;

            if (Listening) return;

            if (!HasApiConnection())
            {
                EstablishConnection();
                return;
            }

            StartSTTInternal();
            SetListening(true);
        }

        /// <summary>
        ///     Stop listening for speech and close the streaming connection.
        /// </summary>
        public void StopSTT()
        {
            shouldBeListening = false;

            if (reconnectionCoroutine != null)
            {
                StopCoroutine(reconnectionCoroutine);
                reconnectionCoroutine = null;
            }

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
            shouldBeListening = false;

            if (reconnectionCoroutine != null)
            {
                StopCoroutine(reconnectionCoroutine);
                reconnectionCoroutine = null;
            }

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
        ///     Check if Speech-to-Text is supported on the current platform
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
                microphoneDevice = Microphone.devices[0];
            else
                Debug.LogError("Player2STT: No microphone devices found!");
#endif
        }

        private bool HasApiConnection()
        {
            var hasManager = npcManager != null;
            var hasApiKey = !string.IsNullOrEmpty(npcManager?.ApiKey);
            var skipAuth = hasManager && npcManager.ShouldSkipAuthentication();

            // Consider connected if we have API key OR if auth is bypassed for hosted scenarios
            var hasConnection = hasManager && (hasApiKey || skipAuth);

            Debug.Log(
                $"Player2STT: HasApiConnection check - npcManager: {hasManager}, apiKey: {hasApiKey}, skipAuth: {skipAuth}, result: {hasConnection}");
            return hasConnection;
        }

        private void EstablishConnection()
        {
            if (npcManager == null)
                Debug.LogError("NpcManager is not assigned to Player2STT. Cannot establish connection.");
        }

        private void StartSTTInternal()
        {
            if (sttEnabled)
            {
                var hasApiKey = !string.IsNullOrEmpty(npcManager?.ApiKey);
                var skipAuth = npcManager != null && npcManager.ShouldSkipAuthentication();

                Debug.Log($"Player2STT: Starting STT. API key available: {hasApiKey}, Skip auth (hosted): {skipAuth}");

                if (hasApiKey)
                    Debug.Log("Player2STT: Using API key authentication");
                else if (skipAuth)
                    Debug.Log("Player2STT: Using hosted authentication (no API key required)");
                else
                    Debug.Log("Player2STT: No authentication method available");

                StartSTTWeb();
            }
        }

        private void StopSTTInternal()
        {
            StopSTTWeb();
        }

        private void StartSTTWeb()
        {
            if (audioStreamRunning) StopAllTimers();

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

                var configJson = JsonConvert.SerializeObject(config);
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
                var baseUrl = npcManager.GetBaseUrl();
                var websocketUrl = baseUrl.Replace("http://", "ws://").Replace("https://", "wss://");

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
                // Skip token requirement for hosted scenarios where authentication is bypassed
                var skipAuth = npcManager.ShouldSkipAuthentication();
                if (!string.IsNullOrEmpty(npcManager.ApiKey))
                {
                    queryParams.Add($"token={npcManager.ApiKey}");
                    Debug.Log("Player2STT: Adding token to query params for authenticated connection");
                }
                else if (skipAuth)
                {
                    Debug.Log("Player2STT: Skipping token authentication for hosted scenario (player2.game domain)");
                }
                else
                {
                    Debug.LogError("Player2STT: API key is null or empty! Cannot authenticate WebSocket connection.");
                }

                var url = $"{websocketUrl}/stt/stream?{string.Join("&", queryParams)}";

#if UNITY_WEBGL && !UNITY_EDITOR
                webSocket = new WebGLWebSocketConnection(url);
#else
                webSocket = new NativeWebSocketConnection(url);
#endif

                webSocket.OnOpen += () =>
                {
                    Debug.Log("WebSocket connected successfully");

                    // Reset reconnection attempts on successful connection
                    reconnectionAttempts = 0;

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

                webSocket.OnMessage += bytes =>
                {
                    try
                    {
                        var message = Encoding.UTF8.GetString(bytes);
                        OnWebSocketText(message);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Error processing WebSocket message: {ex.Message}");
                    }
                };

                webSocket.OnError += error =>
                {
                    Debug.LogError($"WebSocket error: {error}");
                    HandleConnectionLoss($"WebSocket error: {error}", -1);
                };

                webSocket.OnClose += closeCode =>
                {
                    Debug.LogWarning($"WebSocket closed with code: {closeCode}");
                    if (closeCode == WebSocketCloseCode.Normal)
                        // Normal closure - don't attempt reconnection
                        SetListening(false);
                    else
                        // Abnormal closure - attempt reconnection
                        HandleConnectionLoss($"WebSocket closed unexpectedly with code: {closeCode}", (int)closeCode);
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

            if (microphoneClip == null)
            {
                Debug.LogError($"Player2STT: Failed to start microphone recording for device '{microphoneDevice}'");
                return;
            }

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
            if (Microphone.IsRecording(microphoneDevice)) Microphone.End(microphoneDevice);
#endif

            if (audioStreamCoroutine != null)
            {
                StopCoroutine(audioStreamCoroutine);
                audioStreamCoroutine = null;
            }
        }

        private IEnumerator StreamAudioData()
        {
            var chunkDuration = audioChunkDurationMs / 1000f;

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
            var currentPosition = Microphone.GetPosition(microphoneDevice);

            if (currentPosition == lastMicrophonePosition)
                return;

            if (lastMicrophonePosition < 0 || lastMicrophonePosition >= microphoneClip.samples)
            {
                Debug.LogWarning(
                    $"Player2STT: Invalid lastMicrophonePosition {lastMicrophonePosition}, resetting to 0");
                lastMicrophonePosition = 0;
            }

            if (currentPosition < 0 || currentPosition >= microphoneClip.samples)
            {
                Debug.LogWarning($"Player2STT: Invalid currentPosition {currentPosition}, skipping chunk");
                return;
            }

            int samplesToRead;
            if (currentPosition > lastMicrophonePosition)
                samplesToRead = currentPosition - lastMicrophonePosition;
            else
                samplesToRead = microphoneClip.samples - lastMicrophonePosition + currentPosition;

            if (currentPosition < lastMicrophonePosition)
            {
                var expectedSamples = microphoneClip.samples - lastMicrophonePosition + currentPosition;
                if (samplesToRead != expectedSamples)
                {
                    Debug.LogWarning(
                        $"Player2STT: Sample count mismatch in wrap-around case. Expected: {expectedSamples}, Got: {samplesToRead}");
                    samplesToRead = expectedSamples;
                }
            }

            if (samplesToRead > 0 && samplesToRead <= microphoneClip.samples)
            {
                var audioData = new float[samplesToRead];

                try
                {
                    if (currentPosition > lastMicrophonePosition)
                    {
                        var availableSamples = microphoneClip.samples - lastMicrophonePosition;
                        if (samplesToRead > availableSamples)
                        {
                            Debug.LogWarning($"Player2STT: Attempting to read {samplesToRead} samples " +
                                             $"but only {availableSamples} available from position {lastMicrophonePosition}");
                            samplesToRead = availableSamples;
                            Array.Resize(ref audioData, samplesToRead);
                        }

                        microphoneClip.GetData(audioData, lastMicrophonePosition);
                    }
                    else
                    {
                        var firstPartLength = microphoneClip.samples - lastMicrophonePosition;
                        var secondPartLength = currentPosition;

                        if (firstPartLength < 0 || secondPartLength < 0 ||
                            firstPartLength + secondPartLength != samplesToRead)
                        {
                            Debug.LogError("Player2STT: Invalid wrap-around calculation. " +
                                           $"firstPart: {firstPartLength}, secondPart: {secondPartLength}, total: {samplesToRead}");
                            return;
                        }

                        var firstPartData = new float[firstPartLength];
                        var secondPartData = new float[secondPartLength];

                        microphoneClip.GetData(firstPartData, lastMicrophonePosition);
                        microphoneClip.GetData(secondPartData, 0);

                        Array.Copy(firstPartData, 0, audioData, 0, firstPartLength);
                        Array.Copy(secondPartData, 0, audioData, firstPartLength, secondPartLength);
                    }

                    var audioBytes = ConvertAudioToBytes(audioData);

                    if (audioBytes.Length > 0)
                        try
                        {
                            _ = webSocket.Send(audioBytes);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"Failed to send audio data: {ex.Message}");
                        }

                    lastMicrophonePosition = currentPosition;
                }
                catch (Exception ex)
                {
                    Debug.LogError(
                        $"Player2STT: Error processing audio chunk: {ex.Message}\nPosition: {lastMicrophonePosition}->{currentPosition}, Samples: {samplesToRead}");
                    // Reset position on error to prevent getting stuck
                    lastMicrophonePosition = currentPosition;
                }
            }
#endif
        }

        private byte[] ConvertAudioToBytes(float[] audioData)
        {
            var bytes = new byte[audioData.Length * 2];

            for (var i = 0; i < audioData.Length; i++)
            {
                var sample = Mathf.Clamp(audioData[i], -1f, 1f);
                var value = (short)(sample * 32767);

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

        private void HandleConnectionLoss(string errorMessage, int errorCode)
        {
            Debug.LogWarning($"Connection lost: {errorMessage}");

            // Stop current session but don't change shouldBeListening or audioStreamRunning
            SetListening(false);
            CloseWebSocket();

            // Stop microphone but keep audioStreamRunning flag for reconnection
#if UNITY_WEBGL && !UNITY_EDITOR
            if (webGLMicManager != null && webGLMicManager.IsRecording)
            {
                webGLMicManager.StopRecording();
            }
#else
            if (Microphone.IsRecording(microphoneDevice)) Microphone.End(microphoneDevice);

            if (audioStreamCoroutine != null)
            {
                StopCoroutine(audioStreamCoroutine);
                audioStreamCoroutine = null;
            }
#endif

            // Attempt reconnection if we should still be listening and auto-reconnection is enabled
            if (shouldBeListening && enableAutoReconnection && reconnectionAttempts < maxReconnectionAttempts)
            {
                AttemptReconnection();
            }
            else if (shouldBeListening)
            {
                // Auto-reconnection disabled or max attempts reached
                var reason = !enableAutoReconnection
                    ? "Auto-reconnection is disabled"
                    : $"Max reconnection attempts ({maxReconnectionAttempts}) reached";

                Debug.LogError($"{reason}. Stopping STT.");
                OnSTTFailed?.Invoke($"Connection failed: {reason}. {errorMessage}", errorCode);
                shouldBeListening = false;
                audioStreamRunning = false;
                SetListening(false);
            }
            else
            {
                // User manually stopped, don't reconnect
                OnSTTFailed?.Invoke(errorMessage, errorCode);
            }
        }

        private void AttemptReconnection()
        {
            if (reconnectionCoroutine != null) StopCoroutine(reconnectionCoroutine);

            reconnectionCoroutine = StartCoroutine(ReconnectionCoroutine());
        }

        private IEnumerator ReconnectionCoroutine()
        {
            reconnectionAttempts++;
            var delay = baseReconnectionDelay * Mathf.Pow(2, reconnectionAttempts - 1); // Exponential backoff
            delay = Mathf.Min(delay, 30f); // Cap at 30 seconds

            Debug.Log(
                $"Attempting reconnection {reconnectionAttempts}/{maxReconnectionAttempts} in {delay:F1} seconds...");

            yield return new WaitForSeconds(delay);

            if (shouldBeListening && !Listening)
                try
                {
                    Debug.Log($"Reconnection attempt {reconnectionAttempts}/{maxReconnectionAttempts}");

                    // Use StartSTTWeb to ensure proper microphone restart
                    StartSTTWeb();
                    // Reset reconnection attempts on successful connection
                    // (This will be confirmed when WebSocket.OnOpen is called)
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Reconnection attempt {reconnectionAttempts} failed: {ex.Message}");

                    if (reconnectionAttempts < maxReconnectionAttempts)
                    {
                        // Try again
                        AttemptReconnection();
                    }
                    else
                    {
                        // Give up
                        Debug.LogError("All reconnection attempts failed. Stopping STT.");
                        OnSTTFailed?.Invoke($"Failed to reconnect after {maxReconnectionAttempts} attempts", -1);
                        shouldBeListening = false;
                        SetListening(false);
                    }
                }

            reconnectionCoroutine = null;
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
                    var transcript = bestAlternative.transcript.Trim();
                    var isFinal = response.data.is_final ?? false;

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
            var errorMessage = response.data?.message ?? "Unknown STT error";
            var errorCode = response.data?.code ?? -1;

            var requestId = response.metadata?.request_id;
            var traceInfo = !string.IsNullOrEmpty(requestId) ? $" (Request-Id: {requestId})" : "";

            Debug.LogError($"STT error: {errorMessage} (Code: {errorCode}){traceInfo}");
            OnSTTFailed?.Invoke(errorMessage, errorCode);
            SetListening(false);
        }

        #endregion
    }

    #region Data Classes

    [Serializable]
    public class STTReceivedEvent : UnityEvent<string>
    {
    }

    [Serializable]
    public class STTFailedEvent : UnityEvent<string, int>
    {
    }

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
        public string message;
        public float duration;
        public string[] warnings;
        public int? code;
        public bool? is_final;
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