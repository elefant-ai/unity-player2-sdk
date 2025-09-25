using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace player2_sdk
{
    [System.Serializable]
    public class AuthUIStyles
    {
        [Header("Colors")]
        public Color backgroundColor = new Color(0, 0, 0, 0.8f);
        public Color panelColor = new Color(0.2f, 0.2f, 0.2f, 0.95f);
        public Color primaryTextColor = Color.white;
        public Color secondaryTextColor = new Color(0.8f, 0.8f, 0.8f);
        public Color buttonColor = new Color(0.2f, 0.6f, 1f);
        public Color errorColor = new Color(1f, 0.3f, 0.3f);

        [Header("Fonts")]
        public TMP_FontAsset titleFont;
        public TMP_FontAsset bodyFont;

        [Header("Sizes")]
        public float titleFontSize = 24f;
        public float bodyFontSize = 16f;
        public float codeFontSize = 20f;
    }

    /// <summary>
    /// DEPRECATED: This class is no longer needed. The new AuthenticationUI automatically creates its own UI.
    /// Use AuthenticationUI.Setup(npcManager) instead for much simpler integration.
    /// </summary>
    [System.Obsolete("Use AuthenticationUI.Setup(npcManager) instead. This component is no longer needed as AuthenticationUI creates its own UI automatically.")]
    public class AuthenticationUISetup : MonoBehaviour
    {
        [Header("Configuration")]
        public AuthUIStyles styles = new AuthUIStyles();
        public Vector2 panelSize = new Vector2(400, 300);

        [Header("References")]
        public NpcManager npcManager;

        [ContextMenu("Create Authentication UI")]
        public void CreateAuthenticationUI()
        {
            GameObject authUIObject = new GameObject("AuthenticationUI");
            authUIObject.transform.SetParent(transform);

            AuthenticationUI authUI = authUIObject.AddComponent<AuthenticationUI>();
            
            // Create Canvas
            Canvas canvas = CreateOverlayCanvas(authUIObject);
            
            // Create main panel
            GameObject panel = CreateMainPanel(canvas.transform);
            
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
            GameObject canvasObj = new GameObject("AuthOverlay");
            canvasObj.transform.SetParent(parent.transform);
            
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            
            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            
            canvasObj.AddComponent<GraphicRaycaster>();
            
            // Background
            GameObject bg = new GameObject("Background");
            bg.transform.SetParent(canvasObj.transform);
            
            RectTransform bgRect = bg.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            
            Image bgImage = bg.AddComponent<Image>();
            bgImage.color = styles.backgroundColor;
            
            return canvas;
        }

        private GameObject CreateMainPanel(Transform parent)
        {
            GameObject panel = new GameObject("AuthPanel");
            panel.transform.SetParent(parent);
            
            RectTransform panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = panelSize;
            panelRect.anchoredPosition = Vector2.zero;
            
            Image panelImage = panel.AddComponent<Image>();
            panelImage.color = styles.panelColor;
            
            // Add some padding with VerticalLayoutGroup
            VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
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
            GameObject statusObj = new GameObject("StatusText");
            statusObj.transform.SetParent(parent);
            
            TextMeshProUGUI statusText = statusObj.AddComponent<TextMeshProUGUI>();
            statusText.text = "Checking authentication...";
            statusText.font = styles.titleFont;
            statusText.fontSize = styles.titleFontSize;
            statusText.color = styles.primaryTextColor;
            statusText.alignment = TextAlignmentOptions.Center;
            
            RectTransform statusRect = statusObj.GetComponent<RectTransform>();
            statusRect.sizeDelta = new Vector2(0, 40);
        }

        private void CreateUserCodePanel(Transform parent)
        {
            GameObject codePanel = new GameObject("UserCodePanel");
            codePanel.transform.SetParent(parent);
            codePanel.SetActive(false);
            
            VerticalLayoutGroup codeLayout = codePanel.AddComponent<VerticalLayoutGroup>();
            codeLayout.spacing = 10f;
            codeLayout.childAlignment = TextAnchor.MiddleCenter;
            
            RectTransform codePanelRect = codePanel.GetComponent<RectTransform>();
            codePanelRect.sizeDelta = new Vector2(0, 80);
            
            // Instructions
            GameObject instructionsObj = new GameObject("Instructions");
            instructionsObj.transform.SetParent(codePanel.transform);
            
            TextMeshProUGUI instructionsText = instructionsObj.AddComponent<TextMeshProUGUI>();
            instructionsText.text = "Enter this code on the authentication page:";
            instructionsText.font = styles.bodyFont;
            instructionsText.fontSize = styles.bodyFontSize;
            instructionsText.color = styles.secondaryTextColor;
            instructionsText.alignment = TextAlignmentOptions.Center;
            
            // User Code
            GameObject userCodeObj = new GameObject("UserCodeText");
            userCodeObj.transform.SetParent(codePanel.transform);
            
            TextMeshProUGUI userCodeText = userCodeObj.AddComponent<TextMeshProUGUI>();
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
            GameObject browserButtonObj = new GameObject("OpenBrowserButton");
            browserButtonObj.transform.SetParent(parent);
            browserButtonObj.SetActive(false);
            
            Button browserButton = browserButtonObj.AddComponent<Button>();
            Image browserButtonImage = browserButtonObj.AddComponent<Image>();
            browserButtonImage.color = styles.buttonColor;
            
            RectTransform browserButtonRect = browserButtonObj.GetComponent<RectTransform>();
            browserButtonRect.sizeDelta = new Vector2(200, 40);
            
            GameObject browserButtonTextObj = new GameObject("Text");
            browserButtonTextObj.transform.SetParent(browserButtonObj.transform);
            
            TextMeshProUGUI browserButtonText = browserButtonTextObj.AddComponent<TextMeshProUGUI>();
            browserButtonText.text = "Open Browser";
            browserButtonText.font = styles.bodyFont;
            browserButtonText.fontSize = styles.bodyFontSize;
            browserButtonText.color = Color.white;
            browserButtonText.alignment = TextAlignmentOptions.Center;
            
            RectTransform browserTextRect = browserButtonTextObj.GetComponent<RectTransform>();
            browserTextRect.anchorMin = Vector2.zero;
            browserTextRect.anchorMax = Vector2.one;
            browserTextRect.offsetMin = Vector2.zero;
            browserTextRect.offsetMax = Vector2.zero;
            
            browserButton.targetGraphic = browserButtonImage;
            
            // Retry Button
            GameObject retryButtonObj = new GameObject("RetryButton");
            retryButtonObj.transform.SetParent(parent);
            retryButtonObj.SetActive(false);
            
            Button retryButton = retryButtonObj.AddComponent<Button>();
            Image retryButtonImage = retryButtonObj.AddComponent<Image>();
            retryButtonImage.color = styles.buttonColor;
            
            RectTransform retryButtonRect = retryButtonObj.GetComponent<RectTransform>();
            retryButtonRect.sizeDelta = new Vector2(200, 40);
            
            GameObject retryButtonTextObj = new GameObject("Text");
            retryButtonTextObj.transform.SetParent(retryButtonObj.transform);
            
            TextMeshProUGUI retryButtonText = retryButtonTextObj.AddComponent<TextMeshProUGUI>();
            retryButtonText.text = "Retry";
            retryButtonText.font = styles.bodyFont;
            retryButtonText.fontSize = styles.bodyFontSize;
            retryButtonText.color = Color.white;
            retryButtonText.alignment = TextAlignmentOptions.Center;
            
            RectTransform retryTextRect = retryButtonTextObj.GetComponent<RectTransform>();
            retryTextRect.anchorMin = Vector2.zero;
            retryTextRect.anchorMax = Vector2.one;
            retryTextRect.offsetMin = Vector2.zero;
            retryTextRect.offsetMax = Vector2.zero;
            
            retryButton.targetGraphic = retryButtonImage;
        }

        private void CreateSpinner(Transform parent)
        {
            GameObject spinnerObj = new GameObject("ProgressSpinner");
            spinnerObj.transform.SetParent(parent);
            
            Image spinnerImage = spinnerObj.AddComponent<Image>();
            // You would typically assign a spinner sprite here
            spinnerImage.color = styles.primaryTextColor;
            
            RectTransform spinnerRect = spinnerObj.GetComponent<RectTransform>();
            spinnerRect.sizeDelta = new Vector2(32, 32);
        }

        private void CreateErrorPanel(Transform parent)
        {
            GameObject errorPanel = new GameObject("ErrorPanel");
            errorPanel.transform.SetParent(parent);
            errorPanel.SetActive(false);
            
            RectTransform errorRect = errorPanel.GetComponent<RectTransform>();
            errorRect.sizeDelta = new Vector2(0, 60);
            
            Image errorBg = errorPanel.AddComponent<Image>();
            errorBg.color = new Color(styles.errorColor.r, styles.errorColor.g, styles.errorColor.b, 0.2f);
            
            GameObject errorTextObj = new GameObject("ErrorText");
            errorTextObj.transform.SetParent(errorPanel.transform);
            
            TextMeshProUGUI errorText = errorTextObj.AddComponent<TextMeshProUGUI>();
            errorText.text = "Error occurred during authentication";
            errorText.font = styles.bodyFont;
            errorText.fontSize = styles.bodyFontSize;
            errorText.color = styles.errorColor;
            errorText.alignment = TextAlignmentOptions.Center;
            
            RectTransform errorTextRect = errorTextObj.GetComponent<RectTransform>();
            errorTextRect.anchorMin = Vector2.zero;
            errorTextRect.anchorMax = Vector2.one;
            errorTextRect.offsetMin = new Vector2(10, 0);
            errorTextRect.offsetMax = new Vector2(-10, 0);
        }

        private void AssignUIReferences(AuthenticationUI authUI, Canvas canvas, GameObject panel)
        {
            authUI.npcManager = npcManager;
            
            Debug.Log("AuthenticationUI configured. Note: The new AuthenticationUI automatically creates its own UI, so manual UI setup is no longer needed.");
            Debug.Log("Consider using AuthenticationUI.Setup(npcManager) instead for simpler integration.");
        }
    }
}
