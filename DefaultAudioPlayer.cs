#if !UNITY_WEBGL
namespace player2_sdk
{
    /// <summary>
    ///     Default audio player implementation for non-WebGL platforms using temporary files
    /// </summary>
    public class DefaultAudioPlayer : IAudioPlayer
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
            var commaIndex = dataUrl.IndexOf(',');
            if (commaIndex == -1 || commaIndex == dataUrl.Length - 1)
            {
                Debug.LogError(
                    $"Cannot play audio for {identifier}: invalid data URL format (missing comma or no data after comma)");
                yield break;
            }

            // Extract base64 data from data URL
            var base64String = dataUrl.Substring(commaIndex + 1);

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
                // Fix Base64 padding if needed
                var paddedBase64 = FixBase64Padding(base64String);

                // Decode to bytes
                audioBytes = Convert.FromBase64String(paddedBase64);
            }
            catch (FormatException ex)
            {
                // Log additional context for Base64 decoding failures
                var base64Preview = base64String.Length > 50 ? base64String.Substring(0, 50) + "..." : base64String;
                Debug.LogError(
                    $"Cannot play audio for {identifier}: Base64 decoding failed: {ex.Message}. Base64 data length: {base64String.Length}, Preview: {base64Preview}");
                yield break;
            }

            // Write to temp file with random name
            var tempPath = Path.Combine(Application.temporaryCachePath, $"audio_{Guid.NewGuid().ToString("N")}.mp3");

            try
            {
                File.WriteAllBytes(tempPath, audioBytes);
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    $"Cannot play audio for {identifier}: failed to write audio data to temp file: {ex.Message}");
                yield break;
            }

            // Load and play
            using (var request = UnityWebRequestMultimedia.GetAudioClip($"file://{tempPath}", AudioType.MPEG))
            {
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        var clip = DownloadHandlerAudioClip.GetContent(request);
                        if (clip != null)
                        {
                            audioSource.clip = clip;
                            audioSource.Play();
                            Debug.Log($"Playing audio for {identifier} (duration: {clip.length}s)");
                        }
                        else
                        {
                            Debug.LogError(
                                $"Cannot play audio for {identifier}: failed to create AudioClip from downloaded data");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Cannot play audio for {identifier}: error setting up AudioClip: {ex.Message}");
                    }
                }
                else
                {
                    var errorDetails = request.error ?? "Unknown UnityWebRequest error";
                    Debug.LogError($"Cannot play audio for {identifier}: failed to load audio file - {errorDetails}");
                }
            }

            // Cleanup after 5 seconds (with error handling)
            yield return new WaitForSeconds(5f);
            try
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                    Debug.Log($"Cleaned up temporary audio file for {identifier}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Failed to cleanup temporary audio file for {identifier}: {ex.Message}");
            }
        }

        /// <summary>
        ///     Fixes Base64 padding by adding missing = characters if needed
        /// </summary>
        private string FixBase64Padding(string base64String)
        {
            if (string.IsNullOrEmpty(base64String))
                return base64String;

            // Base64 strings must be divisible by 4
            var missingPadding = 4 - base64String.Length % 4;

            if (missingPadding != 4) // Only add padding if needed
            {
                base64String = base64String + new string('=', missingPadding);
                Debug.Log($"Fixed Base64 padding by adding {missingPadding} character(s)");
            }

            return base64String;
        }

        /// <summary>
        ///     Validates that a string contains only valid Base64 characters
        /// </summary>
        private bool IsValidBase64String(string base64String)
        {
            if (string.IsNullOrEmpty(base64String))
                return false;

            // Base64 alphabet includes A-Z, a-z, 0-9, +, /, and = for padding
            // Remove padding characters for validation
            var trimmed = base64String.TrimEnd('=');

            // Check each character
            foreach (var c in trimmed)
                if (!(c >= 'A' && c <= 'Z') &&
                    !(c >= 'a' && c <= 'z') &&
                    !(c >= '0' && c <= '9') &&
                    c != '+' && c != '/')
                    return false;

            // Validate padding (if present)
            var equalCount = 0;
            for (var i = base64String.Length - 1; i >= 0 && base64String[i] == '='; i--) equalCount++;

            // Base64 padding can only be 0, 1, or 2 characters
            if (equalCount > 2)
                return false;

            return true;
        }
    }
}
#endif