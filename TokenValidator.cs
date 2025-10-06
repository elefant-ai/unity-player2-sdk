using System;
using UnityEngine;
using UnityEngine.Networking;

namespace player2_sdk
{
    public static class TokenValidator
    {
        /// <summary>
        ///     Validates a token by making a health check request to the API
        /// </summary>
        /// <param name="token">The token to validate</param>
        /// <param name="npcManager">The NpcManager instance for base URL and auth settings</param>
        /// <returns>True if token is valid, false otherwise</returns>
        public static async Awaitable<bool> ValidateTokenAsync(string token, NpcManager npcManager)
        {
            if (string.IsNullOrEmpty(token) || npcManager == null)
            {
                Debug.LogError("TokenValidator: Invalid parameters - token or npcManager is null/empty");
                return false;
            }

            try
            {
                var healthUrl = $"{npcManager.GetBaseUrl()}/health";
                Debug.Log($"TokenValidator: Performing health check at: {healthUrl}");

                using (var request = UnityWebRequest.Get(healthUrl))
                {
                    // Add authorization header if not skipping authentication
                    if (!npcManager.ShouldSkipAuthentication() && !string.IsNullOrEmpty(token))
                        request.SetRequestHeader("Authorization", $"Bearer {token}");

                    // Set timeout for health check
                    request.timeout = 10;

                    var operation = request.SendWebRequest();

                    // Wait for the request to complete
                    while (!operation.isDone) await Awaitable.WaitForSecondsAsync(0.1f);

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        Debug.Log($"TokenValidator: Health check successful: {request.responseCode}");
                        return true;
                    }

                    Debug.LogError(
                        $"TokenValidator: Health check failed: {request.result} - {request.responseCode} - {request.error}");
                    if (!string.IsNullOrEmpty(request.downloadHandler?.text))
                        Debug.LogError($"TokenValidator: Health check response: {request.downloadHandler.text}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"TokenValidator: Exception during health check: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        ///     Validates token and invokes NewApiKey event only if validation passes
        /// </summary>
        /// <param name="token">The token to validate and pass to NpcManager</param>
        /// <param name="npcManager">The NpcManager instance</param>
        /// <returns>True if token was validated and passed to NpcManager, false otherwise</returns>
        public static async Awaitable<bool> ValidateAndSetTokenAsync(string token, NpcManager npcManager)
        {
            var isValid = await ValidateTokenAsync(token, npcManager);

            if (isValid)
            {
                Debug.Log("TokenValidator: Token validation passed, invoking NewApiKey");
                npcManager.NewApiKey.Invoke(token);
                return true;
            }

            Debug.LogError("TokenValidator: Token validation failed, not invoking NewApiKey");
            return false;
        }
    }
}