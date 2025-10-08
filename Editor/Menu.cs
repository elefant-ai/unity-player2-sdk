using UnityEditor;
using UnityEngine;

namespace player2_sdk.Editor
{
    public class Menu : MonoBehaviour
    {
        private string buildPath = "Builds/WebGL";

        [MenuItem("Player2/Game Overview")]
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
    }
}