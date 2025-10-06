using UnityEditor;
using UnityEngine;

namespace player2_sdk
{
    public class Menu : MonoBehaviour
    {
        [MenuItem("Player2/Publish")]
        private static void OpenWebsite()
        {
            var targetObject = GameObject.Find("NpcManager");

            if (targetObject != null)
            {
                // Get a component and read its value
                var component = targetObject.GetComponent<NpcManager>();
                if (component != null)
                {
                    var clientId = component.clientId; // Access the field
                    Application.OpenURL($"https://player2.game/profile/developer/{clientId}");
                }
                else
                {
                    Debug.LogError("MyComponent not found on GameObject");
                }
            }
            else
            {
                Debug.LogError("GameObject 'MyObjectName' not found in scene");
            }
        }
        
        
        
        
        [CustomEditor(typeof(NpcManager))]
        public class MyComponentEditor : Editor
        {
            public override void OnInspectorGUI()
            {
                NpcManager manager = (NpcManager)target;
                
                // Draw all the default fields
                DrawDefaultInspector();
        
                // Add spacing
                EditorGUILayout.Space(10);
        
                // Add button
                if (GUILayout.Button("Publish to Player2"))
                {
                    if (manager != null)
                    {
                        var clientId = manager.clientId; // Access the field
                        Application.OpenURL($"https://player2.game/profile/developer/{clientId}");
                    }
                }
            }
        }
    }
}
