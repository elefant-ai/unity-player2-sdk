namespace player2_sdk
{
    using System;
    using UnityEngine;
    using Newtonsoft.Json.Linq;

    [Serializable]
    public class ExampleFunctionHandler: MonoBehaviour
    {
        public void HandleFunctionCall(FunctionCall functionCall)
        {
            Debug.Log($"Handling function call: {functionCall.name}");

            // Example: Handle a flame function call
            if (functionCall.name == "flame")
            {
                // Access arguments from the JObject
                if (functionCall.arguments.TryGetValue("radius", out JToken radiusToken))
                {
                    float radius = radiusToken.Value<float>();
                    SpawnFlameCloud(radius);
                }
                else
                {
                    // Use default value if argument not provided
                    SpawnFlameCloud(3f);
                }
            }
            else if (functionCall.name == "teleport")
            {
                // Handle teleport function with multiple arguments
                if (functionCall.arguments.TryGetValue("x", out JToken xToken) &&
                    functionCall.arguments.TryGetValue("y", out JToken yToken) &&
                    functionCall.arguments.TryGetValue("z", out JToken zToken))
                {
                    Vector3 position = new Vector3(
                        xToken.Value<float>(),
                        yToken.Value<float>(),
                        zToken.Value<float>()
                    );
                    TeleportNpc(functionCall.aiObject, position);
                }
            }
            else if (functionCall.name == "spawn_item")
            {
                // Handle spawn_item function with string and numeric arguments
                string itemName = functionCall.arguments["item_name"]?.Value<string>() ?? "default_item";
                int quantity = functionCall.arguments["quantity"]?.Value<int>() ?? 1;
                SpawnItem(itemName, quantity, functionCall.aiObject.transform.position);
            }
            else
            {
                // Log unknown function calls for debugging
                Debug.LogWarning($"Unknown function call: {functionCall.name}");

                // Log all arguments for debugging
                foreach (var arg in functionCall.arguments)
                {
                    Debug.Log($"Argument: {arg.Key} = {arg.Value}");
                }
            }
        }

        void SpawnFlameCloud(float radius)
        {
            // Your VFX / gameplay code here
            Debug.Log($"Spawning flame cloud with radius: {radius}");
        }

        void TeleportNpc(GameObject npc, Vector3 position)
        {
            // Teleport the NPC to the specified position
            npc.transform.position = position;
            Debug.Log($"Teleported NPC to: {position}");
        }

        void SpawnItem(string itemName, int quantity, Vector3 position)
        {
            // Spawn items in the game world
            for (int i = 0; i < quantity; i++)
            {
                Debug.Log($"Spawning {itemName} at {position}");
            }
        }
    }
}
