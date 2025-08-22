using UnityEditor;

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
        [Tooltip("If true, the NPCs will use Text-to-Speech (TTS) to speak their responses. Requires a valid voice_id in the SpawnNpc configuration.")]
        public bool TTS = false;
        
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

            return serializableFunctions;
        }


        private const string BaseUrl = "https://api.player2.game/v1";
        

        public string GetBaseUrl()
        {
            return BaseUrl;
        }

        private void Awake()
        {
            PlayerSettings.insecureHttpOption = InsecureHttpOption.AlwaysAllowed;
            if (string.IsNullOrEmpty(clientId))
            {
                Debug.LogError("NpcManager requires a Client ID to be set.", this);
                return;
            }
            
            // Add the component and configure it
            _responseListener = gameObject.AddComponent<Player2NpcResponseListener>();
            _responseListener.JsonSerializerSettings = JsonSerializerSettings;
            _responseListener.newApiKey = NewApiKey;

            NewApiKey.AddListener((apiKey) =>
            {
                var start = this.apiKey == null;
                this.apiKey = apiKey;
                if (start)
                {
                    spawnNpcs.Invoke();
                }
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

            Debug.Log($"Registering NPC with ID: {id}");

            var onNpcApiResponse = new UnityEvent<NpcApiChatResponse>();
            onNpcApiResponse.AddListener((response) =>
            {
                if (response != null)
                {
                    if (!string.IsNullOrEmpty(response.message))
                    {
                        Debug.Log($"Updating UI for NPC {id}: {response.message}");
                        onNpcResponse.text = response.message;
                    }


                    if (response.command != null)
                    {
                        foreach (var functionCall in response.command)
                        {
                            functionHandler.Invoke(functionCall.ToFunctionCall(npcObject));
                        }
                    }
                }
            });

            _responseListener.RegisterNpc(id, onNpcApiResponse);

            // Ensure listener is running after registering
            if (!_responseListener.IsListening)
            {
                Debug.Log("Listener was not running, starting it now");
                _responseListener.StartListening();
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