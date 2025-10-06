using System.Collections;
using UnityEngine;

namespace player2_sdk
{
    /// <summary>
    ///     Interface for audio playback functionality with platform-specific implementations
    /// </summary>
    public interface IAudioPlayer
    {
        /// <summary>
        ///     Play audio from base64-encoded data URL
        /// </summary>
        /// <param name="dataUrl">Base64-encoded data URL (data:audio/mpeg;base64,...)</param>
        /// <param name="audioSource">AudioSource component to play through</param>
        /// <param name="identifier">Identifier for logging purposes (e.g., NPC ID)</param>
        /// <returns>Coroutine enumerator for the playback operation</returns>
        IEnumerator PlayAudioFromDataUrl(string dataUrl, AudioSource audioSource, string identifier);
    }
}