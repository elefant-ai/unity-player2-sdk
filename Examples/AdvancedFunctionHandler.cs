using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

namespace player2_sdk
{
    /// <summary>
    ///     Advanced example of function call handling with various patterns and best practices
    /// </summary>
    [Serializable]
    public class AdvancedFunctionHandler : MonoBehaviour
    {
        // Configuration
        [Header("Function Call Settings")] [SerializeField]
        private bool enableDebugLogging = true;

        [SerializeField] private float defaultTeleportDelay = 0.5f;

        // References for gameplay integration
        [Header("Gameplay References")] [SerializeField]
        private GameObject flameEffectPrefab;

        [SerializeField] private GameObject itemSpawnPoint;
        [SerializeField] private AudioClip teleportSound;
        [SerializeField] private AudioClip itemSpawnSound;

        // State tracking
        private Dictionary<string, int> functionCallCounts = new();
        private List<GameObject> spawnedObjects = new();

        #region Unity Lifecycle

        private void OnDestroy()
        {
            CleanupSpawnedObjects();
        }

        #endregion

        /// <summary>
        ///     Main function call handler - called by NpcManager when NPC executes a function
        /// </summary>
        public void HandleFunctionCall(FunctionCall functionCall)
        {
            if (enableDebugLogging)
            {
                Debug.Log($"[AdvancedFunctionHandler] Processing function call: {functionCall.name}");
                LogFunctionCallCount(functionCall.name);
            }

            try
            {
                switch (functionCall.name)
                {
                    case "flame":
                        HandleFlameFunction(functionCall);
                        break;

                    case "teleport":
                        HandleTeleportFunction(functionCall);
                        break;

                    case "spawn_item":
                        HandleSpawnItemFunction(functionCall);
                        break;

                    case "heal":
                        HandleHealFunction(functionCall);
                        break;

                    case "attack":
                        HandleAttackFunction(functionCall);
                        break;

                    case "quest_update":
                        HandleQuestUpdateFunction(functionCall);
                        break;

                    default:
                        HandleUnknownFunction(functionCall);
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    $"[AdvancedFunctionHandler] Error processing function '{functionCall.name}': {ex.Message}");
                // Could send error feedback to NPC here if needed
            }
        }

        #region Function Handlers

        private void HandleFlameFunction(FunctionCall functionCall)
        {
            // Extract parameters with defaults
            var radius = GetArgumentValue(functionCall.arguments, "radius", 3.0f);
            var duration = GetArgumentValue(functionCall.arguments, "duration", 2.0f);
            var color = GetArgumentValue(functionCall.arguments, "color", "red");

            if (enableDebugLogging)
                Debug.Log($"[Flame] Creating flame effect: radius={radius}, duration={duration}, color={color}");

            // Spawn flame effect
            SpawnFlameEffect(functionCall.aiObject.transform.position, radius, duration, color);
        }

        private void HandleTeleportFunction(FunctionCall functionCall)
        {
            // Extract position coordinates
            var x = GetArgumentValue(functionCall.arguments, "x", functionCall.aiObject.transform.position.x);
            var y = GetArgumentValue(functionCall.arguments, "y", functionCall.aiObject.transform.position.y);
            var z = GetArgumentValue(functionCall.arguments, "z", functionCall.aiObject.transform.position.z);
            var instant = GetArgumentValue(functionCall.arguments, "instant", false);

            var targetPosition = new Vector3(x, y, z);

            if (enableDebugLogging)
                Debug.Log(
                    $"[Teleport] Moving NPC from {functionCall.aiObject.transform.position} to {targetPosition}, instant={instant}");

            if (instant)
            {
                functionCall.aiObject.transform.position = targetPosition;
                PlayTeleportSound();
            }
            else
            {
                StartCoroutine(TeleportWithDelay(functionCall.aiObject, targetPosition));
            }
        }

        private void HandleSpawnItemFunction(FunctionCall functionCall)
        {
            var itemName = GetArgumentValue(functionCall.arguments, "item_name", "mystery_box");
            var quantity = GetArgumentValue(functionCall.arguments, "quantity", 1);
            var spawnRadius = GetArgumentValue(functionCall.arguments, "spawn_radius", 2.0f);

            var spawnCenter = itemSpawnPoint != null
                ? itemSpawnPoint.transform.position
                : functionCall.aiObject.transform.position;

            if (enableDebugLogging)
                Debug.Log(
                    $"[SpawnItem] Spawning {quantity}x {itemName} around {spawnCenter} within radius {spawnRadius}");

            for (var i = 0; i < quantity; i++)
            {
                var spawnPosition = spawnCenter + Random.insideUnitSphere * spawnRadius;
                spawnPosition.y = Mathf.Max(spawnPosition.y, 0); // Keep above ground

                SpawnItem(itemName, spawnPosition);
            }

            PlayItemSpawnSound();
        }

        private void HandleHealFunction(FunctionCall functionCall)
        {
            var healAmount = GetArgumentValue(functionCall.arguments, "amount", 25.0f);
            var targetType = GetArgumentValue(functionCall.arguments, "target", "self");

            if (enableDebugLogging) Debug.Log($"[Heal] Healing {targetType} for {healAmount} HP");

            // Implement healing logic here
            ApplyHealing(functionCall.aiObject, healAmount, targetType);

            // Visual feedback
            CreateHealEffect(functionCall.aiObject.transform.position);
        }

        private void HandleAttackFunction(FunctionCall functionCall)
        {
            var attackType = GetArgumentValue(functionCall.arguments, "type", "melee");
            var damage = GetArgumentValue(functionCall.arguments, "damage", 10.0f);
            var range = GetArgumentValue(functionCall.arguments, "range", 5.0f);

            if (enableDebugLogging) Debug.Log($"[Attack] {attackType} attack: damage={damage}, range={range}");

            PerformAttack(functionCall.aiObject, attackType, damage, range);
        }

        private void HandleQuestUpdateFunction(FunctionCall functionCall)
        {
            var questId = GetArgumentValue(functionCall.arguments, "quest_id", "");
            var updateType = GetArgumentValue(functionCall.arguments, "update_type", "progress");
            var progressAmount = GetArgumentValue(functionCall.arguments, "progress_amount", 1);

            if (enableDebugLogging)
                Debug.Log($"[QuestUpdate] Updating quest {questId}: {updateType} by {progressAmount}");

            UpdateQuestProgress(questId, updateType, progressAmount);
        }

        private void HandleUnknownFunction(FunctionCall functionCall)
        {
            Debug.LogWarning($"[AdvancedFunctionHandler] Unknown function: {functionCall.name}");

            // Log all arguments for debugging
            foreach (var arg in functionCall.arguments) Debug.Log($"  {arg.Key}: {arg.Value} ({arg.Value?.Type})");

            // Could implement fallback behavior or send error message to NPC
        }

        #endregion

        #region Helper Methods

        /// <summary>
        ///     Generic method to safely extract argument values from JObject with type conversion
        /// </summary>
        private T GetArgumentValue<T>(JObject arguments, string key, T defaultValue)
        {
            if (arguments.TryGetValue(key, out var token))
                try
                {
                    return token.Value<T>();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning(
                        $"[AdvancedFunctionHandler] Failed to convert argument '{key}' to {typeof(T).Name}: {ex.Message}");
                    return defaultValue;
                }

            if (enableDebugLogging)
                Debug.Log(
                    $"[AdvancedFunctionHandler] Using default value for missing argument '{key}': {defaultValue}");

            return defaultValue;
        }

        /// <summary>
        ///     Track function call statistics
        /// </summary>
        private void LogFunctionCallCount(string functionName)
        {
            if (!functionCallCounts.ContainsKey(functionName)) functionCallCounts[functionName] = 0;

            functionCallCounts[functionName]++;
            Debug.Log($"[Stats] Function '{functionName}' called {functionCallCounts[functionName]} times");
        }

        /// <summary>
        ///     Clean up spawned objects (useful for scene transitions)
        /// </summary>
        public void CleanupSpawnedObjects()
        {
            foreach (var obj in spawnedObjects)
                if (obj != null)
                    Destroy(obj);

            spawnedObjects.Clear();
        }

        #endregion

        #region Gameplay Implementation (Stubs - replace with your actual game logic)

        private void SpawnFlameEffect(Vector3 position, float radius, float duration, string color)
        {
            if (flameEffectPrefab != null)
            {
                var flame = Instantiate(flameEffectPrefab, position, Quaternion.identity);
                flame.transform.localScale = Vector3.one * radius;

                // Set color based on parameter
                var renderer = flame.GetComponent<Renderer>();
                if (renderer != null)
                    switch (color.ToLower())
                    {
                        case "blue": renderer.material.color = Color.blue; break;
                        case "green": renderer.material.color = Color.green; break;
                        case "red":
                        default: renderer.material.color = Color.red; break;
                    }

                Destroy(flame, duration);
                spawnedObjects.Add(flame);
            }
        }

        private IEnumerator TeleportWithDelay(GameObject npc, Vector3 targetPosition)
        {
            // Create teleport effect at start position
            CreateTeleportEffect(npc.transform.position);

            yield return new WaitForSeconds(defaultTeleportDelay);

            // Move NPC
            npc.transform.position = targetPosition;

            // Create teleport effect at end position
            CreateTeleportEffect(targetPosition);
            PlayTeleportSound();
        }

        private void SpawnItem(string itemName, Vector3 position)
        {
            // Implement your item spawning logic here
            var item = new GameObject($"Spawned_{itemName}");
            item.transform.position = position;
            item.AddComponent<MeshRenderer>(); // Add basic visual representation

            spawnedObjects.Add(item);

            // Add to your game's item management system
            Debug.Log($"Spawned item: {itemName} at {position}");
        }

        private void ApplyHealing(GameObject target, float amount, string targetType)
        {
            // Implement your healing system here
            Debug.Log($"Applied {amount} healing to {targetType}");
        }

        private void CreateHealEffect(Vector3 position)
        {
            // Create particle effect or visual feedback for healing
            Debug.Log($"Created heal effect at {position}");
        }

        private void PerformAttack(GameObject attacker, string attackType, float damage, float range)
        {
            // Implement your combat system here
            Debug.Log($"Performed {attackType} attack: {damage} damage in {range} unit range");
        }

        private void UpdateQuestProgress(string questId, string updateType, int amount)
        {
            // Implement your quest system here
            Debug.Log($"Updated quest {questId}: {updateType} +{amount}");
        }

        private void CreateTeleportEffect(Vector3 position)
        {
            // Create visual effect for teleportation
            Debug.Log($"Created teleport effect at {position}");
        }

        private void PlayTeleportSound()
        {
            if (teleportSound != null) AudioSource.PlayClipAtPoint(teleportSound, Camera.main.transform.position);
        }

        private void PlayItemSpawnSound()
        {
            if (itemSpawnSound != null) AudioSource.PlayClipAtPoint(itemSpawnSound, Camera.main.transform.position);
        }

        #endregion
    }
}