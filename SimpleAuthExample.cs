using player2_sdk;
using UnityEngine;

/// <summary>
///     Super simple example showing one-line authentication setup
///     Just attach this to any GameObject in your scene!
/// </summary>
public class SimpleAuthExample : MonoBehaviour
{
    [Header("Required")] [Tooltip("Drag your NpcManager here")]
    public NpcManager npcManager;

    [Header("Optional - Game Objects to Show After Auth")]
    [Tooltip("These objects will be hidden until authentication succeeds")]
    public GameObject[] gameObjectsToShowAfterAuth;

    private void Start()
    {
        // Hide game objects until authenticated
        SetGameObjectsActive(false);

        // ONE LINE SETUP - That's it! 🎉
        var authUI = AuthenticationUI.Setup(npcManager);

        // Optional: Subscribe to events if you want to do something when auth completes
        authUI.authenticationCompleted.AddListener(OnAuthenticationComplete);
        authUI.authenticationFailed.AddListener(OnAuthenticationFailed);
    }

    private void OnAuthenticationComplete()
    {
        Debug.Log("🎉 Authentication successful! Enabling game content.");
        SetGameObjectsActive(true);

        // Add any other logic you want when auth succeeds
        // For example: LoadPlayerData(), ShowWelcomeMessage(), etc.
    }

    private void OnAuthenticationFailed(string error)
    {
        Debug.LogError($"❌ Authentication failed: {error}");

        // Keep game objects hidden
        SetGameObjectsActive(false);

        // You could show an offline mode or error message here
    }

    private void SetGameObjectsActive(bool active)
    {
        foreach (var obj in gameObjectsToShowAfterAuth)
            if (obj != null)
                obj.SetActive(active);
    }
}