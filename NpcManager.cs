#if UNITY_EDITOR
using UnityEditor;
#endif

namespace player2_sdk
{
    using System;
    using System.Collections.Generic;
    using TMPro;
    using UnityEngine;
    using UnityEngine.Events;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Serialization;
    using UnityEngine.Serialization;

    [Serializable]
    public class Function
    {
        [Tooltip("The name of the function, used by the LLM to call this function, so try to keep it short and to the point")]
        public string name;
        [Tooltip("A short description of the function, used for explaining to the LLM what this function does")]
        public string description;
        public List<FunctionArgument> functionArguments;
        [Tooltip("If true, this function will never respond with a message when called")]
        public bool neverRespondWithMessage = false;

        public SerializableFunction ToSerializableFunction()
        {

            var props = new Dictionary<string, SerializedArguments>();

            for (int i = 0; i < functionArguments.Count; i++)
            {
                var arg = functionArguments[i];
                props[arg.argumentName] = new SerializedArguments
                {
                    type = arg.argumentType,
                    description = arg.argumentDescription
                };
            }

            Debug.Log(props);
            return new SerializableFunction
            {
                name = name,
                description = description,
                parameters = new Parameters
                {
                    Properties = props,
                    required = functionArguments.FindAll(arg => arg.required).ConvertAll(arg => arg.argumentName),
                },
                neverRespondWithMessage = neverRespondWithMessage
            };
        }
    }


    [Serializable]
    public class FunctionArgument
    {
        public string argumentName;
        public string argumentType;
        public string argumentDescription;
        public bool required;
    }



    public class NpcManager : MonoBehaviour
    {

        [Header("Config")]
        [SerializeField]
        [Tooltip("The Client ID is used to identify your game. It can be acquired from the Player2 Developer Dashboard")]
        public string clientId = null;

        [SerializeField]
        [Tooltip("If true, the NPCs will use Text-to-Speech (TTS) to speak their responses. Requires a valid voice_id in the tts.voice_ids configuration.")]
        public bool TTS = false;
        [SerializeField]
        [Tooltip("If true, the NPCs will keep track of game state information in the conversation history.")]
        public bool keep_game_state = false;

        private Player2NpcResponseListener _responseListener;

        [Header("Functions")] [SerializeField] public List<Function> functions;


        [SerializeField]
        [Tooltip("This event is triggered when a function call is received from the NPC. See the `ExampleFunctionHandler` script for how to handle these calls.")]
        public UnityEvent<FunctionCall> functionHandler;

        public readonly JsonSerializerSettings JsonSerializerSettings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            ContractResolver = new DefaultContractResolver
            {
                NamingStrategy = new SnakeCaseNamingStrategy(),
            }
        };

        public string apiKey = null;
        public UnityEvent spawnNpcs = new UnityEvent();
        public UnityEvent<string> NewApiKey = new UnityEvent<string>();
        public UnityEvent apiTokenReady = new UnityEvent();
        public List<SerializableFunction> GetSerializableFunctions()
        {
            var serializableFunctions = new List<SerializableFunction>();
            foreach (var function in functions)
            {
                serializableFunctions.Add(function.ToSerializableFunction());
            }
            if (serializableFunctions.Count > 0)
            {
                return serializableFunctions;
            }
            else
            {
                return null;
            }
        }

        private const string BaseUrl = "https://api.player2.game/v1";
        private const string BaseUrlPlayer2Game = "https://games.player2.game/_api/v1";

        public string GetBaseUrl()
        {
            // Check if we're running in WebGL and on player2.game domain
            if (IsWebGLAndOnPlayer2GameDomain())
            {
                return BaseUrlPlayer2Game;
            }
            return BaseUrl;
        }

        /// <summary>
        /// Check if authentication should be skipped (WebGL on player2.game domain)
        /// </summary>
        public bool ShouldSkipAuthentication()
        {
            return IsWebGLAndOnPlayer2GameDomain();
        }

        /// <summary>
        /// Check if we're running in WebGL and on player2.game domain
        /// </summary>
        private bool IsWebGLAndOnPlayer2GameDomain()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            try
            {
                string origin = GetWebGLOrigin();
                return !string.IsNullOrEmpty(origin) && origin.Contains("player2.game");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Failed to detect WebGL domain: {ex.Message}");
                return false;
            }
#else
            return false;
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern string GetWebGLOrigin();
#endif

        private void Awake()
        {
#if UNITY_EDITOR
            PlayerSettings.insecureHttpOption = InsecureHttpOption.AlwaysAllowed;
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
            // For WebGL builds, we'll handle certificate validation differently
            // This is set at runtime, not in PlayerSettings
#endif
            if (string.IsNullOrEmpty(clientId))
            {
                Debug.LogError("NpcManager requires a Client ID to be set.", this);
                return;
            }

            _responseListener = gameObject.GetComponent<Player2NpcResponseListener>();
            if (_responseListener == null)
            {
                Debug.LogError("Player2NpcResponseListener component not found on NPC Manager GameObject. Please attach it in the editor.", this);
                return;
            }

            _responseListener.JsonSerializerSettings = JsonSerializerSettings;
            _responseListener._baseUrl = GetBaseUrl();

            _responseListener.SetReconnectionSettings(5, 2.5f);

            NewApiKey.AddListener((apiKey) =>
            {
                Debug.Log($"NpcManager.NewApiKey listener: Received API key: {apiKey?.Substring(0, Math.Min(10, apiKey?.Length ?? 0)) ?? "null"} (Length: {apiKey?.Length ?? 0})");
                this.apiKey = apiKey;

                // For WebGL on player2.game domain, pass empty API key to skip auth headers
                string apiKeyForListener = ShouldSkipAuthentication() ? "" : apiKey;
                Debug.Log($"NpcManager.NewApiKey listener: Set this.apiKey to: {this.apiKey?.Substring(0, Math.Min(10, this.apiKey?.Length ?? 0)) ?? "null"}");
                Debug.Log($"NpcManager.NewApiKey listener: Passing to response listener: {(string.IsNullOrEmpty(apiKeyForListener) ? "empty (skipping auth)" : "API key")}");

                _responseListener.newApiKey.Invoke(apiKeyForListener);
                Debug.Log("NpcManager.NewApiKey listener: API key set, waiting for authentication completion");
            });

            // Listen for when the authentication system signals it's fully ready
            apiTokenReady.AddListener(() =>
            {
                Debug.Log("NpcManager.apiTokenReady listener: Authentication fully complete, spawning NPCs");
                spawnNpcs.Invoke();
                Debug.Log($"NpcManager.apiTokenReady listener: spawnNpcs invoked, API key length: {apiKey?.Length ?? 0}");
            });
            
            Debug.Log($"NpcManager initialized with clientId: {clientId}");
        }


        private void OnValidate()
        {
            if (string.IsNullOrEmpty(clientId))
            {
                Debug.LogError("NpcManager requires a Game ID to be set.", this);
            }
        }



        public void RegisterNpc(string id, TextMeshProUGUI onNpcResponse, GameObject npcObject)
        {
            if (_responseListener == null)
            {
                Debug.LogError("Response listener is null! Cannot register NPC.");
                return;
            }

            if (string.IsNullOrEmpty(id))
            {
                Debug.LogError("Cannot register NPC with empty ID");
                return;
            }

            bool uiAttached = onNpcResponse != null;
            if (!uiAttached)
            {
                Debug.LogWarning($"Registering NPC {id} without a TextMeshProUGUI target; responses will not display in UI.");
            }

            Debug.Log($"Registering NPC with ID: {id}");

            var onNpcApiResponse = new UnityEvent<NpcApiChatResponse>();
            onNpcApiResponse.AddListener(response => HandleNpcApiResponse(id, response, uiAttached, onNpcResponse, npcObject));

            _responseListener.RegisterNpc(id, onNpcApiResponse);

            // Ensure listener is running after registering
            if (!_responseListener.IsListening)
            {
                Debug.Log("Listener was not running, starting it now");
                _responseListener.StartListening();
            }
        }

        private void HandleNpcApiResponse(string id, NpcApiChatResponse response, bool uiAttached, TextMeshProUGUI onNpcResponse, GameObject npcObject)
        {
            try
            {
                if (response == null)
                {
                    Debug.LogWarning($"Received null response object for NPC {id}");
                    return;
                }

                if (npcObject == null)
                {
                    Debug.LogWarning($"NPC object is null for NPC {id}");
                    return;
                }

                if (!string.IsNullOrEmpty(response.message))
                {
                    if (uiAttached && onNpcResponse != null)
                    {
                        Debug.Log($"Updating UI for NPC {id}: {response.message}");
                        onNpcResponse.text = response.message;
                    }
                    else
                    {
                        Debug.Log($"(No UI) NPC {id} message: {response.message}");
                    }
                }

                // Handle audio playback if audio data is available
                if (response.audio != null && !string.IsNullOrEmpty(response.audio.data))
                {
                    // Log detailed audio data information for troubleshooting
                    string audioDataPreview = response.audio.data.Length > 100 
                        ? response.audio.data.Substring(0, 100) + "..." 
                        : response.audio.data;
                    Debug.Log($"NPC {id} - Audio data received: Length={response.audio.data.Length}, Preview={audioDataPreview}");
                    
                    // Validate audio data format
                    if (response.audio.data.StartsWith("data:"))
                    {
                        int commaIndex = response.audio.data.IndexOf(',');
                        if (commaIndex > 0)
                        {
                            string mimeType = response.audio.data.Substring(0, commaIndex);
                            string base64Data = response.audio.data.Substring(commaIndex + 1);
                            Debug.Log($"NPC {id} - Audio format: {mimeType}, Base64 length: {base64Data.Length}");
                        }
                        else
                        {
                            Debug.LogWarning($"NPC {id} - Invalid data URL format: no comma separator found");
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"NPC {id} - Audio data does not start with 'data:' prefix");
                    }

                    // Check if NPC GameObject has AudioSource, add if needed
                    var audioSource = npcObject.GetComponent<AudioSource>();
                    if (audioSource == null)
                    {
                        audioSource = npcObject.AddComponent<AudioSource>();
                    }

                    // Start coroutine to decode and play audio using platform-specific implementation
                    var audioPlayer = AudioPlayerFactory.GetAudioPlayer();
                    StartCoroutine(audioPlayer.PlayAudioFromDataUrl(response.audio.data, audioSource, id));
                }

                if (response.command == null || response.command.Count == 0)
                {
                    return;
                }

                foreach (var functionCall in response.command)
                {
                    try
                    {
                        var call = functionCall.ToFunctionCall(npcObject);
                        functionHandler?.Invoke(call);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Error invoking function call '{functionCall?.name}' for NPC {id}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Unhandled exception processing response for NPC {id}: {ex.Message}");
            }
        }


        public void UnregisterNpc(string id)
        {
            if (_responseListener != null)
            {
                _responseListener.UnregisterNpc(id);
            }
        }

        public bool IsListenerActive()
        {
            return _responseListener != null && _responseListener.IsListening;
        }

        public void StartListener()
        {
            if (_responseListener != null)
            {
                _responseListener.StartListening();
            }
        }

        public void StopListener()
        {
            if (_responseListener != null)
            {
                _responseListener.StopListening();
            }
        }

        private void OnDestroy()
        {
            if (_responseListener != null)
            {
                _responseListener.StopListening();
            }
        }

        // Add this method for debugging
        [ContextMenu("Debug Listener Status")]
        public void DebugListenerStatus()
        {
            if (_responseListener == null)
            {
                Debug.Log("Response listener is NULL");
            }
            else
            {
                Debug.Log(
                    $"Response listener status: IsListening={_responseListener.IsListening}");
            }
        }
    }

    [Serializable]
    public class SerializableFunction
    {
        public string name;
        public string description;
        public Parameters parameters;
        public bool neverRespondWithMessage;
    }

    [Serializable]
    public class Parameters
    {
        public Dictionary<string, SerializedArguments> Properties { get; set; }
        public List<string> required;
        public string type = "object";
    }

    [Serializable]
    public class SerializedArguments
    {
        public string type;
        public string description;
    }
}

