# **Building AI‑Driven NPCs in Minutes with the Player2 Unity SDK**

# 🗺️ Table of contents

1. [Getting Started](#getting-started)
    - [Prerequisites](#prerequisites)
    - [Integration Steps](#integration-steps)
    - [Authentication Setup](#authentication-setup)
2. [NpcManager](#npcmanager)
    - [Introduction](#introduction)
    - [Example setup of NpcManager](#example-setup-of-npcmanager)
3. [NPC Setup](#npc-setup)
    - [Npc Initialisation](#npc-initialisation)
    - [Configure the NPC component](#configure-the-npc-component)
4. [Adding rich NPC functions (Optional)](#adding-rich-npc-functions-optional)
5. [Speech-to-Text Integration](#speech-to-text-integration)
    - [STT Setup](#stt-setup) 
    - [STT Controller](#stt-controller)
    - [Customization](#customization)

---

# Getting Started

**Disclaimer**: Since we are not yet an official package on Unity's asset store - you need for now to manually copy assets.

### Prerequisites

Before integrating the Player2 Unity SDK, ensure you have:
- Unity 2023.2 or later
- A **Client ID** from the [Player2 Developer Dashboard](https://player2.game)
- Newtonsoft.Json package (automatically installed with this SDK)

### Quick Setup

The quickest way to experiment with unity-player2-sdk is to:

1. **Clone the repository**
   ```bash
   git clone git@github.com:elefant-ai/unity-player2-sdk
   cd unity-player2-sdk
   mkdir -p Projects
   ```

2. **Create Unity project**
   - Go to Unity Hub
   - Create new project → 2D (Built-in Render Pipeline)
   - Name it "BasicChat" and save it in the `Projects` directory

3. **Link assets**
   ```bash
   cd Projects/BasicChat
   mkdir -p Assets/Player2SDK
   ln -s ../../../ExampleFunctionHandler.cs Assets/Player2SDK/ExampleFunctionHandler.cs
   ln -s ../../../ExampleFunctionHandler.cs.meta Assets/Player2SDK/ExampleFunctionHandler.cs.meta
   ln -s ../../../Login.cs Assets/Player2SDK/Login.cs
   ln -s ../../../Login.cs.meta Assets/Player2SDK/Login.cs.meta
   ln -s ../../../NpcManager.cs Assets/Player2SDK/NpcManager.cs
   ln -s ../../../NpcManager.cs.meta Assets/Player2SDK/NpcManager.cs.meta
   ln -s ../../../Player2Npc.cs Assets/Player2SDK/Player2Npc.cs
   ln -s ../../../Player2Npc.cs.meta Assets/Player2SDK/Player2Npc.cs.meta
   ln -s ../../../Player2NpcResponseListener.cs Assets/Player2SDK/Player2NpcResponseListener.cs
   ln -s ../../../Player2NpcResponseListener.cs.meta Assets/Player2SDK/Player2NpcResponseListener.cs.meta
   ln -s ../../../Player2STT.cs Assets/Player2SDK/Player2STT.cs
   ln -s ../../../Player2STT.cs.meta Assets/Player2SDK/Player2STT.cs.meta
   ln -s ../../../STTController.cs Assets/Player2SDK/STTController.cs
   ln -s ../../../STTController.cs.meta Assets/Player2SDK/STTController.cs.meta
   ln -s ../../../demo.unity Assets/Scenes/demo.unity
   ln -s ../../../demo.unity.meta Assets/Scenes/demo.unity.meta
   ```

4. **Import TextMeshPro assets**
   - Click Window → TextMeshPro → Import TMP Essential Resources

5. **Reimport all assets**
   - Go to Assets → Reimport All

6. **Run the demo**
   - Choose the demo scene from the project details and run!

7. **Update the SDK**
   - If you need to update the SDK - just do `git pull --rebase origin main` and Assets → Reimport All - that's it!

### Integration Steps

1. **Import the SDK**
   - Copy all `.cs` files from this repository to your Unity project's `Assets` folder
   - Unity will automatically compile the scripts

2. **Set Up NpcManager**
   - Add the `NpcManager` component to a GameObject in your scene (preferably the scene root)
   - **Important**: Only use one NpcManager per scene
   - Configure the required fields:
     - **Client ID**: Enter your Client ID from the Player2 Developer Dashboard
     - **TTS**: Enable if you want text-to-speech for NPCs
     - **Functions**: Define any custom functions your NPCs can call (optional)

3. **Create Login System**
   - Add the `Login` component to a GameObject in your scene
   - In the Login component, drag your NpcManager into the `Npc Manager` field
   - Create a UI Button in your scene
   - In the button's `OnClick()` event, add the Login GameObject and select `Login.OpenURL()`

### Authentication Setup

The SDK uses OAuth device flow for secure authentication:

1. When a user clicks the login button, a browser window opens
2. The user authorizes your application on the Player2 website
3. The SDK automatically receives and stores the API key
4. NPCs become active and ready to chat

**Note**: Users must authenticate each time they start your application. The API key is obtained dynamically and not stored permanently.

---

# NpcManager

### Introduction

The `NpcManager` component is the heart of the Player2 Unity SDK, allowing you to create AI‑driven NPCs that can chat and perform actions in your game world.

To start integrating the player2-sdk into your project; Add `NpcManager` to your scene root, never use more than one NpcManager.
It stores your *Client ID* and the list of functions the LLM can invoke.

![Adding NpcManager to the hierarchy](https://cdn.elefant.gg/unity-sdk/init-npc-manager.png)



### Example setup of `NpcManager`
![NpcManager inspector configured](https://cdn.elefant.gg/unity-sdk/npc-manager-example.png)

* **Client ID** – your unique identifier from the Player2 Developer Dashboard.
* **Functions → +** – one element per action.

  * *Name* – code & prompt identifier.
  * *Description* – natural‑language hint for the model.
  * *Arguments* – nested rows for each typed parameter (e.g. `radius:number`).
    * Each argument can be specified if it is *required* (i.e. is not allowed to be null)

Example above exposes `flame(radius:number)` which spawns a fiery VFX cloud.

---

# NPC Setup

---

### Npc Initialisation
Select the GameObject that represents your NPC (`Person 1` in the image below) and add **Player2Npc.cs**.

![Hierarchy showing Person 1 with Player2Npc](https://cdn.elefant.gg/unity-sdk/npc-init.png)



### Configure the NPC component
1. **Npc Manager** – drag the scene’s NpcManager.
2. **Short / Full Name** – UI labels.
3. **Character Description** – persona sent at spawn.
4. **Input Field / Output Message** – TextMesh Pro components that your npc will listen to and output to.
5. Tick **Persistent** if the NPC should survive restarts of the Player2 client.


That’s it—hit **Play** and chat away.

![Player2Npc inspector settings](https://cdn.elefant.gg/unity-sdk/npc-setup.png)



---


## Adding rich NPC functions (Optional)
If you want to allow for a higher level of AI interactivity,
1. Add a script like the sample below to the Scene Root.
2. In **NpcManager → Function Handler**, press **+**, drag the object, then pick **ExampleFunctionHandler → HandleFunctionCall**.

```csharp
using UnityEngine;
using Newtonsoft.Json.Linq;

namespace player2_sdk
{
    public class ExampleFunctionHandler : MonoBehaviour
    {
        public void HandleFunctionCall(FunctionCall functionCall)
        {
            Debug.Log($"Handling function call: {functionCall.name}");

            // Example: Handle a flame function call
            if (functionCall.name == "flame")
            {
                // Access arguments from the JObject
                if (functionCall.arguments.TryGetValue("radius", out JToken radiusToken))
                {
                    float radius = radiusToken.Value<float>();
                    SpawnFlameCloud(radius);
                }
                else
                {
                    // Use default value if argument not provided
                    SpawnFlameCloud(3f);
                }
            }
            else if (functionCall.name == "teleport")
            {
                // Handle teleport function with multiple arguments
                if (functionCall.arguments.TryGetValue("x", out JToken xToken) &&
                    functionCall.arguments.TryGetValue("y", out JToken yToken) &&
                    functionCall.arguments.TryGetValue("z", out JToken zToken))
                {
                    Vector3 position = new Vector3(
                        xToken.Value<float>(),
                        yToken.Value<float>(),
                        zToken.Value<float>()
                    );
                    TeleportNpc(functionCall.aiObject, position);
                }
            }
            else if (functionCall.name == "spawn_item")
            {
                // Handle spawn_item function with string and numeric arguments
                string itemName = functionCall.arguments["item_name"]?.Value<string>() ?? "default_item";
                int quantity = functionCall.arguments["quantity"]?.Value<int>() ?? 1;
                SpawnItem(itemName, quantity, functionCall.aiObject.transform.position);
            }
        }

        void SpawnFlameCloud(float radius)
        {
            // Your VFX / gameplay code here
            Debug.Log($"Spawning flame cloud with radius: {radius}");
        }

        void TeleportNpc(GameObject npc, Vector3 position)
        {
            // Teleport the NPC to the specified position
            npc.transform.position = position;
            Debug.Log($"Teleported NPC to: {position}");
        }

        void SpawnItem(string itemName, int quantity, Vector3 position)
        {
            // Spawn items in the game world
            for (int i = 0; i < quantity; i++)
            {
                Debug.Log($"Spawning {itemName} at {position}");
            }
        }
    }
}
```

You never respond manually; the back‑end keeps streaming text while your Unity logic happens in parallel.
Now, whenever the model decides the NPC should *act*, `HandleFunctionCall` fires on the main thread.

### Setting up the Function Handler

In the Unity Editor:

1. Select your **NpcManager** GameObject
2. In the Inspector, find the **Function Handler** section
3. Click the **+** button to add a new event handler
4. Drag your **ExampleFunctionHandler** GameObject into the object field
5. Select **ExampleFunctionHandler → HandleFunctionCall(FunctionCall)** from the dropdown

![Selecting HandleFunctionCall in the UnityEvent dropdown](https://cdn.elefant.gg/unity-sdk/function-handler-config.png)

### Function Call Flow

1. **NPC Response**: When an NPC responds with a function call, it's received via SSE
2. **Parsing**: The response is parsed and converted to a `FunctionCall` object
3. **Event Trigger**: The `functionHandler` UnityEvent is invoked on the main thread
4. **Execution**: Your `HandleFunctionCall` method executes with the function details
5. **Game Logic**: Your game code runs, potentially spawning effects, moving objects, etc.

### Function Arguments

Function calls include:
- `name`: The function name (e.g., "flame", "teleport")
- `arguments`: A JObject containing the function parameters
- `aiObject`: Reference to the NPC GameObject that triggered the function call

You can access arguments using JObject methods like `TryGetValue` or direct indexing with null-coalescing operators for safe access.

### Advanced Function Handler Example

For more complex scenarios, see `AdvancedFunctionHandler.cs` which demonstrates:

- **Type-safe argument extraction** with generic helper methods
- **Multiple function types** (combat, healing, teleportation, item spawning)
- **State tracking** and function call statistics
- **Resource management** for spawned objects
- **Error handling** and fallback behavior
- **Integration patterns** with Unity components and audio
- **Configuration options** via serialized fields

The advanced handler includes examples of:
- Safe type conversion from JObject to C# types
- Default value handling for missing arguments
- Coroutine-based delayed actions
- Object pooling and cleanup
- Integration with Unity's AudioSource and effects systems

### Best Practices

1. **Always handle exceptions** in your function handlers
2. **Use type-safe argument extraction** to prevent runtime errors
3. **Provide sensible defaults** for optional parameters
4. **Log function calls** for debugging and analytics
5. **Clean up spawned objects** to prevent memory leaks
6. **Consider performance** - function calls run on the main thread
7. **Test edge cases** like missing arguments or invalid values

---

# Speech-to-Text Integration

The Player2 Unity SDK includes real-time Speech-to-Text (STT) functionality using WebSocket streaming. This allows players to speak directly to NPCs instead of typing.

## STT Setup

### Prerequisites
- **NativeWebSocket package** (automatically added to `package.json`)
- **Newtonsoft.Json** (already included with SDK)
- **Microphone permissions** in your Unity project

### Basic Integration

1. **Add STT Component**
   - Add `Player2STT` component to a GameObject in your scene
   - Assign your `NpcManager` to the **Npc Manager** field

2. **Configure STT Settings**
   - **Sample Rate**: 44100 Hz (default, recommended)
   - **Audio Chunk Duration**: 50ms (default)
   - **Enable Interim Results**: For real-time partial transcriptions
   - **Enable VAD**: Voice Activity Detection (optional)

### Events
The `Player2STT` component provides Unity Events for integration:

- **OnSTTReceived**: Fired when a transcript is received
- **OnSTTFailed**: Fired when STT encounters an error  
- **OnListeningStarted**: Fired when recording begins
- **OnListeningStopped**: Fired when recording ends

## STT Controller

For a complete UI solution, use the `STTController` component:

### Setup
1. **Add STTController** to a GameObject with a Button
2. **Auto-Configuration**: The controller automatically finds:
   - Button component for recording controls
   - TextMeshPro components for button text and transcripts
   - Player2STT component for STT functionality

### Button States
The controller manages button appearance:
- **Normal**: Circle shape, "REC" text, white color
- **Connecting**: Circle shape, "..." text, yellow color  
- **Recording**: Square shape, "STOP" text, red color

### Transcript Display
- Automatically accumulates and displays transcripts
- Shows timestamps and transcript numbers
- Supports both TextMeshPro and regular Text components
- Handles transcript history with configurable limits

## Customization

### Audio Settings
```csharp
[SerializeField] private int sampleRate = 44100;
[SerializeField] private int audioChunkDurationMs = 50;
[SerializeField] private float heartbeatInterval = 5f;
```

### UI Customization  
```csharp
[SerializeField] private string recordText = "REC";
[SerializeField] private string stopText = "STOP";
[SerializeField] private Color recordingColor = Color.red;
[SerializeField] private Sprite circleSprite;
[SerializeField] private Sprite squareSprite;
```

### Manual Integration
If you prefer manual control over the STT system:

```csharp
public class MySTTHandler : MonoBehaviour
{
    [SerializeField] private Player2STT stt;
    
    void Start()
    {
        stt.OnSTTReceived.AddListener(OnTranscriptReceived);
    }
    
    void OnTranscriptReceived(string transcript)
    {
        Debug.Log($"Received: {transcript}");
        // Send transcript to NPC or handle as needed
    }
    
    public void StartListening()
    {
        stt.StartSTT();
    }
    
    public void StopListening()
    {
        stt.StopSTT();
    }
}
```

### Important Notes

- **One STT per Scene**: Only use one `Player2STT` component per scene
- **Microphone Permissions**: Ensure your project has microphone permissions enabled
- **WebSocket Connection**: STT requires an active connection to the Player2 API
- **Real-time Streaming**: Audio is streamed continuously for low-latency transcription
- **Error Handling**: Monitor `OnSTTFailed` events for connection or API issues

### Troubleshooting

- **No Microphone**: Check `Microphone.devices.Length > 0`
- **No Transcripts**: Verify NpcManager has valid API key and base URL
- **Button Not Responding**: Ensure Button component and STT component are properly assigned
- **WebSocket Errors**: Check internet connection and API key validity

---
