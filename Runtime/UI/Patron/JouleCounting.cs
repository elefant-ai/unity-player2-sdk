using player2_sdk;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json.Linq;
using Unity.VectorGraphics;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace player2_sdk
{
    public class JouleCounting : MonoBehaviour
    {
        [SerializeField] private NpcManager npcManager;
        [SerializeField] private TextMeshProUGUI textMesh;
        [SerializeField] private SVGImage image;
        [SerializeField] private GameObject counter;
        [SerializeField] private GameObject upgradePanel;
        [SerializeField] private Button upgradeButton;
        private float pollIntervalSeconds = 5f;
        private bool pollStarted;
        
        void Start()
        {
            upgradeButton.onClick.AddListener(() =>
            {
                Application.OpenURL("https://player2.game/profile/patron");
            });
            if (npcManager == null)
            {
                Debug.LogError("NpcManager reference is not set in JouleCounting.");
                return;
            }

            if (textMesh == null)
            {
                Debug.LogError("TextMeshProUGUI reference is not set in JouleCounting.");
                return;
            }

            // If hosted (auth skipped), fetch immediately; otherwise wait for apiTokenReady.
            if (npcManager.ShouldSkipAuthentication())
            {
                if (!pollStarted) { pollStarted = true; _ = PollLoop(); }
            }
            else
            {
                npcManager.apiTokenReady.AddListener(() =>
                {
                    if (pollStarted) return;
                    pollStarted = true;
                    _ = PollLoop();
                });
            }
            
            
        }

        private async Awaitable PollLoop()
        {
            var wait = Mathf.Max(0.1f, pollIntervalSeconds);

            while (enabled && gameObject.activeInHierarchy)
            {
                await FetchAndDisplayJoules();

                float elapsed = 0f;
                while (elapsed < wait && enabled && gameObject.activeInHierarchy)
                {
                    await Awaitable.NextFrameAsync();
                    elapsed += Time.deltaTime;
                }
            }
        }

        private async Awaitable FetchAndDisplayJoules()
        {
            if (npcManager == null) return;

            var url = $"{npcManager.GetBaseUrl()}/joules";
            using (var req = UnityWebRequest.Get(url))
            {
                // Always send client id; add auth header unless auth is skipped
                req.SetRequestHeader("x-client-id", npcManager.clientId);
                if (!npcManager.ShouldSkipAuthentication() && !string.IsNullOrEmpty(npcManager.apiKey))
                {
                    req.SetRequestHeader("Authorization", $"Bearer {npcManager.apiKey}");
                }

                var op = req.SendWebRequest();
                while (!op.isDone)
                {
                    await Awaitable.NextFrameAsync();
                }

                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"GET {url} failed: {req.error}");
                    return;
                }

                var body = req.downloadHandler.text?.Trim();
                if (string.IsNullOrEmpty(body))
                {
                    Debug.LogWarning("/joules returned empty response.");
                    return;
                }

                int joulesValue;
                // Try plain integer first; fall back to JSON parsing
                if (!int.TryParse(body, out joulesValue))
                {
                    try
                    {
                        var token = JToken.Parse(body);
                        if (token.Type == JTokenType.Object && token["joules"] != null)
                        {
                            joulesValue = token["joules"]!.Value<int>();
                        }
                    }
                    catch
                    {
                        Debug.LogWarning($"Failed to parse /joules response: {body}");
                        return;
                    }
                }

                if (joulesValue == 0)
                {
                    if (counter != null) counter.SetActive(false);
                    if (upgradePanel != null) upgradePanel.SetActive(true);
                }
                else
                {
                    if (counter != null) counter.SetActive(true);
                    if (upgradePanel != null) upgradePanel.SetActive(false);
                    if (textMesh != null)
                    {
                        textMesh.text = joulesValue.ToString();
                    }

                    if (image != null)
                    {
                        var c = JoulesToColor(joulesValue);
                        c.a = image.color.a;
                        image.color = c;
                    }
                }
                
            }
        }

        private Color JoulesToColor(int joules)
        {
            var v = Mathf.Clamp(joules, 0, 500);

            var red = Color.red;
            var yellow = Color.yellow;
            var green = Color.green;

            if (v <= 250)
            {
                var t = v / 250f;         
                return Color.Lerp(red, yellow, t);
            }
            else
            {
                var t = (v - 250f) / 250f; 
                return Color.Lerp(yellow, green, t);
            }
        }
    }
}
