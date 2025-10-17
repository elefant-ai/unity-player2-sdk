using System;
using System.Text;
using Newtonsoft.Json;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;

namespace player2_sdk
{
    [Serializable]
    public class ImageGenerateRequest
    {
        public string prompt;
        public int? width;
        public int? height;
    }

    [Serializable]
    public class ImageGenerateResponse
    {
        public string image;
    }

    [Serializable]
    public class ImageEditRequest
    {
        public string image;
        public string prompt;
    }

    public class Player2ImageGenerator : MonoBehaviour
    {
        [Header("Manager Reference")]
        [SerializeField]
        [Tooltip("Reference to the NpcManager for API access and authentication")]
        private NpcManager npcManager;

        [Header("UI References")]
        [SerializeField]
        [Tooltip("Optional: Input field for users to enter image prompts")]
        public TMP_InputField promptInputField;

        [Tooltip("Optional: Automatically place the texture here")]
        [SerializeField]
        public UnityEngine.UI.RawImage targetRawImage;

        [SerializeField]
        [Tooltip("Optional: Toggle to switch between generate and edit modes")]
        public UnityEngine.UI.Toggle editModeToggle;

        [SerializeField]
        [Tooltip("Optional: Button to use the last generated image for editing")]
        public UnityEngine.UI.Button useLastImageButton;
        
        [Header("Image Settings")]
        [SerializeField]
        [Tooltip("Optional: Image width (128-1024). Leave as 0 to use server default")]
        private int defaultWidth = 0;

        [SerializeField]
        [Tooltip("Optional: Image height (128-1024). Leave as 0 to use server default")]
        private int defaultHeight = 0;

        [Header("Events")]
        [Tooltip("The generated texture")]
        public UnityEvent<Texture2D> onImageGenerated = new();

        // Store the last generated/edited image for editing purposes
        private string lastGeneratedImageBase64;

        
       

        private void Awake()
        {
            if (npcManager == null)
            {
                Debug.LogError("Player2ImageGenerator requires an NpcManager reference. Please assign it in the inspector.", this);
            }

            if (promptInputField != null)
            {
                promptInputField.onEndEdit.AddListener(OnPromptSubmitted);
                promptInputField.onEndEdit.AddListener(_ => promptInputField.text = string.Empty);
            }
            else
            {
                Debug.LogWarning("PromptInputField not assigned on Player2ImageGenerator; UI input disabled.", this);
            }

            if (targetRawImage != null)
            {
                onImageGenerated.AddListener((texture) =>
                {
                    targetRawImage.texture = texture;
                });
            }

            if (editModeToggle != null)
            {
                editModeToggle.onValueChanged.AddListener(OnEditModeToggled);
                UpdateEditModeUI(editModeToggle.isOn);
            }

            if (useLastImageButton != null)
            {
                editModeToggle.onValueChanged.AddListener(editMode =>
                {
                    useLastImageButton.gameObject.SetActive(editMode);
                });
                useLastImageButton.onClick.AddListener(OnUseLastImageButtonClicked);
                UpdateUseLastImageButton();
            }
        }

        private void OnPromptSubmitted(string prompt)
        {
            _ = ProcessImageRequestAsync(prompt);
        }

        private async Awaitable ProcessImageRequestAsync(string prompt)
        {
            if (string.IsNullOrWhiteSpace(prompt)) return;

            // Check if we're in edit mode
            bool isEditMode = editModeToggle != null && editModeToggle.isOn;

            if (isEditMode)
            {
                await EditLastImageAsync(prompt);
            }
            else
            {
                int? width = defaultWidth > 0 ? defaultWidth : null;
                int? height = defaultHeight > 0 ? defaultHeight : null;
                await GenerateImageAsync(prompt, width, height);
            }

            UpdateUseLastImageButton();
        }

        private void OnEditModeToggled(bool isEditMode)
        {
            UpdateEditModeUI(isEditMode);
        }

        private void UpdateEditModeUI(bool isEditMode)
        {
            if (promptInputField != null)
            {
                string placeholderText = isEditMode ? "Edit prompt..." : "Generate prompt...";
                var placeholder = promptInputField.placeholder;
                if (placeholder is TMP_Text tmpText)
                {
                    tmpText.text = placeholderText;
                }
            }
        }

        private void OnUseLastImageButtonClicked()
        {
            if (!string.IsNullOrEmpty(lastGeneratedImageBase64))
            {
                var texture = LoadImageFromBase64(lastGeneratedImageBase64);
                onImageGenerated?.Invoke(texture);
            }
        }

        private void UpdateUseLastImageButton()
        {
            if (useLastImageButton != null)
            {
                useLastImageButton.interactable = !string.IsNullOrEmpty(lastGeneratedImageBase64);
            }
        }

        /// <summary>
        /// Generates an image using the Player2 API
        /// </summary>
        /// <param name="prompt">The image prompt describing what to generate</param>
        /// <param name="width">Optional width (128-1024)</param>
        /// <param name="height">Optional height (128-1024)</param>
        /// <returns>The base64 image URL</returns>
        public async Awaitable<string> GenerateImageAsync(string prompt, int? width = null, int? height = null)
        {
            if (npcManager == null)
            {
                var error = "Cannot generate image: NpcManager is null";
                Debug.LogError(error);
                return null;
            }

            if (string.IsNullOrEmpty(prompt))
            {
                var error = "Cannot generate image: Prompt is required";
                Debug.LogError(error);
                return null;
            }

            // Validate dimensions if provided
            if (width.HasValue && (width.Value < 128 || width.Value > 1024))
            {
                var error = $"Invalid width: {width.Value}. Must be between 128 and 1024";
                Debug.LogError(error);
                return null;
            }

            if (height.HasValue && (height.Value < 128 || height.Value > 1024))
            {
                var error = $"Invalid height: {height.Value}. Must be between 128 and 1024";
                Debug.LogError(error);
                return null;
            }

            // Ensure we have a valid API key before attempting to generate (unless auth is bypassed for hosted scenarios)
            if (string.IsNullOrEmpty(npcManager.ApiKey) && !npcManager.ShouldSkipAuthentication())
            {
                var error = "Cannot generate image: No API key available. Please ensure authentication is completed first.";
                Debug.LogError(error);
                return null;
            }

            Debug.Log($"Generating image with prompt: '{prompt}', width: {width?.ToString() ?? "default"}, height: {height?.ToString() ?? "default"}");

            var requestData = new ImageGenerateRequest
            {
                prompt = prompt,
                width = width,
                height = height
            };

            var url = $"{npcManager.GetBaseUrl()}/image/generate";
            var json = JsonConvert.SerializeObject(requestData, npcManager.JsonSerializerSettings);
            var bodyRaw = Encoding.UTF8.GetBytes(json);

            using var request = new UnityWebRequest(url, "POST");
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();

            // Skip authentication if running on player2.game domain (cookies will handle auth)
            if (!npcManager.ShouldSkipAuthentication())
            {
                request.SetRequestHeader("Authorization", $"Bearer {npcManager.ApiKey}");
            }

            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Accept", "application/json");

            await request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    var response = JsonConvert.DeserializeObject<ImageGenerateResponse>(request.downloadHandler.text);
                    if (response != null && !string.IsNullOrEmpty(response.image))
                    {
                        Debug.Log($"Image generated successfully. Base64 URL length: {response.image.Length}");
                        lastGeneratedImageBase64 = response.image;
                        var texture = LoadImageFromBase64(response.image);
                        onImageGenerated?.Invoke(texture);
                        return response.image;
                    }
                    else
                    {
                        var error = "Failed to parse image generation response";
                        Debug.LogError(error);
                        return null;
                    }
                }
                catch (Exception ex)
                {
                    var error = $"Error parsing image generation response: {ex.Message}";
                    Debug.LogError(error);
                    return null;
                }
            }
            else
            {
                var error = $"Failed to generate image: {request.error} - Response: {request.downloadHandler.text}";
                Debug.LogError(error);
                return null;
            }
        }

        /// <summary>
        /// Generates an image with just a prompt (using default dimensions)
        /// </summary>
        public async Awaitable<string> GenerateImageAsync(string prompt)
        {
            return await GenerateImageAsync(prompt, null, null);
        }

        /// <summary>
        /// Edits an existing image using the Player2 API
        /// </summary>
        /// <param name="baseImage">The base64 encoded image to edit</param>
        /// <param name="prompt">The edit prompt describing the desired changes</param>
        /// <returns>The base64 edited image URL</returns>
        public async Awaitable<string> EditImageAsync(string baseImage, string prompt)
        {
            if (npcManager == null)
            {
                var error = "Cannot edit image: NpcManager is null";
                Debug.LogError(error);
                return null;
            }

            if (string.IsNullOrEmpty(baseImage))
            {
                var error = "Cannot edit image: Base image is required";
                Debug.LogError(error);
                return null;
            }

            if (string.IsNullOrEmpty(prompt))
            {
                var error = "Cannot edit image: Prompt is required";
                Debug.LogError(error);
                return null;
            }

            // Ensure we have a valid API key before attempting to edit (unless auth is bypassed for hosted scenarios)
            if (string.IsNullOrEmpty(npcManager.ApiKey) && !npcManager.ShouldSkipAuthentication())
            {
                var error = "Cannot edit image: No API key available. Please ensure authentication is completed first.";
                Debug.LogError(error);
                return null;
            }

            Debug.Log($"Editing image with prompt: '{prompt}'");

            var requestData = new ImageEditRequest
            {
                image = baseImage,
                prompt = prompt
            };

            var url = $"{npcManager.GetBaseUrl()}/image/edit";
            var json = JsonConvert.SerializeObject(requestData, npcManager.JsonSerializerSettings);
            var bodyRaw = Encoding.UTF8.GetBytes(json);

            using var request = new UnityWebRequest(url, "POST");
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();

            // Skip authentication if running on player2.game domain (cookies will handle auth)
            if (!npcManager.ShouldSkipAuthentication())
            {
                request.SetRequestHeader("Authorization", $"Bearer {npcManager.ApiKey}");
            }

            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Accept", "application/json");

            await request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    var response = JsonConvert.DeserializeObject<ImageGenerateResponse>(request.downloadHandler.text);
                    if (response != null && !string.IsNullOrEmpty(response.image))
                    {
                        Debug.Log($"Image edited successfully. Base64 URL length: {response.image.Length}");
                        lastGeneratedImageBase64 = response.image;
                        var texture = LoadImageFromBase64(response.image);
                        onImageGenerated?.Invoke(texture);
                        return response.image;
                    }
                    else
                    {
                        var error = "Failed to parse image edit response";
                        Debug.LogError(error);
                        return null;
                    }
                }
                catch (Exception ex)
                {
                    var error = $"Error parsing image edit response: {ex.Message}";
                    Debug.LogError(error);
                    return null;
                }
            }
            else
            {
                var error = $"Failed to edit image: {request.error} - Response: {request.downloadHandler.text}";
                Debug.LogError(error);
                return null;
            }
        }

        /// <summary>
        /// Edits the last generated/edited image with a new prompt
        /// </summary>
        /// <param name="prompt">The edit prompt describing the desired changes</param>
        /// <returns>The base64 edited image URL</returns>
        public async Awaitable<string> EditLastImageAsync(string prompt)
        {
            if (string.IsNullOrEmpty(lastGeneratedImageBase64))
            {
                var error = "Cannot edit image: No previous image available. Generate an image first.";
                Debug.LogError(error);
                return null;
            }

            return await EditImageAsync(lastGeneratedImageBase64, prompt);
        }

        /// <summary>
        /// Gets the last generated/edited image as base64
        /// </summary>
        public string GetLastGeneratedImage()
        {
            return lastGeneratedImageBase64;
        }


        private Texture2D LoadImageFromBase64(string base64Url)
        {
            try
            {
                // Extract base64 data from data URL (format: data:image/png;base64,...)
                string base64Data = base64Url;
                if (base64Url.Contains(","))
                {
                    int commaIndex = base64Url.IndexOf(',');
                    base64Data = base64Url.Substring(commaIndex + 1);
                }

                // Convert base64 to byte array
                byte[] imageBytes = System.Convert.FromBase64String(base64Data);

                // Create texture from bytes
                Texture2D texture = new Texture2D(2, 2);
                 texture.LoadImage(imageBytes);

                 return texture;
                 // Display in RawImage

            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error loading image from base64: {ex.Message}");
                throw ex;
                
            }
        }
    }
}
