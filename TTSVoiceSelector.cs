#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
using UnityEditor;
using Newtonsoft.Json;

namespace player2_sdk
{
    /// <summary>
    /// Data structure for TTS voice information from the API
    /// </summary>
    [Serializable]
    public class TTSVoice
    {
        public string gender;
        public string id;
        public string language;
        public string name;

        public string DisplayName => $"{name} ({language}, {gender})";
    }

    /// <summary>
    /// Response structure for the voices API endpoint
    /// </summary>
    [Serializable]
    public class TTSVoicesResponse
    {
        public List<TTSVoice> voices;
    }

    /// <summary>
    /// Attribute to mark a string field as a TTS voice selector
    /// </summary>
    public class TTSVoiceAttribute : PropertyAttribute
    {
        public TTSVoiceAttribute() { }
    }

    /// <summary>
    /// Manager for fetching and caching TTS voices in the Unity Editor
    /// </summary>
    public static class TTSVoiceManager
    {
        private static TTSVoicesResponse _cachedVoices;
        private static float _lastFetchTime = -1f;
        private const float CACHE_DURATION = 300f; // 5 minutes cache
        private const string VOICES_ENDPOINT = "http://localhost:4315/v1/tts/voices";

        private static bool _isFetching = false;
        private static string _fetchError = null;

        public static TTSVoicesResponse CachedVoices => _cachedVoices;
        public static bool IsFetching => _isFetching;
        public static string FetchError => _fetchError;
        public static bool HasValidCache => _cachedVoices != null &&
                                           (Time.realtimeSinceStartup - _lastFetchTime) < CACHE_DURATION;

        /// <summary>
        /// Fetch voices from the API (async for Editor coroutines)
        /// </summary>
        public static void FetchVoices(Action<TTSVoicesResponse> onComplete = null, bool forceRefresh = false)
        {
            // Don't fetch if already fetching
            if (_isFetching) return;

            // Use cache if valid and not forcing refresh
            if (!forceRefresh && HasValidCache)
            {
                onComplete?.Invoke(_cachedVoices);
                return;
            }

            _isFetching = true;
            _fetchError = null;

            EditorCoroutineUtility.StartCoroutine(FetchVoicesCoroutine(onComplete));
        }

        private static System.Collections.IEnumerator FetchVoicesCoroutine(Action<TTSVoicesResponse> onComplete)
        {
            using (UnityWebRequest request = UnityWebRequest.Get(VOICES_ENDPOINT))
            {
                request.timeout = 5; // 5 second timeout
                request.SetRequestHeader("Accept", "application/json");

                yield return request.SendWebRequest();

                _isFetching = false;

                if (request.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        string json = request.downloadHandler.text;
                        _cachedVoices = JsonConvert.DeserializeObject<TTSVoicesResponse>(json);
                        _lastFetchTime = Time.realtimeSinceStartup;
                        _fetchError = null;

                        Debug.Log($"TTSVoiceManager: Successfully fetched {_cachedVoices?.voices?.Count ?? 0} voices");

                        onComplete?.Invoke(_cachedVoices);
                    }
                    catch (Exception ex)
                    {
                        _fetchError = $"Failed to parse voices response: {ex.Message}";
                        Debug.LogError($"TTSVoiceManager: {_fetchError}");
                        onComplete?.Invoke(null);
                    }
                }
                else
                {
                    _fetchError = $"Failed to fetch voices: {request.error}";
                    Debug.LogWarning($"TTSVoiceManager: {_fetchError} (Is Player2 App running on localhost:4315?)");
                    onComplete?.Invoke(null);
                }
            }
        }

        /// <summary>
        /// Clear the cache (useful for forcing a refresh)
        /// </summary>
        public static void ClearCache()
        {
            _cachedVoices = null;
            _lastFetchTime = -1f;
            _fetchError = null;
        }
    }

    /// <summary>
    /// Custom property drawer for TTS voice selection
    /// </summary>
    [CustomPropertyDrawer(typeof(TTSVoiceAttribute))]
    public class TTSVoicePropertyDrawer : PropertyDrawer
    {
        private const float BUTTON_WIDTH = 60f;
        private const float REFRESH_BUTTON_WIDTH = 25f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            string s = property.stringValue.Clone() as string;
            
            if (property.propertyType != SerializedPropertyType.String )
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            if ((TTSVoiceManager.CachedVoices?.voices.Count ?? 0) == 0 && !TTSVoiceManager.IsFetching)
            {
                if (string.IsNullOrEmpty(property.stringValue))
                {
                    property.stringValue = EditorGUI.TextField(position, label.text, "");
                }
                else
                {
                    property.stringValue = EditorGUI.TextField(position, label.text, property.stringValue);
                }
                return;
            }
           

            EditorGUI.BeginProperty(position, label, property);

            // Calculate rects
            Rect labelRect = new Rect(position.x, position.y, EditorGUIUtility.labelWidth, position.height);
            Rect dropdownRect = new Rect(
                position.x + EditorGUIUtility.labelWidth,
                position.y,
                position.width - EditorGUIUtility.labelWidth - BUTTON_WIDTH - REFRESH_BUTTON_WIDTH - 8f,
                position.height
            );
            Rect refreshButtonRect = new Rect(
                dropdownRect.x + dropdownRect.width + 4f,
                position.y,
                REFRESH_BUTTON_WIDTH,
                position.height
            );

            // Draw label
            EditorGUI.LabelField(labelRect, label);

            // Get current voice ID
            string currentVoiceId = property.stringValue;

            // Check if we have cached voices
            if (TTSVoiceManager.CachedVoices?.voices != null && TTSVoiceManager.CachedVoices.voices.Count > 0)
            {
                var voices = TTSVoiceManager.CachedVoices.voices;

                // Create display options (no custom option)
                string[] displayNames = new string[voices.Count];
                string[] voiceIds = new string[voices.Count];

                for (int i = 0; i < voices.Count; i++)
                {
                    displayNames[i] = voices[i].DisplayName;
                    voiceIds[i] = voices[i].id;
                }

                // Find current selection (default to first if not found)
                int selectedIndex = 0;
                bool foundMatch = false;
                for (int i = 0; i < voiceIds.Length; i++)
                {
                    if (voiceIds[i] == currentVoiceId)
                    {
                        selectedIndex = i;
                        foundMatch = true;
                        break;
                    }
                }

                // If no match found and we have voices, default to the first voice
                if (!foundMatch && voices.Count > 0)
                {
                    property.stringValue = voiceIds[0];
                    selectedIndex = 0;
                    property.serializedObject.ApplyModifiedProperties();
                }

                // Draw dropdown
                EditorGUI.BeginChangeCheck();
                int newIndex = EditorGUI.Popup(dropdownRect, selectedIndex, displayNames);
                if (EditorGUI.EndChangeCheck())
                {
                    if (newIndex >= 0 && newIndex < voiceIds.Length)
                    {
                        property.stringValue = voiceIds[newIndex];
                    }
                    property.serializedObject.ApplyModifiedProperties();
                }
            }
            else
            {
                // No voices cached, show disabled dropdown with placeholder text
                EditorGUI.BeginDisabledGroup(true);
                EditorGUI.Popup(dropdownRect, 0, new string[] { "No voices loaded - click Fetch" });
                TTSVoiceManager.FetchVoices();
                EditorGUI.EndDisabledGroup();
            }

        
            // Draw refresh button
            EditorGUI.BeginDisabledGroup(TTSVoiceManager.IsFetching);
            if (GUI.Button(refreshButtonRect, "↻"))
            {
                TTSVoiceManager.FetchVoices((voices) =>
                {
                    if (voices != null)
                    {
                        EditorUtility.SetDirty(property.serializedObject.targetObject);
                    }
                }, forceRefresh: true);
            }
            EditorGUI.EndDisabledGroup();

            // Show error message if there's one
            if (!string.IsNullOrEmpty(TTSVoiceManager.FetchError))
            {
                Rect helpBoxRect = new Rect(position.x, position.y + EditorGUIUtility.singleLineHeight + 2f, position.width, EditorGUIUtility.singleLineHeight * 2f);
                EditorGUI.HelpBox(helpBoxRect, TTSVoiceManager.FetchError, MessageType.Warning);
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float height = EditorGUIUtility.singleLineHeight;

            // Add space for error message
            if (!string.IsNullOrEmpty(TTSVoiceManager.FetchError))
            {
                height += EditorGUIUtility.singleLineHeight * 2f + 2f;
            }

            return height;
        }
    }

    /// <summary>
    /// Simple coroutine utility for the Editor
    /// </summary>
    public static class EditorCoroutineUtility
    {
        private class EditorCoroutine
        {
            public System.Collections.IEnumerator Routine;
            public System.DateTime LastUpdate;
        }

        private static List<EditorCoroutine> _coroutines = new List<EditorCoroutine>();

        static EditorCoroutineUtility()
        {
            EditorApplication.update += Update;
        }

        public static void StartCoroutine(System.Collections.IEnumerator routine)
        {
            _coroutines.Add(new EditorCoroutine { Routine = routine, LastUpdate = System.DateTime.Now });
        }

        private static void Update()
        {
            for (int i = _coroutines.Count - 1; i >= 0; i--)
            {
                var coroutine = _coroutines[i];

                // Handle UnityWebRequest properly
                if (coroutine.Routine.Current is UnityWebRequestAsyncOperation asyncOp)
                {
                    if (!asyncOp.isDone)
                        continue;
                }

                if (!coroutine.Routine.MoveNext())
                {
                    _coroutines.RemoveAt(i);
                }
            }
        }
    }
}
#endif
