using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace player2_sdk
{
    [Serializable]
    public class AuthUIStyles
    {
        [Header("Colors")] public Color backgroundColor = new(0, 0, 0, 0.8f);

        public Color panelColor = new(0.2f, 0.2f, 0.2f, 0.95f);
        public Color primaryTextColor = Color.white;
        public Color secondaryTextColor = new(0.8f, 0.8f, 0.8f);
        public Color buttonColor = new(0.2f, 0.6f, 1f);
        public Color errorColor = new(1f, 0.3f, 0.3f);

        [Header("Fonts")] public TMP_FontAsset titleFont;

        public TMP_FontAsset bodyFont;

        [Header("Sizes")] public float titleFontSize = 24f;

        public float bodyFontSize = 16f;
        public float codeFontSize = 20f;
    }

    /// <summary>
    ///     DEPRECATED: This class is no longer needed. The new AuthenticationUI automatically creates its own UI.
    ///     Use AuthenticationUI.Setup(npcManager) instead for much simpler integration.
    /// </summary>
    [Obsolete(
        "Use AuthenticationUI.Setup(npcManager) instead. This component is no longer needed as AuthenticationUI creates its own UI automatically.")]
    public class AuthenticationUISetup : MonoBehaviour
    {
        [Header("Configuration")] public AuthUIStyles styles = new();

        public Vector2 panelSize = new(400, 300);

        [Header("References")] public NpcManager npcManager;

        [ContextMenu("Create Authentication UI")]
        public void CreateAuthenticationUI()
        {
            var authUIObject = new GameObject("AuthenticationUI");
            authUIObject.transform.SetParent(transform);

            var authUI = authUIObject.AddComponent<AuthenticationUI>();

            // Create Canvas
            var canvas = CreateOverlayCanvas(authUIObject);

            // Create main panel
            var panel = CreateMainPanel(canvas.transform);

            // Create UI elements
            CreateStatusText(panel.transform);
            CreateUserCodePanel(panel.transform);
            CreateButtons(panel.transform);
            CreateSpinner(panel.transform);
            CreateErrorPanel(panel.transform);

            // Assign references to AuthenticationUI component
            AssignUIReferences(authUI, canvas, panel);

            Debug.Log("Authentication UI created successfully!");
        }

        private Canvas CreateOverlayCanvas(GameObject parent)
        {
            var canvasObj = new GameObject("AuthOverlay");
            canvasObj.transform.SetParent(parent.transform);

            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;

            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            canvasObj.AddComponent<GraphicRaycaster>();

            // Background
            var bg = new GameObject("Background");
            bg.transform.SetParent(canvasObj.transform);

            var bgRect = bg.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            var bgImage = bg.AddComponent<Image>();
            bgImage.color = styles.backgroundColor;

            return canvas;
        }

        private GameObject CreateMainPanel(Transform parent)
        {
            var panel = new GameObject("AuthPanel");
            panel.transform.SetParent(parent);

            var panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = panelSize;
            panelRect.anchoredPosition = Vector2.zero;

            var panelImage = panel.AddComponent<Image>();
            panelImage.color = styles.panelColor;

            // Add some padding with VerticalLayoutGroup
            var layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(20, 20, 20, 20);
            layout.spacing = 15f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlHeight = false;
            layout.childControlWidth = false;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            return panel;
        }

        private void CreateStatusText(Transform parent)
        {
            var statusObj = new GameObject("StatusText");
            statusObj.transform.SetParent(parent);

            var statusText = statusObj.AddComponent<TextMeshProUGUI>();
            statusText.text = "Checking authentication...";
            statusText.font = styles.titleFont;
            statusText.fontSize = styles.titleFontSize;
            statusText.color = styles.primaryTextColor;
            statusText.alignment = TextAlignmentOptions.Center;

            var statusRect = statusObj.GetComponent<RectTransform>();
            statusRect.sizeDelta = new Vector2(0, 40);
        }

        private void CreateUserCodePanel(Transform parent)
        {
            var codePanel = new GameObject("UserCodePanel");
            codePanel.transform.SetParent(parent);
            codePanel.SetActive(false);

            var codeLayout = codePanel.AddComponent<VerticalLayoutGroup>();
            codeLayout.spacing = 10f;
            codeLayout.childAlignment = TextAnchor.MiddleCenter;

            var codePanelRect = codePanel.GetComponent<RectTransform>();
            codePanelRect.sizeDelta = new Vector2(0, 80);

            // Instructions
            var instructionsObj = new GameObject("Instructions");
            instructionsObj.transform.SetParent(codePanel.transform);

            var instructionsText = instructionsObj.AddComponent<TextMeshProUGUI>();
            instructionsText.text = "Enter this code on the authentication page:";
            instructionsText.font = styles.bodyFont;
            instructionsText.fontSize = styles.bodyFontSize;
            instructionsText.color = styles.secondaryTextColor;
            instructionsText.alignment = TextAlignmentOptions.Center;

            // User Code
            var userCodeObj = new GameObject("UserCodeText");
            userCodeObj.transform.SetParent(codePanel.transform);

            var userCodeText = userCodeObj.AddComponent<TextMeshProUGUI>();
            userCodeText.text = "XXXX-XXXX";
            userCodeText.font = styles.bodyFont;
            userCodeText.fontSize = styles.codeFontSize;
            userCodeText.color = styles.primaryTextColor;
            userCodeText.alignment = TextAlignmentOptions.Center;
            userCodeText.fontStyle = FontStyles.Bold;
        }

        private void CreateButtons(Transform parent)
        {
            // Open Browser Button
            var browserButtonObj = new GameObject("OpenBrowserButton");
            browserButtonObj.transform.SetParent(parent);
            browserButtonObj.SetActive(false);

            var browserButton = browserButtonObj.AddComponent<Button>();
            var browserButtonImage = browserButtonObj.AddComponent<Image>();
            browserButtonImage.color = styles.buttonColor;

            var browserButtonRect = browserButtonObj.GetComponent<RectTransform>();
            browserButtonRect.sizeDelta = new Vector2(200, 40);

            var browserButtonTextObj = new GameObject("Text");
            browserButtonTextObj.transform.SetParent(browserButtonObj.transform);

            var browserButtonText = browserButtonTextObj.AddComponent<TextMeshProUGUI>();
            browserButtonText.text = "Open Browser";
            browserButtonText.font = styles.bodyFont;
            browserButtonText.fontSize = styles.bodyFontSize;
            browserButtonText.color = Color.white;
            browserButtonText.alignment = TextAlignmentOptions.Center;

            var browserTextRect = browserButtonTextObj.GetComponent<RectTransform>();
            browserTextRect.anchorMin = Vector2.zero;
            browserTextRect.anchorMax = Vector2.one;
            browserTextRect.offsetMin = Vector2.zero;
            browserTextRect.offsetMax = Vector2.zero;

            browserButton.targetGraphic = browserButtonImage;

            // Retry Button
            var retryButtonObj = new GameObject("RetryButton");
            retryButtonObj.transform.SetParent(parent);
            retryButtonObj.SetActive(false);

            var retryButton = retryButtonObj.AddComponent<Button>();
            var retryButtonImage = retryButtonObj.AddComponent<Image>();
            retryButtonImage.color = styles.buttonColor;

            var retryButtonRect = retryButtonObj.GetComponent<RectTransform>();
            retryButtonRect.sizeDelta = new Vector2(200, 40);

            var retryButtonTextObj = new GameObject("Text");
            retryButtonTextObj.transform.SetParent(retryButtonObj.transform);

            var retryButtonText = retryButtonTextObj.AddComponent<TextMeshProUGUI>();
            retryButtonText.text = "Retry";
            retryButtonText.font = styles.bodyFont;
            retryButtonText.fontSize = styles.bodyFontSize;
            retryButtonText.color = Color.white;
            retryButtonText.alignment = TextAlignmentOptions.Center;

            var retryTextRect = retryButtonTextObj.GetComponent<RectTransform>();
            retryTextRect.anchorMin = Vector2.zero;
            retryTextRect.anchorMax = Vector2.one;
            retryTextRect.offsetMin = Vector2.zero;
            retryTextRect.offsetMax = Vector2.zero;

            retryButton.targetGraphic = retryButtonImage;
        }

        private void CreateSpinner(Transform parent)
        {
            var spinnerObj = new GameObject("ProgressSpinner");
            spinnerObj.transform.SetParent(parent);

            var spinnerImage = spinnerObj.AddComponent<Image>();
            // You would typically assign a spinner sprite here
            spinnerImage.color = styles.primaryTextColor;

            var spinnerRect = spinnerObj.GetComponent<RectTransform>();
            spinnerRect.sizeDelta = new Vector2(32, 32);
        }

        private void CreateErrorPanel(Transform parent)
        {
            var errorPanel = new GameObject("ErrorPanel");
            errorPanel.transform.SetParent(parent);
            errorPanel.SetActive(false);

            var errorRect = errorPanel.GetComponent<RectTransform>();
            errorRect.sizeDelta = new Vector2(0, 60);

            var errorBg = errorPanel.AddComponent<Image>();
            errorBg.color = new Color(styles.errorColor.r, styles.errorColor.g, styles.errorColor.b, 0.2f);

            var errorTextObj = new GameObject("ErrorText");
            errorTextObj.transform.SetParent(errorPanel.transform);

            var errorText = errorTextObj.AddComponent<TextMeshProUGUI>();
            errorText.text = "Error occurred during authentication";
            errorText.font = styles.bodyFont;
            errorText.fontSize = styles.bodyFontSize;
            errorText.color = styles.errorColor;
            errorText.alignment = TextAlignmentOptions.Center;

            var errorTextRect = errorTextObj.GetComponent<RectTransform>();
            errorTextRect.anchorMin = Vector2.zero;
            errorTextRect.anchorMax = Vector2.one;
            errorTextRect.offsetMin = new Vector2(10, 0);
            errorTextRect.offsetMax = new Vector2(-10, 0);
        }

        private void AssignUIReferences(AuthenticationUI authUI, Canvas canvas, GameObject panel)
        {
            authUI.npcManager = npcManager;

            Debug.Log(
                "AuthenticationUI configured. Note: The new AuthenticationUI automatically creates its own UI, so manual UI setup is no longer needed.");
            Debug.Log("Consider using AuthenticationUI.Setup(npcManager) instead for simpler integration.");
        }
    }
}