#if UNITY_WEBGL
namespace player2_sdk
{
    using System;
    using System.Collections;
    using UnityEngine;
    using UnityEngine.Networking;

    /// <summary>
    /// WebGL-specific audio player implementation that avoids file:// protocol issues
    /// </summary>
    public class WebGLAudioPlayer : IAudioPlayer
    {
        public IEnumerator PlayAudioFromDataUrl(string dataUrl, AudioSource audioSource, string identifier)
        {
            // Validate input parameters
            if (string.IsNullOrEmpty(dataUrl))
            {
                Debug.LogError($"Cannot play audio for {identifier}: dataUrl is null or empty");
                yield break;
            }

            // Check if this is a valid data URL format
            if (!dataUrl.StartsWith("data:"))
            {
                Debug.LogError($"Cannot play audio for {identifier}: invalid data URL format (missing 'data:' prefix)");
                yield break;
            }

            // Find the comma that separates metadata from base64 data
            int commaIndex = dataUrl.IndexOf(',');
            if (commaIndex == -1 || commaIndex == dataUrl.Length - 1)
            {
                Debug.LogError($"Cannot play audio for {identifier}: invalid data URL format (missing comma or no data after comma)");
                yield break;
            }

            // Extract base64 data from data URL
            string base64String = dataUrl.Substring(commaIndex + 1);

            // Validate that we have base64 data
            if (string.IsNullOrEmpty(base64String))
            {
                Debug.LogError($"Cannot play audio for {identifier}: no base64 data found in data URL");
                yield break;
            }

            // Additional validation: check for valid base64 characters
            if (!IsValidBase64String(base64String))
            {
                Debug.LogError($"Cannot play audio for {identifier}: extracted string is not valid Base64");
                yield break;
            }

            byte[] audioBytes;
            try
            {
                // Decode to bytes
                audioBytes = Convert.FromBase64String(base64String);
            }
            catch (FormatException ex)
            {
                Debug.LogError($"Cannot play audio for {identifier}: Base64 decoding failed: {ex.Message}");
                yield break;
            }

            // For WebGL, we need to use a different approach since file:// protocol doesn't work
            // We'll create an upload handler with the raw bytes and use UnityWebRequest to process it
            using (var request = new UnityWebRequest())
            {
                request.url = "data:audio/mpeg;base64," + base64String;
                request.method = "GET";

                // Create download handler for audio
                var downloadHandler = new DownloadHandlerAudioClip(request.url, AudioType.MPEG);
                request.downloadHandler = downloadHandler;

                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        AudioClip clip = downloadHandler.audioClip;
                        if (clip != null)
                        {
                            audioSource.clip = clip;
                            audioSource.Play();
                            Debug.Log($"Playing audio for {identifier} (duration: {clip.length}s)");
                        }
                        else
                        {
                            Debug.LogError($"Cannot play audio for {identifier}: failed to create AudioClip from downloaded data");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Cannot play audio for {identifier}: error setting up AudioClip: {ex.Message}");
                    }
                }
                else
                {
                    string errorDetails = request.error ?? "Unknown UnityWebRequest error";
                    Debug.LogError($"Cannot play audio for {identifier}: failed to load audio data - {errorDetails}");
                }
            }
        }

        /// <summary>
        /// Validates that a string contains only valid Base64 characters
        /// </summary>
        private bool IsValidBase64String(string base64String)
        {
            if (string.IsNullOrEmpty(base64String))
                return false;

            // Base64 alphabet includes A-Z, a-z, 0-9, +, /, and = for padding
            // Remove padding characters for validation
            string trimmed = base64String.TrimEnd('=');

            // Check each character
            foreach (char c in trimmed)
            {
                if (!(c >= 'A' && c <= 'Z') &&
                    !(c >= 'a' && c <= 'z') &&
                    !(c >= '0' && c <= '9') &&
                    c != '+' && c != '/')
                {
                    return false;
                }
            }

            // Validate padding (if present)
            int equalCount = 0;
            for (int i = base64String.Length - 1; i >= 0 && base64String[i] == '='; i--)
            {
                equalCount++;
            }

            // Base64 padding can only be 0, 1, or 2 characters
            if (equalCount > 2)
                return false;

            return true;
        }
    }
}
#endif
