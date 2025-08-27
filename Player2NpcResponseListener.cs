namespace player2_sdk
{


    using System;
    using System.Collections.Generic;
    using System.Text;
    using JetBrains.Annotations;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using UnityEngine;
    using UnityEngine.Events;
    using UnityEngine.Networking;

    [Serializable]
    public class NpcApiChatResponse
    {
        public string npc_id;
        [CanBeNull] public string message;
        [CanBeNull] public SingleTextToSpeechData audio;
        [CanBeNull] public List<FunctionCallResponse> command;
    }

    [Serializable]
    public class SingleTextToSpeechData
    {
        public string data;
    }

    [Serializable]
    public class FunctionCallResponse
    {
        public string name;
        public string arguments;

    public FunctionCall ToFunctionCall(GameObject ai)
        {
            var args = JsonConvert.DeserializeObject<JObject>(arguments);
            return new FunctionCall
            {
                name = name,
                arguments = args,
                aiObject = ai
            };
        }
    }

    [Serializable]
    public class FunctionCall
    {
        public string name;
        public JObject arguments;
        public GameObject aiObject;
    }



    [Serializable]
    public class NpcResponseEvent : UnityEvent<NpcApiChatResponse>
    {
    }

    public class Player2NpcResponseListener : MonoBehaviour
    {
        public string _baseUrl = null;
        [SerializeField] private float _reconnectDelay = 2.0f;
        [SerializeField] private int _maxReconnectAttempts = 5;

        private string apiKey;
        private bool _isListening = false;
        private int _reconnectAttempts = 0;

        private Dictionary<string, UnityEvent<NpcApiChatResponse>> _responseEvents =
            new Dictionary<string, UnityEvent<NpcApiChatResponse>>();

        public JsonSerializerSettings JsonSerializerSettings;
        public UnityEvent<string> newApiKey = new UnityEvent<string>();


        public Player2NpcResponseListener(JsonSerializerSettings jsonSerializerSettings)
        {
            this.JsonSerializerSettings = jsonSerializerSettings;
            
        }
        
        public bool IsListening => _isListening;

      

        private void Awake()
        {
            // Ensure JsonSerializerSettings is initialized
            if (newApiKey == null)
            {
                newApiKey = new UnityEvent<string>();
            }
            if (_responseEvents == null)
            {
                _responseEvents = new Dictionary<string, UnityEvent<NpcApiChatResponse>>();
            }


            newApiKey.AddListener((apiKey) =>
            {
                bool start = this.apiKey == null;
                
                this.apiKey = apiKey;
                if (start)
                {
                    StartListening();
                }
            });
        }
        

        public void RegisterNpc(string npcId, UnityEvent<NpcApiChatResponse> onNpcResponse)
        {
            if (_responseEvents.ContainsKey(npcId))
            {
                _responseEvents[npcId] = onNpcResponse;
            }
            else
            {
                _responseEvents.Add(npcId, onNpcResponse);
            }

            Debug.Log($"Registered NPC response listener for: {npcId}");
        }

        public void UnregisterNpc(string npcId)
        {
            if (_responseEvents.ContainsKey(npcId))
            {
                _responseEvents.Remove(npcId);
                Debug.Log($"Unregistered NPC response listener for: {npcId}");
            }
        }

        public void StartListening()
        {
            if (_isListening)
            {
                Debug.LogWarning("Already listening for responses");
                return;
            }

            if (string.IsNullOrEmpty(apiKey))
            {
                Debug.LogError("Cannot start listening: user is not authenticated");
                return;
            }

            _isListening = true;
            _reconnectAttempts = 0;
            Debug.Log("Starting NPC response listener...");

            // Fire and forget async operation
            _ = ListenForResponsesAsync();
        }

        public void StopListening()
        {
            if (!_isListening)
                return;

            _isListening = false;
            Debug.Log("Stopped listening for NPC responses");
        }

        private async Awaitable ListenForResponsesAsync()
        {
            while (_isListening && this != null)
            {
                try
                {
                    Debug.Log("Starting streaming connection...");
                    await ProcessStreamingResponsesAsync();

                    // If we get here and we're still supposed to be listening, 
                    // it means the connection ended unexpectedly - reconnect
                    if (_isListening)
                    {
                        Debug.LogWarning("Streaming connection ended unexpectedly, attempting to reconnect...");
                        await HandleReconnectionAsync();
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error in response listener: {ex.Message}");

                    if (_isListening)
                    {
                        await HandleReconnectionAsync();
                    }
                    else
                    {
                        Debug.Log("Stopping listener due to error while not listening");
                        break;
                    }
                }
            }

            Debug.Log("Response listener task ended");
        }

        private async Awaitable ProcessStreamingResponsesAsync()
        {
            string url = $"{_baseUrl}/npcs/responses";
            Debug.Log($"Connecting to response stream: {url}");

            using var request = UnityWebRequest.Get(url);

            // Set headers for streaming
            request.SetRequestHeader("Authorization", $"Bearer {apiKey}");
            request.SetRequestHeader("Accept", "application/json");

            request.SetRequestHeader("Cache-Control", "no-cache");
            request.SetRequestHeader("Connection", "keep-alive");

            // Start the request
            var operation = request.SendWebRequest();

            StringBuilder lineBuffer = new StringBuilder();
            int lastProcessedLength = 0;
            bool connectionEstablished = false;

            // Keep streaming until we stop listening or encounter an error
            while (_isListening && this != null)
            {
                // Wait for connection to be established
                if (!connectionEstablished && !operation.isDone)
                {
                    await Awaitable.WaitForSecondsAsync(0.05f);

                    // Check if connection failed
                    if (request.result == UnityWebRequest.Result.ConnectionError ||
                        request.result == UnityWebRequest.Result.ProtocolError)
                    {
                        throw new Exception($"Connection failed: {request.error}");
                    }

                    // Check if we have any data (connection established)
                    if (request.downloadHandler?.text?.Length > 0)
                    {
                        connectionEstablished = true;
                        Debug.Log("Streaming connection established");
                    }

                    continue;
                }

                connectionEstablished = true;

                // Check for new data
                var downloadHandler = request.downloadHandler;
                if (downloadHandler != null && downloadHandler.text.Length > lastProcessedLength)
                {
                    Debug.Log($"Received new data: {downloadHandler.text}");
                    string newData = downloadHandler.text.Substring(lastProcessedLength);
                    lastProcessedLength = downloadHandler.text.Length;

                    ProcessNewData(newData, lineBuffer);
                }

                // Check for connection errors during streaming
                if (downloadHandler.error != null)
                {
                    throw new Exception($"Streaming connection lost: {downloadHandler.error}");
                }

                // Small delay to prevent excessive polling
                await Awaitable.WaitForSecondsAsync(0.05f);
            }

            Debug.Log("Streaming loop ended");
        }

        private void ProcessNewData(string newData, StringBuilder lineBuffer)
        {
            for (int i = 0; i < newData.Length; i++)
            {
                char c = newData[i];

                if (c == '\n')
                {
                    // Process complete line
                    if (lineBuffer.Length > 0)
                    {
                        ProcessLine(lineBuffer.ToString());
                        lineBuffer.Clear();
                    }
                }
                else if (c != '\r') // Skip carriage returns
                {
                    lineBuffer.Append(c);
                }
            }
        }

        private void ProcessLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return;

            try
            {
                NpcApiChatResponse response =
                    JsonConvert.DeserializeObject<NpcApiChatResponse>(line, JsonSerializerSettings);

                if (response?.npc_id != null && _responseEvents.ContainsKey(response.npc_id))
                {
                    Debug.Log($"Received response from NPC {response.npc_id}: {response.message}");
                    _responseEvents[response.npc_id]?.Invoke(response);

                    // Reset reconnect attempts on successful message
                    _reconnectAttempts = 0;
                }
                else if (response?.npc_id != null)
                {
                    Debug.LogWarning($"Received response for unregistered NPC: {response.npc_id}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to parse response line '{line}': {e.Message}");
            }
        }

        private async Awaitable HandleReconnectionAsync()
        {
            _reconnectAttempts++;

            if (_reconnectAttempts > _maxReconnectAttempts)
            {
                Debug.LogError($"Max reconnection attempts ({_maxReconnectAttempts}) reached. Stopping listener.");
                _isListening = false;
                return;
            }

            Debug.Log(
                $"Reconnection attempt {_reconnectAttempts}/{_maxReconnectAttempts} in {_reconnectDelay} seconds...");
            await Awaitable.WaitForSecondsAsync(_reconnectDelay);
        }

        private void OnDestroy()
        {
            StopListening();
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                StopListening();
            }
            else if (!string.IsNullOrEmpty(apiKey))
            {
                StartListening();
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                StopListening();
            }
            else if (!string.IsNullOrEmpty(apiKey))
            {
                StartListening();
            }
        }
    }
}