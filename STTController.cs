using System;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace player2_sdk
{
    public class STTController : MonoBehaviour
    {
        [Header("UI References")] [SerializeField]
        private Button recordButton;

        [SerializeField] private Image recordButtonImage;
        [SerializeField] private TextMeshProUGUI buttonText;
        [SerializeField] private TextMeshProUGUI transcriptText;
        [SerializeField] private Player2STT sttComponent;

        [Header("Button Appearance")] [SerializeField]
        private Sprite circleSprite;

        [SerializeField] private Sprite squareSprite;
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color recordingColor = Color.red;
        [SerializeField] private Color waitingColor = Color.yellow;

        [Header("Button Text")] [SerializeField]
        private string recordText = "REC";

        [SerializeField] private string stopText = "STOP";
        [SerializeField] private string connectingText = "...";
        [SerializeField] private string processingText = "WAIT";

        [Header("Transcript Settings")] [SerializeField]
        private bool showTimestamps = true;

        [SerializeField] private bool showTranscriptNumbers = true;
        [SerializeField] private int maxTranscripts = 100;

        // Transcript accumulation
        private readonly StringBuilder transcriptBuilder = new();

        // Fallback for regular Text components
        private Text buttonTextFallback;

        private bool isCurrentlyRecording;
        private int transcriptCounter = 1;
        private Text transcriptTextFallback;

        private void Start()
        {
            AutoConfigureComponents();
            CreateDefaultSprites();
            SetupEventListeners();
            SetButtonState(ButtonState.Normal);

            ShowWelcomeMessage();
        }

        private void ShowWelcomeMessage()
        {
            var welcomeMessage = "SPEECH-TO-TEXT READY\n" +
                                 "Click the button to start recording.\n" +
                                 "Transcripts will appear here in real-time.\n" +
                                 "========================================\n";

            transcriptBuilder.AppendLine(welcomeMessage);
            UpdateTranscriptDisplay();
        }

        private void AutoConfigureComponents()
        {
            if (recordButton == null)
                recordButton = GetComponent<Button>() ?? GetComponentInChildren<Button>();

            if (recordButton != null && recordButtonImage == null)
                recordButtonImage = recordButton.GetComponent<Image>();

            if (sttComponent == null)
                sttComponent = GetComponent<Player2STT>() ?? FindAnyObjectByType<Player2STT>();

            if (buttonText == null && recordButton != null)
            {
                buttonText = recordButton.GetComponentInChildren<TextMeshProUGUI>();

                if (buttonText == null) buttonTextFallback = recordButton.GetComponentInChildren<Text>();

                if (buttonText == null && buttonTextFallback == null)
                {
                    buttonText = recordButton.GetComponent<TextMeshProUGUI>();
                    if (buttonText == null) buttonTextFallback = recordButton.GetComponent<Text>();
                }

                Debug.Log(
                    $"Button text search result: {(buttonText != null ? $"Found TextMeshPro: {buttonText.name}" : buttonTextFallback != null ? $"Found Text: {buttonTextFallback.name}" : "Not found - button will work but text won't change")}");
            }

            if (transcriptText == null)
            {
                var allTexts = FindObjectsByType<TextMeshProUGUI>(FindObjectsSortMode.None);
                foreach (var text in allTexts)
                    if (text != buttonText && (text.name.ToLower().Contains("transcript") ||
                                               text.name.ToLower().Contains("output")))
                    {
                        transcriptText = text;
                        break;
                    }

                if (transcriptText == null)
                    foreach (var text in allTexts)
                        if (text != buttonText)
                        {
                            transcriptText = text;
                            break;
                        }

                if (transcriptText == null)
                {
                    var allRegularTexts = FindObjectsByType<Text>(FindObjectsSortMode.None);
                    foreach (var text in allRegularTexts)
                        if (text != buttonTextFallback && (text.name.ToLower().Contains("transcript") ||
                                                           text.name.ToLower().Contains("output")))
                        {
                            transcriptTextFallback = text;
                            Debug.Log(
                                $"STTController: Using regular Text component '{transcriptTextFallback.name}' as fallback for transcript text.");
                            break;
                        }

                    if (transcriptTextFallback == null)
                        foreach (var text in allRegularTexts)
                            if (text != buttonTextFallback)
                            {
                                transcriptTextFallback = text;
                                Debug.Log(
                                    $"STTController: Using regular Text component '{transcriptTextFallback.name}' as fallback for transcript text.");
                                break;
                            }
                }
            }

            if (recordButton == null || sttComponent == null)
                Debug.LogWarning("STTController: Some components could not be auto-configured. Check inspector.");
        }

        private void CreateDefaultSprites()
        {
            if (circleSprite == null)
            {
                circleSprite = CreateCircleSprite();
                Debug.Log("Created default circle sprite");
            }

            if (squareSprite == null)
            {
                squareSprite = CreateSquareSprite();
                Debug.Log("Created default square sprite");
            }
        }

        private Sprite CreateCircleSprite()
        {
            var texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            var center = new Vector2(32, 32);
            var radius = 28;

            for (var x = 0; x < 64; x++)
            for (var y = 0; y < 64; y++)
            {
                var distance = Vector2.Distance(new Vector2(x, y), center);
                var color = distance <= radius ? Color.white : Color.clear;
                texture.SetPixel(x, y, color);
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f));
        }

        private Sprite CreateSquareSprite()
        {
            var texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);

            for (var x = 0; x < 64; x++)
            for (var y = 0; y < 64; y++)
            {
                var color = x >= 8 && x < 56 && y >= 8 && y < 56 ? Color.white : Color.clear;
                texture.SetPixel(x, y, color);
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f));
        }

        private void SetupEventListeners()
        {
            if (recordButton != null) recordButton.onClick.AddListener(OnRecordButtonClicked);

            if (sttComponent != null)
            {
                sttComponent.OnSTTReceived.AddListener(OnTranscriptReceived);
                sttComponent.OnSTTFailed.AddListener(OnSTTFailed);
                sttComponent.OnListeningStarted.AddListener(OnListeningStarted);
                sttComponent.OnListeningStopped.AddListener(OnListeningStopped);
            }
        }

        private void OnRecordButtonClicked()
        {
            if (sttComponent == null) return;

            if (isCurrentlyRecording)
                StopRecording();
            else
                StartRecording();
        }

        private void StartRecording()
        {
            if (sttComponent != null)
            {
                SetButtonState(ButtonState.Connecting);
                sttComponent.StartSTT();
            }
        }

        private void StopRecording()
        {
            if (sttComponent != null && isCurrentlyRecording)
            {
                sttComponent.StopSTT();
                isCurrentlyRecording = false;
                SetButtonState(ButtonState.Normal);
            }
        }

        private void OnTranscriptReceived(string transcript)
        {
            if (string.IsNullOrEmpty(transcript)) return;

            var entry = "";

            if (showTimestamps)
            {
                var timestamp = DateTime.Now.ToString("HH:mm:ss");
                entry += $"[{timestamp}] ";
            }

            if (showTranscriptNumbers) entry += $"#{transcriptCounter:D2}: ";

            entry += transcript;

            transcriptBuilder.AppendLine(entry);
            transcriptCounter++;

            if (transcriptCounter > maxTranscripts) TrimOldTranscripts();

            UpdateTranscriptDisplay();
        }

        private void TrimOldTranscripts()
        {
            var currentText = transcriptBuilder.ToString();
            var lines = currentText.Split('\n');

            if (lines.Length > maxTranscripts)
            {
                var recentLines = lines.Skip(lines.Length - maxTranscripts);
                transcriptBuilder.Clear();
                transcriptBuilder.Append(string.Join("\n", recentLines));
            }
        }

        private void UpdateTranscriptDisplay()
        {
            var fullTranscript = transcriptBuilder.ToString();

            if (transcriptText != null)
                transcriptText.text = fullTranscript;
            else if (transcriptTextFallback != null)
                transcriptTextFallback.text = fullTranscript;
            else
                Debug.LogError("No transcript text component found. Please assign one in the inspector.");
        }

        public void ClearTranscripts()
        {
            transcriptBuilder.Clear();
            transcriptCounter = 1;
            UpdateTranscriptDisplay();
        }

        private void OnSTTFailed(string error, int code)
        {
            Debug.LogError($"STT failed: {error} (Code: {code})");

            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            var errorEntry = $"[{timestamp}] ERROR: {error}\n";

            transcriptBuilder.AppendLine(errorEntry);
            UpdateTranscriptDisplay();

            isCurrentlyRecording = false;
            SetButtonState(ButtonState.Normal);
        }

        private void OnListeningStarted()
        {
            isCurrentlyRecording = true;
            SetButtonState(ButtonState.Recording);
        }

        private void OnListeningStopped()
        {
            isCurrentlyRecording = false;
            SetButtonState(ButtonState.Normal);
        }


        private void SetButtonState(ButtonState state)
        {
            if (recordButtonImage == null) return;

            switch (state)
            {
                case ButtonState.Normal:
                    recordButtonImage.color = normalColor;
                    recordButtonImage.sprite = circleSprite;
                    SetButtonText(recordText);
                    break;

                case ButtonState.Connecting:
                    recordButtonImage.color = waitingColor;
                    recordButtonImage.sprite = circleSprite;
                    SetButtonText(connectingText);
                    break;

                case ButtonState.Recording:
                    recordButtonImage.color = recordingColor;
                    recordButtonImage.sprite = squareSprite;
                    SetButtonText(stopText);
                    break;

                case ButtonState.Processing:
                    recordButtonImage.color = waitingColor;
                    recordButtonImage.sprite = squareSprite;
                    SetButtonText(processingText);
                    break;
            }
        }

        private void SetButtonText(string text)
        {
            if (buttonText != null)
                buttonText.text = text;
            else if (buttonTextFallback != null) buttonTextFallback.text = text;
        }

        private enum ButtonState
        {
            Normal, // Circle, "Record", white
            Connecting, // Circle, "Connecting...", yellow
            Recording, // Square, "Stop", red
            Processing // Square, "Processing...", yellow
        }
    }
}