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

        public string GetBaseUrl()
        {
            return BaseUrl;
        }

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
            _responseListener._baseUrl = BaseUrl;

            _responseListener.SetReconnectionSettings(5, 2.5f);

            NewApiKey.AddListener((apiKey) =>
            {
                Debug.Log($"New API Key received: {apiKey?.Substring(0, Math.Min(10, apiKey?.Length ?? 0)) ?? "null"}");
                this.apiKey = apiKey;
                _responseListener.newApiKey.Invoke(apiKey);
                spawnNpcs.Invoke();
                Debug.Log($"NpcManager: API key set successfully. Length: {apiKey?.Length ?? 0}");
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
