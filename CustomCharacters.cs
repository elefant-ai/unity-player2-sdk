using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;

namespace player2_sdk
{
    /// <summary>
    ///     Attribute to mark a string field as a TTS voice selector
    /// </summary>
    public class RemoveIfCustomNpcAttribute : PropertyAttribute
    {
    }


#if UNITY_EDITOR

    /// <summary>
    ///     Custom property drawer for TTS voice selection
    /// </summary>
    [CustomPropertyDrawer(typeof(RemoveIfCustomNpcAttribute))]
    public class RemoveIfCustomNpcDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var targetObject = property.serializedObject.targetObject;

            var customNpc = targetObject is Player2Npc myScript && myScript.customNpc;
            // Cast to your specific type
            if (customNpc) label.tooltip = "Disabled because 'Custom NPC' is enabled.";
            EditorGUI.BeginDisabledGroup(customNpc);
            EditorGUI.PropertyField(position, property, label, true);
            EditorGUI.EndDisabledGroup();
        }
    }

    public class CustomNpcChecker : PropertyAttribute
    {
    }

    [CustomPropertyDrawer(typeof(CustomNpcChecker))]
    public class CustomNpcCheckerDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var targetObject = property.serializedObject.targetObject;
            if (targetObject is not Player2Npc)
            {
                EditorGUI.LabelField(position, "Error: CustomNpcChecker can only be used on Player2Npc.");
                return;
            }

            var player2Npc = (Player2Npc)targetObject;

            var customNpc = player2Npc.customNpc;
            // Cast to your specific type
            if (customNpc)
            {
                var height = GetPropertyHeight(property, label);

                EditorGUI.PropertyField(position, property, label);
            }
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var targetObject = property.serializedObject.targetObject;

            var customNpc = targetObject is Player2Npc myScript && myScript.customNpc;
            if (customNpc) return EditorGUIUtility.singleLineHeight * 5; // Height for the help box
            return 0; // No height when not showing the help box
        }
    }
#endif


    [Serializable]
    public class CharacterMeta
    {
        public string type;
        public string skin_url;
    }

    [Serializable]
    public class Character
    {
        public string id;
        public string name;
        public string short_name;
        public string greeting;
        public string description;
        public List<string> voice_ids;
        public CharacterMeta meta;
    }

    [Serializable]
    public class SelectedCharactersResponse
    {
        public List<Character> characters;
    }


    public class CustomCharacters : MonoBehaviour
    {
        [Header("API Configuration")] [SerializeField]
        private float fetchInterval = 15f; // 15 seconds

        [Header("NPC Spawning")] [SerializeField]
        private NpcManager npcManager;


        [Header("Debug")] [SerializeField] private bool enableDebugLogs = true;

        public UnityEvent<Character, string> OnNpcSpawned; // Character, NPC ID


        public UnityEvent<Character, UnityEvent<Character, string>> OnNewCustomCharacter = new();

        // Persistent NPC tracking - never clear these
        private readonly Dictionary<string, string> spawnedNpcIds = new(); // Character ID -> NPC ID

        private List<Character> cachedCharacters;
        private Coroutine fetchCoroutine;
        private bool isInitialized;


        public List<Character> Characters => cachedCharacters ?? new List<Character>();
        public bool IsDataReady => cachedCharacters != null && cachedCharacters.Count > 0;


        private void Start()
        {
            npcManager.apiTokenReady.AddListener(async () => { await InitializeCharacterFetching(); });
        }

        // Public access to the name -> ID mapping

        public event Action<List<Character>> OnCharactersUpdated;
        public event Action<string> OnFetchError;


        private async Awaitable InitializeCharacterFetching()
        {
            if (isInitialized) return;

            isInitialized = true;
            cachedCharacters = new List<Character>();

            if (enableDebugLogs)
                Debug.Log("SelectedCharacters: Starting periodic character fetching");


            // Start the periodic fetching coroutine
            await PeriodicFetchCoroutine();
        }

        private async Awaitable PeriodicFetchCoroutine()
        {
            while (true)
            {
                var url = $"{npcManager.GetBaseUrl()}/selected_characters";

                if (enableDebugLogs)
                    Debug.Log($"SelectedCharacters: Fetching characters from {url}");

                using (var request = UnityWebRequest.Get(url))
                {
                    request.timeout = 10; // 10 second timeout
                    request.SetRequestHeader("Accept", "application/json");
                    request.SetRequestHeader("Authorization", $"Bearer {npcManager.ApiKey}");


                    await request.SendWebRequest();

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        var jsonResponse = request.downloadHandler.text;

                        if (enableDebugLogs)
                            Debug.Log($"SelectedCharacters: Received response: {jsonResponse}");

                        var response = JsonConvert.DeserializeObject<SelectedCharactersResponse>(jsonResponse);

                        if (response?.characters != null)
                        {
                            foreach (var character in response.characters)
                            {
                                if (cachedCharacters.Contains(character)) continue;
                                cachedCharacters.Add(character);
                            }

                            if (enableDebugLogs)
                                Debug.Log(
                                    $"SelectedCharacters: Successfully cached {cachedCharacters.Count} characters");


                            // Spawn NPCs for new characters
                            SpawnNewCharacterNpcs();

                            OnCharactersUpdated?.Invoke(cachedCharacters);
                        }
                        else
                        {
                            var error = "Invalid response format: missing characters array";
                            if (enableDebugLogs)
                                Debug.LogWarning($"SelectedCharacters: {error}");
                            OnFetchError?.Invoke(error);
                        }
                    }
                    else
                    {
                        var error = $"HTTP request failed: {request.result} - {request.error}";
                        if (enableDebugLogs)
                            Debug.LogError($"SelectedCharacters: {error}");
                        OnFetchError?.Invoke(error);
                    }
                }

                await Awaitable.WaitForSecondsAsync(5f);
            }
        }


        public Character GetCharacterById(string id)
        {
            return cachedCharacters?.Find(c => c.id == id);
        }

        public Character GetCharacterByName(string name)
        {
            return cachedCharacters?.Find(c => c.short_name.Equals(name, StringComparison.InvariantCultureIgnoreCase));
        }


        private void SpawnNewCharacterNpcs()
        {
            if (npcManager == null)
            {
                if (enableDebugLogs)
                    Debug.LogWarning("SelectedCharacters: No NpcManager assigned, skipping NPC spawning");
                return;
            }

            if (cachedCharacters == null || cachedCharacters.Count == 0)
                return;


            foreach (var character in cachedCharacters)
            {
                // Skip if we already spawned an NPC for this character
                if (spawnedNpcIds.ContainsKey(character.id)) continue;
                var newEvent = new UnityEvent<Character, string>();


                newEvent.AddListener((character, npcId) =>
                {
                    spawnedNpcIds.Add(character.id, npcId);
                    OnNpcSpawned.Invoke(character, npcId);
                });
                OnNewCustomCharacter.Invoke(character, newEvent);
            }
        }


        public string GetNpcIdForCharacter(Character character)
        {
            return spawnedNpcIds.TryGetValue(character.id, out var npcId) ? npcId : null;
        }
    }
}