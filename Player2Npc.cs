using System;
using System.Collections.Generic;
using System.Text;
using JetBrains.Annotations;
using Newtonsoft.Json;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;

namespace player2_sdk
{
    [Serializable]
    public class TTSInfo
    {
        public double speed = 1;
        public string audio_format = "mp3";
        public List<string> voice_ids;
    }

    [Serializable]
    public class SpawnNpc
    {
        public string short_name;
        public string name;
        public string character_description;
        public string system_prompt;
        public List<SerializableFunction> commands;
        public TTSInfo tts;
        public bool keep_game_state;
    }

    [Serializable]
    public class ChatRequest
    {
        public string sender_name;
        public string sender_message;
        [CanBeNull] public string game_state_info;
        [CanBeNull] public string tts; // Nullable by convention / attribute
    }

    public class Player2Npc : MonoBehaviour
    {
        [Header("State Config")] [SerializeField]
        private NpcManager npcManager;


        [Header("NPC Configuration")] [SerializeField]
        public bool customNpc;

        [RemoveIfCustomNpc] [SerializeField] private string shortName = "Victor";

        [RemoveIfCustomNpc] [SerializeField] private string fullName = "Victor J. Johnson";

        [RemoveIfCustomNpc]
        [Tooltip(
            "A description of the NPC, written in first person, used for the LLM to understand the character better.")]
        [SerializeField]
        private string characterDescription = "I am crazed scientist on the hunt for gold!";

        [Tooltip(
            "The system prompt should be written the third person, describing the NPC's personality and behavior.")]
        [SerializeField]
        private string systemPrompt = "Victor is a scientist obsessed with finding gold.";

        // Use the TTSVoice attribute in Editor for dropdown functionality

        [RemoveIfCustomNpc]
        [Tooltip("The voice ID to use for TTS. Click 'Fetch' to load available voices from Player2 App.")]
#if UNITY_EDITOR
        [TTSVoice]
#endif
        [SerializeField]
        public string voiceId;


        [Header("Events")] [SerializeField] public TMP_InputField inputField;

        [SerializeField] public TextMeshProUGUI outputMessage;

        [Header("Debugging")]
        [Tooltip(
            "Custom trace ID for request tracking. If set, this will be added as X-Player2-Trace-Id header to all requests.")]
        [SerializeField]
        private string customTraceId = "";
#if UNITY_EDITOR
        [CustomNpcChecker]
#endif
        private readonly UnityEvent<Character, string> OnChangedCustomCharacter = new();

#if UNITY_EDITOR
        [CustomNpcChecker]
#endif
        private readonly UnityEvent<Character, UnityEvent<Character, string>> OnNewCustomCharacter = new();

        private string _npcID;

        private async void Awake()
        {
            Debug.Log("Starting Player2Npc with NPC: " + fullName);
            if (npcManager == null)
            {
                Debug.LogError("Player2Npc requires an NpcManager reference. Please assign it in the inspector.", this);
                return;
            }


            if (customNpc)
            {
                OnNewCustomCharacter.AddListener(async (character, onReceivedCharacter) =>
                {
                    shortName = character.short_name;
                    fullName = character.name;
                    characterDescription = character.description;
                    voiceId = character.voice_ids.Count > 0 ? character.voice_ids[0] : voiceId;
                    if (!string.IsNullOrEmpty(npcManager.ApiKey))
                        await SpawnNpcAsync();
                    else
                        npcManager.NewApiKey.AddListener(async _ => { await SpawnNpcAsync(); });
                    onReceivedCharacter?.Invoke(character, _npcID);
                });
                OnChangedCustomCharacter.AddListener((character, npcId) =>
                {
                    shortName = character.short_name;
                    fullName = character.name;
                    characterDescription = character.description;
                    voiceId = character.voice_ids.Count > 0 ? character.voice_ids[0] : voiceId;
                    _npcID = npcId;
                    outputMessage.text = "";

                    Debug.Log($"Changed custom NPC to '{fullName}' with ID: {_npcID}");
                });
            }
            else
            {
                if (!string.IsNullOrEmpty(npcManager.ApiKey))
                    await SpawnNpcAsync();
                else
                    npcManager.spawnNpcs.AddListener(async () => { await SpawnNpcAsync(); });
            }

            if (inputField != null)
            {
                inputField.onEndEdit.AddListener(OnChatMessageSubmitted);
                inputField.onEndEdit.AddListener(_ => inputField.text = string.Empty);
            }
            else
            {
                Debug.LogWarning("InputField not assigned on Player2Npc; chat input disabled.", this);
            }

            // Auto-fetch voices in Editor on first load
#if UNITY_EDITOR
            AutoFetchVoicesInEditor();
#endif
        }

        /// <summary>
        ///     Sets a custom trace ID that will be included in all requests from this NPC
        /// </summary>
        public void SetCustomTraceId(string traceId)
        {
            customTraceId = traceId;
        }

        /// <summary>
        ///     Gets the current custom trace ID
        /// </summary>
        public string GetCustomTraceId()
        {
            return customTraceId;
        }

        private string _clientID()
        {
            return npcManager.clientId;
        }

#if UNITY_EDITOR
        private void AutoFetchVoicesInEditor()
        {
            // Only auto-fetch if we haven't cached voices yet
            if (TTSVoiceManager.CachedVoices == null && !TTSVoiceManager.IsFetching)
                TTSVoiceManager.FetchVoices(voices =>
                {
                    if (voices != null && voices.voices != null && voices.voices.Count > 0)
                    {
                        Debug.Log($"Player2Npc: Auto-fetched {voices.voices.Count} TTS voices");

                        // If the current voiceId is the default one and we have voices available,
                        // you could optionally set it to the first available voice
                        if (string.IsNullOrEmpty(voiceId) || voiceId == "01955d76-ed5b-7451-92d6-5ef579d3ed28")
                            voiceId = voices.voices[0].id;
                    }
                });
        }
#endif


        public void NewCustomCharacter(Character character, UnityEvent<Character, string> onReceivedCharacter)
        {
            OnNewCustomCharacter.Invoke(character, onReceivedCharacter);
        }

        public void ChangedCustomCharacter(Character character, string npcId)
        {
            Debug.Log(character.name);
            OnChangedCustomCharacter.Invoke(character, npcId);
        }

        private void OnChatMessageSubmitted(string message)
        {
            _ = SendChatMessageAsync(message);
        }

        private async Awaitable SpawnNpcAsync()
        {
            if (npcManager == null)
            {
                Debug.LogError("Player2Npc.SpawnNpcAsync called but npcManager is NOT assigned. Aborting spawn.");
                return;
            }

            // Ensure we have a valid API key before attempting to spawn (unless auth is bypassed for hosted scenarios)
            if (string.IsNullOrEmpty(npcManager.ApiKey) && !npcManager.ShouldSkipAuthentication())
            {
                Debug.LogError(
                    $"Cannot spawn NPC '{fullName}': No API key available. Please ensure authentication is completed first.");
                return;
            }

            if (npcManager.ShouldSkipAuthentication())
                Debug.Log($"Spawning NPC '{fullName}' in hosted mode (no API key required)");
            else
                Debug.Log($"Spawning NPC '{fullName}' with API key authentication");

            Debug.Log($"Spawning NPC '{fullName}' with voice ID: {voiceId}");

            var spawnData = new SpawnNpc
            {
                short_name = shortName,
                name = fullName,
                character_description = characterDescription,
                system_prompt = systemPrompt,
                commands = npcManager.GetSerializableFunctions(),
                tts = new TTSInfo
                {
                    speed = 1.0,
                    audio_format = "mp3",
                    voice_ids = new List<string> { voiceId }
                },
                keep_game_state = npcManager.keepGameState
            };

            var url = $"{npcManager.GetBaseUrl()}/npcs/spawn";
            Debug.Log($"Spawning NPC at URL: {url}");

            var json = JsonConvert.SerializeObject(spawnData, npcManager.JsonSerializerSettings);
            var bodyRaw = Encoding.UTF8.GetBytes(json);

            using var request = new UnityWebRequest(url, "POST");
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();

            // Skip authentication if running on player2.game domain (cookies will handle auth)
            if (!npcManager.ShouldSkipAuthentication())
            {
                Debug.Log("Setting Authorization header with API key");
                request.SetRequestHeader("Authorization", $"Bearer {npcManager.ApiKey}");
            }
            else
            {
                Debug.Log("Skipping Authorization header (WebGL on player2.game domain - using cookies for auth)");
            }

            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Accept", "application/json");

            if (!string.IsNullOrEmpty(customTraceId)) request.SetRequestHeader("X-Player2-Trace-Id", customTraceId);

            await request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                _npcID = request.downloadHandler.text.Trim('"');
                Debug.Log($"NPC spawned successfully with ID: {_npcID}");

                if (!string.IsNullOrEmpty(_npcID) && npcManager != null)
                    npcManager.RegisterNpc(_npcID, outputMessage, gameObject);
                else
                    Debug.LogError($"Invalid NPC ID or null npcManager: ID={_npcID}, Manager={npcManager}");
            }
            else
            {
                var traceId = request.GetResponseHeader("X-Player2-Trace-Id");
                var traceInfo = !string.IsNullOrEmpty(traceId) ? $" (X-Player2-Trace-Id: {traceId})" : "";
                var error =
                    $"Failed to spawn NPC: {request.error} - Response: {request.downloadHandler.text}{traceInfo}";
                Debug.LogError(error);
            }
        }

        private async Awaitable SendChatMessageAsync(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;

            try
            {
                Debug.Log("Sending message to NPC: " + message);

                if (string.IsNullOrEmpty(_npcID))
                {
                    Debug.LogWarning("NPC ID is not set! Cannot send message.");
                    return;
                }

                var chatRequest = new ChatRequest
                {
                    sender_name = fullName,
                    sender_message = message,
                    tts = null
                };

                await SendChatRequestAsync(chatRequest);
            }
            catch (OperationCanceledException)
            {
                Debug.Log("Chat message send operation was cancelled");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Unexpected error sending chat message: {ex.Message}");
            }
        }

        private async Awaitable SendChatRequestAsync(ChatRequest chatRequest)
        {
            if (npcManager == null)
            {
                Debug.LogError("Cannot send chat request because npcManager is null.");
                return;
            }

            // Ensure we have a valid API key before attempting to send chat (unless auth is bypassed for hosted scenarios)
            if (string.IsNullOrEmpty(npcManager.ApiKey) && !npcManager.ShouldSkipAuthentication())
            {
                Debug.LogError(
                    "Cannot send chat message: No API key available. Please ensure authentication is completed first.");
                return;
            }

            if (npcManager.TTS) chatRequest.tts = "server";
            var url = $"{npcManager.GetBaseUrl()}/npcs/{_npcID}/chat";
            var json = JsonConvert.SerializeObject(chatRequest, npcManager.JsonSerializerSettings);
            var bodyRaw = Encoding.UTF8.GetBytes(json);

            using var request = new UnityWebRequest(url, "POST");
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();

            // Skip authentication if running on player2.game domain (cookies will handle auth)
            if (!npcManager.ShouldSkipAuthentication())
                request.SetRequestHeader("Authorization", $"Bearer {npcManager.ApiKey}");

            request.SetRequestHeader("Content-Type", "application/json");

            if (!string.IsNullOrEmpty(customTraceId)) request.SetRequestHeader("X-Player2-Trace-Id", customTraceId);

            await request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"Message sent successfully to NPC {_npcID}");
            }
            else
            {
                var traceId = request.GetResponseHeader("X-Player2-Trace-Id");
                var traceInfo = !string.IsNullOrEmpty(traceId) ? $" (X-Player2-Trace-Id: {traceId})" : "";
                var error =
                    $"Failed to send message: {request.error} - Response: {request.downloadHandler.text}{traceInfo}";
                Debug.LogError(error);
            }
        }
    }
}