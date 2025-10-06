using System;
using System.Collections;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;
using UnityEngine.UI;

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
        [Header("Configuration")] public NpcManager npcManager;

        public bool autoShowOnStart = true;

        [Header("Events")] public UnityEvent authenticationStarted;

        public UnityEvent authenticationCompleted;
        public UnityEvent<string> authenticationFailed;
        private Button approveButton;
        private GameObject authPanel;
        private InitiateAuthFlowResponse currentAuthFlow;

        // Authentication state
        private AuthenticationState currentState = AuthenticationState.Checking;
        private TextMeshProUGUI descriptionText;
        private Image exampleImage;
        private bool isAuthenticating;
        private string lastError = "";

        // UI Components (created automatically)
        private Canvas overlayCanvas;
        private Image spinnerImage;
        private GameObject spinnerObject;
        private TextMeshProUGUI statusText;
        private TextMeshProUGUI titleText;

        /// <summary>
        ///     Get the current AuthenticationUI instance
        /// </summary>
        public static AuthenticationUI Instance { get; private set; }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                if (transform.parent == null) DontDestroyOnLoad(gameObject);
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            InitializeEvents();
        }

        private void Start()
        {
            if (autoShowOnStart && npcManager != null) CheckAuthenticationStatus();
        }

        private void Update()
        {
            if (spinnerImage != null && spinnerObject != null && spinnerObject.activeInHierarchy)
                spinnerImage.transform.Rotate(0, 0, -90f * Time.deltaTime);
        }

        /// <summary>
        ///     One-line setup for authentication UI. Call this with your NpcManager and you're done!
        ///     Example: AuthenticationUI.Setup(myNpcManager);
        /// </summary>
        public static AuthenticationUI Setup(NpcManager npcManager)
        {
            if (Instance == null) Instance = FindObjectOfType<AuthenticationUI>();

            if (Instance == null)
            {
                var authObj = new GameObject("AuthenticationUI");
                Instance = authObj.AddComponent<AuthenticationUI>();
                DontDestroyOnLoad(authObj);
            }

            Instance.npcManager = npcManager;
            Instance.autoShowOnStart = true;

            if (Instance.gameObject.activeInHierarchy) Instance.CheckAuthenticationStatus();

            Debug.Log("AuthenticationUI: One-line setup complete! Authentication will start automatically.");
            return Instance;
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

            // Skip authentication if running in WebGL on player2.game domain
            Debug.Log("AuthenticationUI.CheckAuthenticationStatus: Checking if authentication should be skipped...");
            if (npcManager.ShouldSkipAuthentication())
            {
                Debug.Log(
                    "AuthenticationUI.CheckAuthenticationStatus: Running on player2.game domain, skipping authentication entirely");
                Debug.Log(
                    $"AuthenticationUI.CheckAuthenticationStatus: Base URL being used: {npcManager.GetBaseUrl()}");

                // Trigger NPC initialization with empty API key (auth headers not needed for hosted)
                Debug.Log(
                    "AuthenticationUI.CheckAuthenticationStatus: Triggering NewApiKey with empty string for hosted auth bypass");
                npcManager.NewApiKey.Invoke("");

                SetState(AuthenticationState.Success);
                authenticationCompleted.Invoke();
                Debug.Log("AuthenticationUI.CheckAuthenticationStatus: Authentication bypass completed successfully");
                return;
            }

            Debug.Log(
                "AuthenticationUI.CheckAuthenticationStatus: Authentication not being skipped, proceeding with normal flow");

            SetState(AuthenticationState.Checking);
            CreateUI();
            ShowOverlay();

            try
            {
                Debug.Log("CheckAuthenticationStatus: Attempting localhost authentication first");
                var hasToken = await TryImmediateWebLogin();

                if (hasToken)
                {
                    SetState(AuthenticationState.Success);
                    HideOverlay();
                    authenticationCompleted.Invoke();
                }
                else
                {
                    Debug.Log("CheckAuthenticationStatus: Localhost auth failed, starting device flow");
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
            if (overlayCanvas != null) return;

            var canvasObj = new GameObject("AuthOverlay");
            canvasObj.transform.SetParent(transform);

            overlayCanvas = canvasObj.AddComponent<Canvas>();
            overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            overlayCanvas.sortingOrder = 1000;

            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();

            var background = new GameObject("Background");
            background.transform.SetParent(canvasObj.transform);

            var bgRect = background.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            var bgImage = background.AddComponent<Image>();
            bgImage.color = new Color(0.1f, 0.12f, 0.16f, 0.95f); // Dark background like screenshot

            authPanel = new GameObject("AuthPanel");
            authPanel.transform.SetParent(canvasObj.transform);

            var panelRect = authPanel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(800, 400);
            panelRect.anchoredPosition = Vector2.zero;

            var panelBg = authPanel.AddComponent<Image>();
            panelBg.color = new Color(0.12f, 0.14f, 0.18f, 0.98f);

            var panelOutline = authPanel.AddComponent<Outline>();
            panelOutline.effectColor = new Color(0.4f, 0.7f, 1f, 1f);
            panelOutline.effectDistance = new Vector2(2, -2);
            panelOutline.useGraphicAlpha = false;

            CreateUIElements();
        }

        private void CreateUIElements()
        {
            var titleObj = new GameObject("Title");
            titleObj.transform.SetParent(authPanel.transform);

            var titleRect = titleObj.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.35f, 0.7f);
            titleRect.anchorMax = new Vector2(0.95f, 0.85f);
            titleRect.offsetMin = Vector2.zero;
            titleRect.offsetMax = Vector2.zero;

            titleText = titleObj.AddComponent<TextMeshProUGUI>();
            titleText.text = "Player2 AI Required";
            titleText.fontSize = 42f;
            titleText.enableAutoSizing = false;
            titleText.extraPadding = true;
            titleText.color = new Color(0.9f, 0.95f, 1f, 1f);
            titleText.alignment = TextAlignmentOptions.Left;
            titleText.fontStyle = FontStyles.Bold;

            var statusObj = new GameObject("StatusText");
            statusObj.transform.SetParent(authPanel.transform);

            var statusRect = statusObj.AddComponent<RectTransform>();
            statusRect.anchorMin = new Vector2(0.35f, 0.6f);
            statusRect.anchorMax = new Vector2(0.95f, 0.68f);
            statusRect.offsetMin = Vector2.zero;
            statusRect.offsetMax = Vector2.zero;

            statusText = statusObj.AddComponent<TextMeshProUGUI>();
            statusText.text = "Authentication Required";
            statusText.fontSize = 24f;
            statusText.enableAutoSizing = false;
            statusText.extraPadding = true;
            statusText.color = new Color(1f, 0.85f, 0.4f, 1f);
            statusText.alignment = TextAlignmentOptions.Left;
            statusText.fontStyle = FontStyles.Normal;

            var descObj = new GameObject("Description");
            descObj.transform.SetParent(authPanel.transform);

            var descRect = descObj.AddComponent<RectTransform>();
            descRect.anchorMin = new Vector2(0.35f, 0.3f);
            descRect.anchorMax = new Vector2(0.95f, 0.55f);
            descRect.offsetMin = Vector2.zero;
            descRect.offsetMax = Vector2.zero;

            descriptionText = descObj.AddComponent<TextMeshProUGUI>();
            descriptionText.text =
                "This game uses Player2's AI system to power in-game conversations with NPCs and generate hilarious live chat reactions. Without it, the core gameplay won't work.";
            descriptionText.fontSize = 18f;
            descriptionText.enableAutoSizing = false;
            descriptionText.extraPadding = true;
            descriptionText.color = new Color(0.85f, 0.9f, 0.95f, 1f);
            descriptionText.alignment = TextAlignmentOptions.Left;
            descriptionText.enableWordWrapping = true;
            descriptionText.fontStyle = FontStyles.Normal;
            descriptionText.lineSpacing = 1.3f;

            CreateExampleImage();
            CreateSpinner();
            CreateButtons();
        }

        private void CreateSpinner()
        {
            spinnerObject = new GameObject("Spinner");
            spinnerObject.transform.SetParent(authPanel.transform);
            spinnerObject.SetActive(false);

            var spinnerRect = spinnerObject.AddComponent<RectTransform>();
            spinnerRect.anchorMin = new Vector2(0.5f, 0.5f);
            spinnerRect.anchorMax = new Vector2(0.5f, 0.5f);
            spinnerRect.pivot = new Vector2(0.5f, 0.5f);
            spinnerRect.sizeDelta = new Vector2(80, 80);
            spinnerRect.anchoredPosition = Vector2.zero;

            spinnerImage = spinnerObject.AddComponent<Image>();
            spinnerImage.color = new Color(1f, 0.8f, 0.4f, 1f);

            CreateSpinnerTexture();
        }

        private void CreateSpinnerTexture()
        {
            var spinnerTex = new Texture2D(64, 64);
            var pixels = new Color[64 * 64];
            var center = new Vector2(32, 32);

            for (var y = 0; y < 64; y++)
            for (var x = 0; x < 64; x++)
            {
                var distance = Vector2.Distance(new Vector2(x, y), center);
                var angle = Mathf.Atan2(y - 32, x - 32) * Mathf.Rad2Deg;

                if (distance <= 28 && distance >= 18)
                {
                    var normalizedAngle = (angle + 180) / 360f;
                    var alpha = Mathf.Clamp01(1f - normalizedAngle * 1.5f);
                    if (alpha > 0.1f)
                        pixels[y * 64 + x] = new Color(1, 1, 1, alpha);
                    else
                        pixels[y * 64 + x] = Color.clear;
                }
                else
                {
                    pixels[y * 64 + x] = Color.clear;
                }
            }

            spinnerTex.SetPixels(pixels);
            spinnerTex.Apply();

            var spinnerSprite = Sprite.Create(spinnerTex, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f));
            spinnerImage.sprite = spinnerSprite;
        }

        private void CreateExampleImage()
        {
            var imageObj = new GameObject("Player2Logo");
            imageObj.transform.SetParent(authPanel.transform);

            var imageRect = imageObj.AddComponent<RectTransform>();
            imageRect.anchorMin = new Vector2(0.08f, 0.55f);
            imageRect.anchorMax = new Vector2(0.08f, 0.55f);
            imageRect.pivot = new Vector2(0.5f, 0.5f);
            imageRect.sizeDelta = new Vector2(160, 160);
            imageRect.anchoredPosition = new Vector2(80, 0);

            exampleImage = imageObj.AddComponent<Image>();
            LoadPlayer2Logo();
        }

        private void LoadPlayer2Logo()
        {
            StartCoroutine(LoadImageFromUrl("https://assets.elefant.gg/player2.png"));
        }

        private IEnumerator LoadImageFromUrl(string url)
        {
            using (var www = UnityWebRequestTexture.GetTexture(url))
            {
                yield return www.SendWebRequest();

                if (www.result == UnityWebRequest.Result.Success)
                {
                    var logoTexture = DownloadHandlerTexture.GetContent(www);
                    if (logoTexture != null)
                    {
                        var logoSprite = Sprite.Create(logoTexture,
                            new Rect(0, 0, logoTexture.width, logoTexture.height), new Vector2(0.5f, 0.5f));
                        exampleImage.sprite = logoSprite;
                        exampleImage.preserveAspect = true;
                    }
                }
                else
                {
                    Debug.LogWarning($"Failed to load Player2 logo from URL: {www.error}");
                    CreateFallbackLogo();
                }
            }
        }

        private void CreateFallbackLogo()
        {
            var fallbackTex = new Texture2D(150, 150);
            var pixels = new Color[150 * 150];

            var bgColor = new Color(0.4f, 0.7f, 1f, 1f);
            for (var i = 0; i < pixels.Length; i++) pixels[i] = bgColor;

            var textColor = Color.white;
            for (var y = 60; y < 90; y++)
            for (var x = 50; x < 100; x++)
                if ((x >= 55 && x <= 60) ||
                    (y >= 60 && y <= 65 && x >= 55 && x <= 70) ||
                    (y >= 72 && y <= 77 && x >= 55 && x <= 70) ||
                    (x >= 70 && x <= 75 && y >= 60 && y <= 77) ||
                    (x >= 80 && x <= 95 && y >= 60 && y <= 65) ||
                    (x >= 90 && x <= 95 && y >= 60 && y <= 72) ||
                    (x >= 80 && x <= 95 && y >= 72 && y <= 77) ||
                    (x >= 80 && x <= 85 && y >= 77 && y <= 90) ||
                    (x >= 80 && x <= 95 && y >= 85 && y <= 90))
                    pixels[y * 150 + x] = textColor;

            fallbackTex.SetPixels(pixels);
            fallbackTex.Apply();

            var fallbackSprite = Sprite.Create(fallbackTex, new Rect(0, 0, 150, 150), new Vector2(0.5f, 0.5f));
            exampleImage.sprite = fallbackSprite;
            exampleImage.preserveAspect = true;
        }

        private void CreateButtons()
        {
            var approveObj = new GameObject("ApproveButton");
            approveObj.transform.SetParent(authPanel.transform);

            var approveRect = approveObj.AddComponent<RectTransform>();
            approveRect.anchorMin = new Vector2(0.3f, 0.08f);
            approveRect.anchorMax = new Vector2(0.7f, 0.22f);
            approveRect.offsetMin = Vector2.zero;
            approveRect.offsetMax = Vector2.zero;

            approveButton = approveObj.AddComponent<Button>();
            var approveBg = approveObj.AddComponent<Image>();
            approveBg.color = new Color(0.2f, 0.65f, 0.2f, 1f);

            var approveTextObj = new GameObject("Text");
            approveTextObj.transform.SetParent(approveObj.transform);

            var approveTextRect = approveTextObj.AddComponent<RectTransform>();
            approveTextRect.anchorMin = Vector2.zero;
            approveTextRect.anchorMax = Vector2.one;
            approveTextRect.offsetMin = Vector2.zero;
            approveTextRect.offsetMax = Vector2.zero;

            var approveText = approveTextObj.AddComponent<TextMeshProUGUI>();
            approveText.text = "Approve";
            approveText.fontSize = 24f;
            approveText.enableAutoSizing = false;
            approveText.extraPadding = true;
            approveText.color = new Color(0.95f, 1f, 0.95f, 1f);
            approveText.alignment = TextAlignmentOptions.Center;
            approveText.fontStyle = FontStyles.Bold;

            approveButton.targetGraphic = approveBg;
            approveButton.onClick.AddListener(OnApproveClicked);
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


        private async Task<bool> TryImmediateWebLogin()
        {
            // Skip localhost authentication if running in WebGL on player2.game domain
            Debug.Log(
                "AuthenticationUI.TryImmediateWebLogin: Checking if localhost authentication should be skipped...");
            if (npcManager.ShouldSkipAuthentication())
            {
                Debug.Log(
                    "AuthenticationUI.TryImmediateWebLogin: Running on player2.game domain, skipping localhost authentication");
                Debug.Log($"AuthenticationUI.TryImmediateWebLogin: API requests will use: {npcManager.GetBaseUrl()}");
                return true;
            }

            Debug.Log(
                "AuthenticationUI.TryImmediateWebLogin: Not on player2.game domain, proceeding with localhost authentication");

            var url = $"http://localhost:4315/v1/login/web/{npcManager.clientId}";
            Debug.Log($"TryImmediateWebLogin: Attempting localhost auth at: {url}");

            using var request = UnityWebRequest.PostWwwForm(url, string.Empty);
            request.SetRequestHeader("Accept", "application/json");
            await request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                var text = request.downloadHandler.text;
                if (!string.IsNullOrEmpty(text))
                    try
                    {
                        var resp = JsonConvert.DeserializeObject<TokenResponse>(text);
                        if (!string.IsNullOrEmpty(resp?.p2Key))
                        {
                            Debug.Log("TryImmediateWebLogin: Got API key, validating with health check...");
                            var tokenValid = await TokenValidator.ValidateAndSetTokenAsync(resp.p2Key, npcManager);
                            if (tokenValid)
                            {
                                Debug.Log("TryImmediateWebLogin: Token validation successful");
                                return true;
                            }

                            Debug.LogError("TryImmediateWebLogin: Token validation failed");
                            return false;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"Failed to parse immediate web login response: {ex.Message}");
                    }
            }
            else
            {
                Debug.Log($"TryImmediateWebLogin: Failed to connect to Player2 App: {request.error}");
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
            var url = $"{npcManager.GetBaseUrl()}/login/device/new";
            var initAuth = new InitiateAuthFlow(npcManager);
            var json = JsonConvert.SerializeObject(initAuth, npcManager.JsonSerializerSettings);
            var bodyRaw = Encoding.UTF8.GetBytes(json);

            using var request = new UnityWebRequest(url, "POST");
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Accept", "application/json");

            await request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                if (request.downloadHandler.isDone)
                    return JsonConvert.DeserializeObject<InitiateAuthFlowResponse>(request.downloadHandler.text);
                throw new Exception("Failed to get auth initiation response");
            }

            var traceId = request.GetResponseHeader("X-Player2-Trace-Id");
            var traceInfo = !string.IsNullOrEmpty(traceId) ? $" (X-Player2-Trace-Id: {traceId})" : "";
            var error = $"Failed to start auth: {request.error} - Response: {request.downloadHandler.text}{traceInfo}";
            throw new Exception(error);
        }

        private async Task PollForToken(InitiateAuthFlowResponse auth)
        {
            var url = $"{npcManager.GetBaseUrl()}/login/device/token";
            var pollInterval = Mathf.Max(1, (int)auth.interval);
            var deadline = Time.realtimeSinceStartup + auth.expiresIn;

            while (Time.realtimeSinceStartup < deadline && currentState != AuthenticationState.Success)
            {
                var tokenRequest = new TokenRequest(npcManager.clientId, auth.deviceCode);
                var json = JsonConvert.SerializeObject(tokenRequest, npcManager.JsonSerializerSettings);
                var bodyRaw = Encoding.UTF8.GetBytes(json);

                // Suppress console error logs for expected polling failures (400 = authorization_pending)
                var originalLogEnabled = Debug.unityLogger.logEnabled;
                if (Application.platform == RuntimePlatform.WebGLPlayer) Debug.unityLogger.logEnabled = false;

                using var request = new UnityWebRequest(url, "POST");
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Accept", "application/json");

                await request.SendWebRequest();

                // Restore logging after request
                if (Application.platform == RuntimePlatform.WebGLPlayer)
                    Debug.unityLogger.logEnabled = originalLogEnabled;

                if (request.result == UnityWebRequest.Result.Success)
                {
                    if (request.downloadHandler.isDone && !string.IsNullOrEmpty(request.downloadHandler.text))
                    {
                        var response = JsonConvert.DeserializeObject<TokenResponse>(request.downloadHandler.text);
                        if (!string.IsNullOrEmpty(response?.p2Key))
                        {
                            Debug.Log("PollForToken: Got token, validating with health check...");
                            var tokenValid = await TokenValidator.ValidateAndSetTokenAsync(response.p2Key, npcManager);
                            if (tokenValid)
                            {
                                Debug.Log("PollForToken: Token validation successful");
                                SetState(AuthenticationState.Success);
                                HideOverlay();
                                authenticationCompleted.Invoke();
                                isAuthenticating = false;
                                return;
                            }

                            Debug.LogError("PollForToken: Token validation failed, continuing to poll");
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
                    var traceId = request.GetResponseHeader("X-Player2-Trace-Id");
                    var traceInfo = !string.IsNullOrEmpty(traceId) ? $" (X-Player2-Trace-Id: {traceId})" : "";
                    Debug.LogError(
                        $"Failed to get token: HTTP {request.responseCode} - {request.error} - Response: {request.downloadHandler.text}{traceInfo}");
                    SetState(AuthenticationState.Error, $"Authentication failed: {request.error}");
                    isAuthenticating = false;
                    return;
                }

                var remaining = deadline - Time.realtimeSinceStartup;
                if (remaining <= 0f) break;

                var wait = Mathf.Min(pollInterval, Mathf.Max(1, (int)remaining));
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
                authenticationFailed.Invoke(errorMessage);
        }

        private void UpdateUI()
        {
            if (statusText == null) return;

            switch (currentState)
            {
                case AuthenticationState.Checking:
                    statusText.text = "Checking authentication...";
                    statusText.color = new Color(1f, 0.8f, 0.4f, 1f);
                    SetSpinnerActive(true);
                    SetButtonsActive(false);
                    break;

                case AuthenticationState.StartingDeviceFlow:
                    statusText.text = "Starting authentication...";
                    statusText.color = new Color(1f, 0.8f, 0.4f, 1f);
                    SetSpinnerActive(true);
                    SetButtonsActive(false);
                    break;

                case AuthenticationState.RequiresAuth:
                    statusText.text = "Authentication Required";
                    statusText.color = new Color(1f, 0.8f, 0.4f, 1f);
                    SetSpinnerActive(false);
                    SetButtonsActive(true);
                    break;

                case AuthenticationState.WaitingForUser:
                    statusText.text = "Waiting for browser authentication...";
                    statusText.color = new Color(0.4f, 0.7f, 1f, 1f);
                    SetSpinnerActive(true);
                    SetButtonsActive(false);
                    break;

                case AuthenticationState.Success:
                    statusText.text = "Authentication successful!";
                    statusText.color = new Color(0.3f, 0.8f, 0.3f, 1f);
                    SetSpinnerActive(false);
                    SetButtonsActive(false);
                    break;

                case AuthenticationState.Error:
                    statusText.text = $"Authentication failed: {lastError}";
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
            return !string.IsNullOrEmpty(npcManager?.ApiKey);
        }

        public void ForceShowAuthUI()
        {
            CheckAuthenticationStatus();
        }
    }
}