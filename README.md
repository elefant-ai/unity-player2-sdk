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

---

# Getting Started

### Prerequisites

Before integrating the Player2 Unity SDK, ensure you have:
- Unity 2023.2 or later
- A **Client ID** from the [Player2 Developer Dashboard](https://player2.game)
- Newtonsoft.Json package (automatically installed with this SDK)

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

public class ExampleFunctionHandler : MonoBehaviour
{
    public void HandleFunctionCall(FunctionCall call)
    {
        if (call.name == "flame")
        {
            float radius = call.ArgumentAsFloat("radius", defaultValue: 3f);
            SpawnFlameCloud(radius);
        }
    }

    void SpawnFlameCloud(float r)
    {
        // Your VFX / gameplay code here
    }
}
```

You never respond manually; the back‑end keeps streaming text while your Unity logic happens in parallel.
Now, whenever the model decides the NPC should *act*, `HandleFunctionCall` fires on the main thread.

![Selecting HandleFunctionCall in the UnityEvent dropdown](https://cdn.elefant.gg/unity-sdk/function-handler-config.png)


---
