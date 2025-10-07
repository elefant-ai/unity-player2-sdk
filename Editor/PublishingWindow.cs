using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Policy;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Unity.Collections;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.UI;
using CompressionLevel = System.IO.Compression.CompressionLevel;

namespace player2_sdk.Editor
{
    public class PublishingWindow : EditorWindow
    {
        private string buildPath = "Builds/WebGL";


        [Serializable]
        class SerializedGameSettings
        {
            public string name = "Game Name";
            public string description = "Game Description";
            [CanBeNull] public string videoUrl = null;
            public Dictionary<string,string> downloadLinks = new Dictionary<string, string>();
            public string playNowUrl = "";
            private string gameCardAssetId = "";
            private string webGameAssetId = "";
            public string[] tags = Array.Empty<string>();
            private WebGameOptions webGameOptions = new WebGameOptions();
            class WebGameOptions
            {
                private int viewportWidth = 0;
                private int viewportHeight = 0;
                private bool supportsSharedArrayBuffer = false;
            }
        }
        
        
        private void OnEnable()
        {
            name = EditorPrefs.GetString("Player2_GameName", "");
            gameDescription = EditorPrefs.GetString("Player2_GameDescription", "");
            videoLink = EditorPrefs.GetString("Player2_VideoLink", "");
            var dlString = EditorPrefs.GetString("Player2_DownloadLinks", "");
            if (!string.IsNullOrEmpty(dlString))   
                downloadLinks = dlString.Split('\n').Select(s =>
                {
                    var parts = s.Split(':');
                    if (parts.Length == 2)
                        return (parts[0], parts[1]);
                    return ("", "");
                }).ToArray();
        }
        private void SaveData()
        {
            EditorPrefs.SetString("Player2_GameName", name);
            EditorPrefs.SetString("Player2_GameDescription", gameDescription);
            EditorPrefs.SetString("Player2_VideoLink", videoLink);
            EditorPrefs.SetString("Player2_DownloadLinks", string.Join("\n", downloadLinks.Select(dl => $"{dl.Item1}:{dl.Item2}")));
        }
        
        private string gameDescription;
        private int numberInput;
        private (string,string)[] downloadLinks = Array.Empty<(string,string)>();
        private string videoLink;
        
        private Texture2D cover;
        private GameObject objectReference;
        
        
        [MenuItem("Player2/Publish")]
        public static void ShowWindow()
        {
            // Creates or focuses the window
            GetWindow<PublishingWindow>("Publish to Player2");
        }
        private void OnGUI()
        {
            GUILayout.Label("Game Settings", EditorStyles.boldLabel);

            name = EditorGUILayout.TextField("Game Name", name);
            EditorGUILayout.Space(10);
            GUILayout.Label("Enter a description of your game (Markdown format)", EditorStyles.label);
            gameDescription = EditorGUILayout.TextArea(gameDescription, GUILayout.Height(60));
            
            // New: youtube video link input

            EditorGUILayout.Space(10);

            
            
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField("Play/Download Links", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Name", GUILayout.MaxWidth(200));
            EditorGUILayout.Space(20, expand: false);
            EditorGUILayout.LabelField("Link");
            EditorGUILayout.EndHorizontal();

            if (downloadLinks.Length == 0)
            {
                downloadLinks = downloadLinks.Append(("", "")).ToArray();
            }
            for (int i = 0; i < downloadLinks.Length; i++)
            {
                EditorGUILayout.BeginHorizontal();

                var (key, value) = downloadLinks[i];

                var k = GUILayout.TextField(key, GUILayout.MaxWidth(200));
                EditorGUILayout.Space(20, expand: false);

                var val = GUILayout.TextField(value, GUILayout.MaxWidth(200));
                downloadLinks[i] = (k, val);
                EditorGUILayout.EndHorizontal();

                // Show URL validation error for link value (if non-empty and invalid)
                if (!IsValidUrl(val))
                {
                    EditorGUILayout.HelpBox($"Invalid URL for '{(string.IsNullOrEmpty(k) ? "Link" : k)}'. Use a full http/https URL.", MessageType.Error);
                }
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Add link", GUILayout.MaxWidth(200)))
            {
                downloadLinks = downloadLinks.Append(("", "")).ToArray();
            }
            EditorGUILayout.Space(20, expand: false);

            if (GUILayout.Button("Remove link", GUILayout.MaxWidth(200)))
            {
                if (downloadLinks.Length > 0)
                    downloadLinks = downloadLinks.Take(downloadLinks.Length - 1).ToArray();
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            
            

            videoLink = EditorGUILayout.TextField("Youtube video link", videoLink);
            if (!IsValidUrl(videoLink))
            {
                EditorGUILayout.HelpBox("Invalid URL. Use a full http/https URL.", MessageType.Error);
            }

            EditorGUILayout.Space(20);
        
            if (GUILayout.Button("Submit"))
            {
                // ReSharper disable once Unity.PerformanceCriticalCodeInvocation
                BuildWebGL();
                
                var baseUrl = "http://localhost:3000/profile/developer/01999b45-cf44-723a-bddf-736840b49478/upload";
                var uriBuilder = new UriBuilder(baseUrl);
                var encodedName = Uri.EscapeDataString(name ?? "");
                var encodedDescription = Uri.EscapeDataString(gameDescription ?? "");
                var downloadDictionary = downloadLinks
                    .Where(dl => !string.IsNullOrEmpty(dl.Item1) && !string.IsNullOrEmpty(dl.Item2))
                    .ToDictionary(dl => Uri.EscapeDataString(dl.Item1), dl => Uri.EscapeDataString(dl.Item2));
                var encodedDownloadLinks = JsonConvert.SerializeObject(downloadDictionary);
                var encodedVideoLink = Uri.EscapeDataString(videoLink ?? "");
                var query = "";
                if (!string.IsNullOrEmpty(name)) query += $"name={encodedName}&";
                if (!string.IsNullOrEmpty(gameDescription)) query += $"description={encodedDescription}&";
                if (downloadDictionary.Count > 0) query += $"downloadLinks={encodedDownloadLinks}&";
                if (!string.IsNullOrEmpty(videoLink)) query += $"videoUrl={encodedVideoLink}&";
                if (query.EndsWith("&")) query = query[..^1];
                uriBuilder.Query = query;
                Application.OpenURL(uriBuilder.Uri.AbsoluteUri);
                
            }
            if (GUI.changed)
            {
                SaveData();
            }
        }

        // URL validation: accept only absolute http/https URLs; empty values are treated as valid.
        private static bool IsValidUrl(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return true;
            if (Uri.TryCreate(s, UriKind.Absolute, out var uri))
                return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
            return false;
        }

        void BuildWebGL()
        {
            BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions();
            buildPlayerOptions.scenes = GetScenePaths();
            buildPlayerOptions.locationPathName = buildPath;
            buildPlayerOptions.target = BuildTarget.WebGL;
            buildPlayerOptions.options = BuildOptions.None;

            BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log("Build succeeded: " + summary.totalSize + " bytes");
                EditorUtility.RevealInFinder(buildPath);

                // Create Builds/WebGL.zip containing all contents of Builds/WebGL/*
                try
                {
                    var buildsDir = Path.GetDirectoryName(buildPath);
                    if (string.IsNullOrEmpty(buildsDir)) buildsDir = "Builds";
                    Directory.CreateDirectory(buildsDir);

                    var zipPath = Path.Combine(buildsDir, "WebGL.zip");
                    if (File.Exists(zipPath)) File.Delete(zipPath);

                    ZipFile.CreateFromDirectory(buildPath, zipPath, CompressionLevel.Optimal, includeBaseDirectory: false);
                    Debug.Log($"Zipped WebGL build to: {zipPath}");
                }
                catch (Exception e)
                {
                    Debug.LogError($"Failed to zip WebGL build: {e}");
                }
            }
            else if (summary.result == BuildResult.Failed)
            {
                Debug.Log("Build failed");
            }
        }

        string[] GetScenePaths()
        {
            string[] scenes = new string[EditorBuildSettings.scenes.Length];
            for (int i = 0; i < scenes.Length; i++)
            {
                scenes[i] = EditorBuildSettings.scenes[i].path;
            }
            return scenes;
        }
    }
}