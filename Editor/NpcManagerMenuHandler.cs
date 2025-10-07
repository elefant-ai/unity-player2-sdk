using System;
using UnityEditor;
using UnityEngine;

namespace player2_sdk.Editor
{
    [CustomEditor(typeof(NpcManager))]
    public class NpcManagerMenuHandler : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var manager = (NpcManager)target;

            if (manager == null)
            {
                EditorGUILayout.LabelField("NpcManager not found.");
                return;
            }

            if (string.IsNullOrEmpty(manager.clientId) || !IsValidUuidV7(manager.clientId))
            {
                GUILayout.Label("Client ID is not set. Please enter your Client ID to enable publishing features.",
                    EditorStyles.wordWrappedLabel);
                EditorGUILayout.Space(10);
                if (GUILayout.Button("Create a new game on our website"))
                    Application.OpenURL("https://player2.game/profile/developer");
                EditorGUILayout.Space(10);
                GUILayout.Label("Enter your Client ID:");
                manager.clientId = GUILayout.TextField(manager.clientId);
                return;
            }

            DrawDefaultInspector();

            EditorGUILayout.Space(10);

            //if (GUILayout.Button("Publish to Player2")) PublishingWindow.ShowWindow();
        }


        public static bool IsValidUuidV7(string uuidString)
        {
            // Try to parse the UUID
            if (!Guid.TryParse(uuidString, out var uuid)) return false;

            // Get the byte array representation
            var bytes = uuid.ToByteArray();

            // Check version (should be 7)
            // Version is in bits 48-51 (byte 6, high nibble)
            var version = (bytes[7] >> 4) & 0x0F;
            if (version != 7) return false;

            // Check variant (should be RFC 4122, variant bits should be 10xx)
            // Variant is in bits 64-65 (byte 8, top 2 bits)
            var variant = (bytes[8] >> 6) & 0x03;
            if (variant != 0x02) // Binary 10
                return false;

            return true;
        }
    }
}