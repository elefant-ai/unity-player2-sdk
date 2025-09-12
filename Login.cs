using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine.Networking;

namespace player2_sdk
{
    using System;

    using UnityEngine;
    using UnityEngine.Events;

    [Serializable]
    public class InitiateAuthFlow
    {
        public string ClientId;


        public InitiateAuthFlow(NpcManager npcManager)
        {
            ClientId = npcManager.clientId;
        }
    }



    [Serializable]
    public class InitiateAuthFlowResponse
    {
        public string deviceCode;
        public string userCode;
        public string verificationUri;
        public string verificationUriComplete;
        public uint expiresIn;
        public uint interval;
    }

    [Serializable]
    public class TokenRequest
    {
        public string clientId;
        public string deviceCode;
        public string grantType = "urn:ietf:params:oauth:grant-type:device_code";

        public TokenRequest(string clientId, string deviceCode)
        {
            this.clientId = clientId;
            this.deviceCode = deviceCode;
        }
    }

    [Serializable]
    public class TokenResponse
    {
        public string p2Key;
    }
    public class Login : MonoBehaviour
    {
        [SerializeField]
        public NpcManager npcManager;


        [SerializeField]
        public UnityEvent authenticationFinished;


        [SerializeField]
        public GameObject loginButton;


        private void Awake()
        {
            if (authenticationFinished == null)
            {
                authenticationFinished = new UnityEvent();
            }
            authenticationFinished.AddListener(() =>
            {
                loginButton.SetActive(false);
            });
            _ = TryImmediateWebLogin();

        }


        public async void OpenURL()
        {
            try
            {


                var response = await StartLogin();

                Application.OpenURL(response.verificationUriComplete);

                var token = await GetToken(response);
                Debug.Log("Token received");

                npcManager.NewApiKey.Invoke(token);
                npcManager.apiTokenReady.Invoke();
                authenticationFinished.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private async Awaitable<InitiateAuthFlowResponse> StartLogin()
        {
            string url = $"{npcManager.GetBaseUrl()}/login/device/new";
            var initAuth = new InitiateAuthFlow(npcManager);
            Debug.Log(initAuth);
            string json = JsonConvert.SerializeObject(initAuth, npcManager.JsonSerializerSettings);
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

            using var request = new UnityWebRequest(url, "POST");
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Accept", "application/json");
            await request.SendWebRequest();
            if (request.result == UnityWebRequest.Result.Success)
            {
                if (request.downloadHandler.isDone)
                {
                    var response = JsonConvert.DeserializeObject<InitiateAuthFlowResponse>(request.downloadHandler.text);

                    return response;
                }
                Debug.LogError("Failed to get auth initiation response");
            }
            else
            {
                string traceId = request.GetResponseHeader("X-Player2-Trace-Id");
                string traceInfo = !string.IsNullOrEmpty(traceId) ? $" (X-Player2-Trace-Id: {traceId})" : "";
                string error = $"Failed to start auth: {request.error} - Response: {request.downloadHandler.text}{traceInfo}";
                Debug.LogError(error);
            }
            throw new Exception("Failed to start auth");

        }

        private async Awaitable<string> GetToken(InitiateAuthFlowResponse auth)
        {
            string url = $"{npcManager.GetBaseUrl()}/login/device/token";
            int pollInterval = Mathf.Max(1, (int)auth.interval);        // seconds
            float deadline = Time.realtimeSinceStartup + auth.expiresIn; // seconds from now

            while (Time.realtimeSinceStartup < deadline)
            {
                // Build request body
                var tokenRequest = new TokenRequest(npcManager.clientId, auth.deviceCode);
                string json = JsonConvert.SerializeObject(tokenRequest, npcManager.JsonSerializerSettings);
                byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

                using var request = new UnityWebRequest(url, "POST");
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Accept", "application/json");

                await request.SendWebRequest();

                // Success path
                if (request.result == UnityWebRequest.Result.Success)
                {
                    if (request.downloadHandler.isDone && !string.IsNullOrEmpty(request.downloadHandler.text))
                    {
                        var response = JsonConvert.DeserializeObject<TokenResponse>(request.downloadHandler.text);
                        if (!string.IsNullOrEmpty(response?.p2Key))
                        {
                            return response.p2Key;
                        }
                        // Defensive: success but no key — wait and try again within window
                        Debug.LogWarning("Token endpoint returned success but no key yet. Polling again...");
                    }
                }
                else
                {
                    // Protocol errors (4xx/5xx)
                    long code = request.responseCode;
                    string text = request.downloadHandler?.text ?? string.Empty;

                    // 400 during device flow usually means "authorization_pending" (keep polling)
                    if (code == 400)
                    {
                        // Optional: handle specific OAuth errors if your backend returns them in body
                        // e.g. {"error":"authorization_pending"} | {"error":"slow_down"} | {"error":"expired_token"}
                        try
                        {
                            var errObj = JsonConvert.DeserializeObject<Dictionary<string, object>>(text);
                            if (errObj != null && errObj.TryGetValue("error", out var errVal))
                            {
                                string err = errVal?.ToString();
                                if (string.Equals(err, "slow_down", StringComparison.OrdinalIgnoreCase))
                                {
                                    // RFC 8628 suggests increasing the interval on slow_down
                                    pollInterval += 5;
                                }
                                else if (string.Equals(err, "expired_token", StringComparison.OrdinalIgnoreCase) ||
                                         string.Equals(err, "expired_token_code", StringComparison.OrdinalIgnoreCase))
                                {
                                    Debug.LogError("Device code expired while polling for token.");
                                    return null;
                                }
                                else
                                {
                                    // authorization_pending or unknown -> continue polling
                                }
                            }
                            else
                            {
                                // No structured error? Treat as pending and keep polling.
                            }
                        }
                        catch
                        {
                            // Body not JSON; still treat as pending
                        }
                    }
                    else if (code == 429)
                    {
                        // Too many requests — backoff a bit
                        pollInterval += 5;
                    }
                    else
                    {
                        // Other errors are treated as fatal
                        string traceId = request.GetResponseHeader("X-Player2-Trace-Id");
                        string traceInfo = !string.IsNullOrEmpty(traceId) ? $" (X-Player2-Trace-Id: {traceId})" : "";
                        Debug.LogError($"Failed to get token: HTTP {code} - {request.error} - Response: {text}{traceInfo}");
                        return null;
                    }
                }

                // Wait before next poll, but don’t overrun the deadline
                float remaining = deadline - Time.realtimeSinceStartup;
                if (remaining <= 0f) break;

                int wait = Mathf.Min(pollInterval, Mathf.Max(1, (int)remaining));
                await Awaitable.WaitForSecondsAsync(wait);
            }

            Debug.LogError("Timed out waiting for token (device code flow expired).");
            return null;
    }



    private async Awaitable<bool> TryImmediateWebLogin()
        {
            string url = $"http://localhost:4315/v1/login/web/{npcManager.clientId}";
            using var request = UnityWebRequest.PostWwwForm(url, string.Empty);
            request.SetRequestHeader("Accept", "application/json");
            await request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                var text = request.downloadHandler.text;
                if (!string.IsNullOrEmpty(text))
                {
                    try
                    {
                        var resp = JsonConvert.DeserializeObject<TokenResponse>(text);
                        if (!string.IsNullOrEmpty(resp?.p2Key))
                        {
                            npcManager.NewApiKey.Invoke(resp.p2Key);
                            npcManager.apiTokenReady.Invoke();
                            authenticationFinished.Invoke();
                            return true;
                        }
                        Debug.Log("Immediate web login response lacked p2Key.");
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"Failed to parse immediate web login response: {ex.Message}");
                    }
                }
            }
            else
            {
                Debug.Log($"TryImmediateWebLogin: Failed to connect to Player2 App: {request.responseCode} {request.error}");
            }
            return false;
        }
    }
}
