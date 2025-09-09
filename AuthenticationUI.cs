using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;
using UnityEngine.UI;
using TMPro;

namespace player2_sdk
{
    public enum AuthenticationState
    {
        Checking,
        RequiresAuth,
        StartingDeviceFlow,
        WaitingForUser,
        Success,
        Error
    }

    public class AuthenticationUI : MonoBehaviour
    {
        private static AuthenticationUI instance;

        /// <summary>
        /// One-line setup for authentication UI. Call this with your NpcManager and you're done!
        /// Example: AuthenticationUI.Setup(myNpcManager);
        /// </summary>
        public static AuthenticationUI Setup(NpcManager npcManager)
        {
            // Find existing instance or create new one
            if (instance == null)
            {
                instance = FindObjectOfType<AuthenticationUI>();
            }

            if (instance == null)
            {
                // Create a new GameObject with AuthenticationUI component
                GameObject authObj = new GameObject("AuthenticationUI");
                instance = authObj.AddComponent<AuthenticationUI>();
                
                // Make it persist across scenes (optional)
                DontDestroyOnLoad(authObj);
            }

            // Configure the instance
            instance.npcManager = npcManager;
            instance.autoShowOnStart = true;

            // Start authentication check immediately
            if (instance.gameObject.activeInHierarchy)
            {
                instance.CheckAuthenticationStatus();
            }

            Debug.Log("AuthenticationUI: One-line setup complete! Authentication will start automatically.");
            return instance;
        }

        /// <summary>
        /// Get the current AuthenticationUI instance
        /// </summary>
        public static AuthenticationUI Instance => instance;

        [Header("Configuration")]
        public NpcManager npcManager;
        public bool autoShowOnStart = true;

        [Header("Events")]
        public UnityEvent authenticationStarted;
        public UnityEvent authenticationCompleted;
        public UnityEvent<string> authenticationFailed;

        // UI Components (created automatically)
        private Canvas overlayCanvas;
        private GameObject authPanel;
        private TextMeshProUGUI titleText;
        private TextMeshProUGUI statusText;
        private TextMeshProUGUI descriptionText;
        private Button approveButton;
        private Button denyButton;
        private GameObject spinnerObject;
        private Image spinnerImage;

        // Authentication state
        private AuthenticationState currentState = AuthenticationState.Checking;
        private InitiateAuthFlowResponse currentAuthFlow;
        private bool isAuthenticating = false;
        private string lastError = "";

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
                if (transform.parent == null)
                {
                    DontDestroyOnLoad(gameObject);
                }
            }
            else if (instance != this)
            {
                Destroy(gameObject);
                return;
            }

            InitializeEvents();
        }

        private void Start()
        {
            if (autoShowOnStart && npcManager != null)
            {
                CheckAuthenticationStatus();
            }
        }

        private void Update()
        {
            // Rotate spinner
            if (spinnerImage != null && spinnerObject != null && spinnerObject.activeInHierarchy)
            {
                spinnerImage.transform.Rotate(0, 0, -90f * Time.deltaTime);
            }
        }

        private void InitializeEvents()
        {
            if (authenticationStarted == null)
                authenticationStarted = new UnityEvent();
            if (authenticationCompleted == null)
                authenticationCompleted = new UnityEvent();
            if (authenticationFailed == null)
                authenticationFailed = new UnityEvent<string>();
        }

        public async void CheckAuthenticationStatus()
        {
            if (isAuthenticating || npcManager == null) return;

            SetState(AuthenticationState.Checking);
            CreateUI();
            ShowOverlay();

            try
            {
                bool hasToken = await TryImmediateWebLogin();
                if (hasToken)
                {
                    SetState(AuthenticationState.Success);
                    await Task.Delay(1000);
                    HideOverlay();
                    authenticationCompleted.Invoke();
                }
                else
                {
                    SetState(AuthenticationState.RequiresAuth);
                    await StartDeviceFlow();
                }
            }
            catch (Exception e)
            {
                SetState(AuthenticationState.Error, e.Message);
            }
        }

        private void CreateUI()
        {
            if (overlayCanvas != null) return; // Already created

            // Create overlay canvas
            GameObject canvasObj = new GameObject("AuthOverlay");
            canvasObj.transform.SetParent(transform);
            
            overlayCanvas = canvasObj.AddComponent<Canvas>();
            overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            overlayCanvas.sortingOrder = 1000;
            
            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            
            canvasObj.AddComponent<GraphicRaycaster>();

            // Create background
            GameObject background = new GameObject("Background");
            background.transform.SetParent(canvasObj.transform);
            
            RectTransform bgRect = background.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            
            Image bgImage = background.AddComponent<Image>();
            bgImage.color = new Color(0.1f, 0.12f, 0.16f, 0.95f); // Dark background like screenshot

            // Create main panel
            authPanel = new GameObject("AuthPanel");
            authPanel.transform.SetParent(canvasObj.transform);
            
            RectTransform panelRect = authPanel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(600, 450);
            panelRect.anchoredPosition = Vector2.zero;
            
            Image panelBg = authPanel.AddComponent<Image>();
            panelBg.color = new Color(0.15f, 0.17f, 0.22f, 1f); // Panel background
            
            // Add subtle border
            Outline panelOutline = authPanel.AddComponent<Outline>();
            panelOutline.effectColor = new Color(0.3f, 0.35f, 0.45f, 0.8f);
            panelOutline.effectDistance = new Vector2(2, -2);

            CreateUIElements();
        }

        private void CreateUIElements()
        {
            // Title with brain emoji
            GameObject titleObj = new GameObject("Title");
            titleObj.transform.SetParent(authPanel.transform);
            
            RectTransform titleRect = titleObj.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.5f, 1f);
            titleRect.anchorMax = new Vector2(0.5f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.sizeDelta = new Vector2(500, 60);
            titleRect.anchoredPosition = new Vector2(0, -40);
            
            titleText = titleObj.AddComponent<TextMeshProUGUI>();
            titleText.text = "🧠 Player2 AI Required";
            titleText.fontSize = 32;
            titleText.color = new Color(0.4f, 0.7f, 1f, 1f); // Blue like screenshot
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.fontStyle = FontStyles.Bold;

            // Status text (checking authentication...)
            GameObject statusObj = new GameObject("StatusText");
            statusObj.transform.SetParent(authPanel.transform);
            
            RectTransform statusRect = statusObj.AddComponent<RectTransform>();
            statusRect.anchorMin = new Vector2(0.1f, 0.7f);
            statusRect.anchorMax = new Vector2(0.9f, 0.8f);
            statusRect.offsetMin = Vector2.zero;
            statusRect.offsetMax = Vector2.zero;
            
            statusText = statusObj.AddComponent<TextMeshProUGUI>();
            statusText.text = "Checking authentication...";
            statusText.fontSize = 18;
            statusText.color = new Color(1f, 0.8f, 0.4f, 1f); // Orange like screenshot
            statusText.alignment = TextAlignmentOptions.Left;

            // Description text
            GameObject descObj = new GameObject("Description");
            descObj.transform.SetParent(authPanel.transform);
            
            RectTransform descRect = descObj.AddComponent<RectTransform>();
            descRect.anchorMin = new Vector2(0.1f, 0.4f);
            descRect.anchorMax = new Vector2(0.9f, 0.65f);
            descRect.offsetMin = Vector2.zero;
            descRect.offsetMax = Vector2.zero;
            
            descriptionText = descObj.AddComponent<TextMeshProUGUI>();
            descriptionText.text = "This game uses Player2's AI system to power in-game conversations with NPCs and generate hilarious live chat reactions. Without it, the core gameplay won't work.";
            descriptionText.fontSize = 16;
            descriptionText.color = new Color(0.8f, 0.8f, 0.8f, 1f);
            descriptionText.alignment = TextAlignmentOptions.Left;
            descriptionText.enableWordWrapping = true;

            // Create spinner (initially hidden)
            CreateSpinner();

            // Create buttons
            CreateButtons();
        }

        private void CreateSpinner()
        {
            spinnerObject = new GameObject("Spinner");
            spinnerObject.transform.SetParent(authPanel.transform);
            spinnerObject.SetActive(false);
            
            RectTransform spinnerRect = spinnerObject.AddComponent<RectTransform>();
            spinnerRect.anchorMin = new Vector2(0.15f, 0.7f);
            spinnerRect.anchorMax = new Vector2(0.15f, 0.7f);
            spinnerRect.pivot = new Vector2(0.5f, 0.5f);
            spinnerRect.sizeDelta = new Vector2(24, 24);
            spinnerRect.anchoredPosition = new Vector2(-30, 0);
            
            spinnerImage = spinnerObject.AddComponent<Image>();
            spinnerImage.color = new Color(1f, 0.8f, 0.4f, 1f);
            
            // Create simple spinner texture
            CreateSpinnerTexture();
        }

        private void CreateSpinnerTexture()
        {
            Texture2D spinnerTex = new Texture2D(32, 32);
            Color[] pixels = new Color[32 * 32];
            Vector2 center = new Vector2(16, 16);
            
            for (int y = 0; y < 32; y++)
            {
                for (int x = 0; x < 32; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center);
                    float angle = Mathf.Atan2(y - 16, x - 16) * Mathf.Rad2Deg;
                    
                    if (distance <= 14 && distance >= 8)
                    {
                        // Create partial circle (loading spinner effect)
                        float normalizedAngle = (angle + 180) / 360f;
                        float alpha = Mathf.Clamp01(1f - normalizedAngle * 2f);
                        if (alpha > 0.1f)
                        {
                            pixels[y * 32 + x] = new Color(1, 1, 1, alpha);
                        }
                        else
                        {
                            pixels[y * 32 + x] = Color.clear;
                        }
                    }
                    else
                    {
                        pixels[y * 32 + x] = Color.clear;
                    }
                }
            }
            
            spinnerTex.SetPixels(pixels);
            spinnerTex.Apply();
            
            Sprite spinnerSprite = Sprite.Create(spinnerTex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f));
            spinnerImage.sprite = spinnerSprite;
        }

        private void CreateButtons()
        {
            // Deny button (left)
            GameObject denyObj = new GameObject("DenyButton");
            denyObj.transform.SetParent(authPanel.transform);
            
            RectTransform denyRect = denyObj.AddComponent<RectTransform>();
            denyRect.anchorMin = new Vector2(0.25f, 0.1f);
            denyRect.anchorMax = new Vector2(0.45f, 0.25f);
            denyRect.offsetMin = Vector2.zero;
            denyRect.offsetMax = Vector2.zero;
            
            denyButton = denyObj.AddComponent<Button>();
            Image denyBg = denyObj.AddComponent<Image>();
            denyBg.color = new Color(0.8f, 0.3f, 0.3f, 1f); // Red like screenshot
            
            GameObject denyTextObj = new GameObject("Text");
            denyTextObj.transform.SetParent(denyObj.transform);
            
            RectTransform denyTextRect = denyTextObj.AddComponent<RectTransform>();
            denyTextRect.anchorMin = Vector2.zero;
            denyTextRect.anchorMax = Vector2.one;
            denyTextRect.offsetMin = Vector2.zero;
            denyTextRect.offsetMax = Vector2.zero;
            
            TextMeshProUGUI denyText = denyTextObj.AddComponent<TextMeshProUGUI>();
            denyText.text = "Deny";
            denyText.fontSize = 16;
            denyText.color = Color.white;
            denyText.alignment = TextAlignmentOptions.Center;
            denyText.fontStyle = FontStyles.Bold;
            
            denyButton.targetGraphic = denyBg;
            denyButton.onClick.AddListener(OnDenyClicked);

            // Approve button (right)
            GameObject approveObj = new GameObject("ApproveButton");
            approveObj.transform.SetParent(authPanel.transform);
            
            RectTransform approveRect = approveObj.AddComponent<RectTransform>();
            approveRect.anchorMin = new Vector2(0.55f, 0.1f);
            approveRect.anchorMax = new Vector2(0.75f, 0.25f);
            approveRect.offsetMin = Vector2.zero;
            approveRect.offsetMax = Vector2.zero;
            
            approveButton = approveObj.AddComponent<Button>();
            Image approveBg = approveObj.AddComponent<Image>();
            approveBg.color = new Color(0.3f, 0.7f, 0.3f, 1f); // Green like screenshot
            
            GameObject approveTextObj = new GameObject("Text");
            approveTextObj.transform.SetParent(approveObj.transform);
            
            RectTransform approveTextRect = approveTextObj.AddComponent<RectTransform>();
            approveTextRect.anchorMin = Vector2.zero;
            approveTextRect.anchorMax = Vector2.one;
            approveTextRect.offsetMin = Vector2.zero;
            approveTextRect.offsetMax = Vector2.zero;
            
            TextMeshProUGUI approveText = approveTextObj.AddComponent<TextMeshProUGUI>();
            approveText.text = "Approve";
            approveText.fontSize = 16;
            approveText.color = Color.white;
            approveText.alignment = TextAlignmentOptions.Center;
            approveText.fontStyle = FontStyles.Bold;
            
            approveButton.targetGraphic = approveBg;
            approveButton.onClick.AddListener(OnApproveClicked);

            // Initially hide buttons
            denyButton.gameObject.SetActive(false);
            approveButton.gameObject.SetActive(false);
        }

        private void OnApproveClicked()
        {
            if (currentAuthFlow != null && !string.IsNullOrEmpty(currentAuthFlow.verificationUriComplete))
            {
                Application.OpenURL(currentAuthFlow.verificationUriComplete);
                SetState(AuthenticationState.WaitingForUser);
            }
        }

        private void OnDenyClicked()
        {
            HideOverlay();
            authenticationFailed.Invoke("User denied authentication");
        }

        private async Task<bool> TryImmediateWebLogin()
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
                            return true;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"Failed to parse immediate web login response: {ex.Message}");
                    }
                }
            }
            return false;
        }

        private async Task StartDeviceFlow()
        {
            if (isAuthenticating) return;

            isAuthenticating = true;
            authenticationStarted.Invoke();
            SetState(AuthenticationState.StartingDeviceFlow);

            try
            {
                currentAuthFlow = await InitiateAuthFlow();
                SetState(AuthenticationState.RequiresAuth);
                
                // Start polling in background
                _ = PollForToken(currentAuthFlow);
            }
            catch (Exception e)
            {
                SetState(AuthenticationState.Error, e.Message);
                isAuthenticating = false;
            }
        }

        private async Task<InitiateAuthFlowResponse> InitiateAuthFlow()
        {
            string url = $"{npcManager.GetBaseUrl()}/login/device/new";
            var initAuth = new InitiateAuthFlow(npcManager);
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
                    return JsonConvert.DeserializeObject<InitiateAuthFlowResponse>(request.downloadHandler.text);
                }
                throw new Exception("Failed to get auth initiation response");
            }
            else
            {
                string traceId = request.GetResponseHeader("X-Player2-Trace-Id");
                string traceInfo = !string.IsNullOrEmpty(traceId) ? $" (X-Player2-Trace-Id: {traceId})" : "";
                string error = $"Failed to start auth: {request.error} - Response: {request.downloadHandler.text}{traceInfo}";
                throw new Exception(error);
            }
        }

        private async Task PollForToken(InitiateAuthFlowResponse auth)
        {
            string url = $"{npcManager.GetBaseUrl()}/login/device/token";
            int pollInterval = Mathf.Max(1, (int)auth.interval);
            float deadline = Time.realtimeSinceStartup + auth.expiresIn;

            while (Time.realtimeSinceStartup < deadline && currentState != AuthenticationState.Success)
            {
                var tokenRequest = new TokenRequest(npcManager.clientId, auth.deviceCode);
                string json = JsonConvert.SerializeObject(tokenRequest, npcManager.JsonSerializerSettings);
                byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

                using var request = new UnityWebRequest(url, "POST");
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Accept", "application/json");

                await request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    if (request.downloadHandler.isDone && !string.IsNullOrEmpty(request.downloadHandler.text))
                    {
                        var response = JsonConvert.DeserializeObject<TokenResponse>(request.downloadHandler.text);
                        if (!string.IsNullOrEmpty(response?.p2Key))
                        {
                            npcManager.NewApiKey.Invoke(response.p2Key);
                            SetState(AuthenticationState.Success);
                            await Task.Delay(1500);
                            HideOverlay();
                            authenticationCompleted.Invoke();
                            isAuthenticating = false;
                            return;
                        }
                    }
                }
                else if (request.responseCode == 400)
                {
                    // Continue polling for 400 errors (pending)
                }
                else if (request.responseCode == 429)
                {
                    pollInterval += 5;
                }
                else
                {
                    string traceId = request.GetResponseHeader("X-Player2-Trace-Id");
                    string traceInfo = !string.IsNullOrEmpty(traceId) ? $" (X-Player2-Trace-Id: {traceId})" : "";
                    Debug.LogError($"Failed to get token: HTTP {request.responseCode} - {request.error} - Response: {request.downloadHandler.text}{traceInfo}");
                    SetState(AuthenticationState.Error, $"Authentication failed: {request.error}");
                    isAuthenticating = false;
                    return;
                }

                float remaining = deadline - Time.realtimeSinceStartup;
                if (remaining <= 0f) break;

                int wait = Mathf.Min(pollInterval, Mathf.Max(1, (int)remaining));
                await Awaitable.WaitForSecondsAsync(wait);
            }

            SetState(AuthenticationState.Error, "Authentication timed out");
            isAuthenticating = false;
        }

        private void SetState(AuthenticationState newState, string errorMessage = "")
        {
            currentState = newState;
            lastError = errorMessage;
            UpdateUI();

            if (newState == AuthenticationState.Error && !string.IsNullOrEmpty(errorMessage))
            {
                authenticationFailed.Invoke(errorMessage);
            }
        }

        private void UpdateUI()
        {
            if (statusText == null) return;

            switch (currentState)
            {
                case AuthenticationState.Checking:
                    statusText.text = "🔄 Checking authentication...";
                    statusText.color = new Color(1f, 0.8f, 0.4f, 1f);
                    SetSpinnerActive(true);
                    SetButtonsActive(false);
                    break;

                case AuthenticationState.StartingDeviceFlow:
                    statusText.text = "🔄 Starting authentication...";
                    statusText.color = new Color(1f, 0.8f, 0.4f, 1f);
                    SetSpinnerActive(true);
                    SetButtonsActive(false);
                    break;

                case AuthenticationState.RequiresAuth:
                    statusText.text = "🔐 Authentication Required";
                    statusText.color = new Color(1f, 0.8f, 0.4f, 1f);
                    SetSpinnerActive(false);
                    SetButtonsActive(true);
                    break;

                case AuthenticationState.WaitingForUser:
                    statusText.text = "⏳ Waiting for browser authentication...";
                    statusText.color = new Color(0.4f, 0.7f, 1f, 1f);
                    SetSpinnerActive(true);
                    SetButtonsActive(false);
                    break;

                case AuthenticationState.Success:
                    statusText.text = "✅ Authentication successful!";
                    statusText.color = new Color(0.3f, 0.8f, 0.3f, 1f);
                    SetSpinnerActive(false);
                    SetButtonsActive(false);
                    break;

                case AuthenticationState.Error:
                    statusText.text = $"❌ Authentication failed: {lastError}";
                    statusText.color = new Color(0.8f, 0.3f, 0.3f, 1f);
                    SetSpinnerActive(false);
                    SetButtonsActive(false);
                    break;
            }
        }

        private void SetSpinnerActive(bool active)
        {
            if (spinnerObject != null)
                spinnerObject.SetActive(active);
        }

        private void SetButtonsActive(bool active)
        {
            if (approveButton != null)
                approveButton.gameObject.SetActive(active);
            if (denyButton != null)
                denyButton.gameObject.SetActive(active);
        }

        public void ShowOverlay()
        {
            if (overlayCanvas != null)
                overlayCanvas.gameObject.SetActive(true);
        }

        public void HideOverlay()
        {
            if (overlayCanvas != null)
                overlayCanvas.gameObject.SetActive(false);
        }

        public bool IsAuthenticated()
        {
            return !string.IsNullOrEmpty(npcManager?.apiKey);
        }

        public void ForceShowAuthUI()
        {
            CheckAuthenticationStatus();
        }
    }

}