namespace player2_sdk
{
    /// <summary>
    ///     Factory class for creating platform-specific audio player implementations
    /// </summary>
    public static class AudioPlayerFactory
    {
        private static IAudioPlayer _instance;

        /// <summary>
        ///     Gets the appropriate audio player implementation for the current platform
        /// </summary>
        public static IAudioPlayer GetAudioPlayer()
        {
            if (_instance != null)
                return _instance;

#if UNITY_WEBGL
            _instance = new WebGLAudioPlayer();
#else
            _instance = new DefaultAudioPlayer();
#endif

            return _instance;
        }
    }
}