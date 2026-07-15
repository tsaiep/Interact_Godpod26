# Cabin Portraits Video Cycle Setup

## Assets

- Config: `Assets/_Interact_Godpod26_CabinPortraits/Data/System/CabinPortraitVideoSequenceConfig.asset`
- RenderTexture A: `Assets/_Interact_Godpod26_CabinPortraits/RenderTextures/RT_CabinPortraitA.renderTexture`
- RenderTexture B: `Assets/_Interact_Godpod26_CabinPortraits/RenderTextures/RT_CabinPortraitB.renderTexture`
- Expected video folder: `Assets/StreamingAssets/Videos/Interaction02/`
- Default files:
  - `Video_00.mp4`
  - `Video_01.mp4`
  - `Video_02.mp4`
  - `Video_03.mp4`
  - `Video_04.mp4`
  - `Video_05.mp4`

## Scene Object Layout

Create this structure in the CabinPortraits scene:

```text
SceneRoot
  VideoSystem
    VideoPlayerA
    VideoPlayerB
    FlowController
  VideoDisplay
    VideoDisplayA or SharedDisplay
    VideoDisplayB optional
  Transition
    TransitionVisual optional
```

## Component Wiring

1. Add `VideoPlayer` to `VideoPlayerA`.
   - Source: URL
   - Play On Awake: off
   - Wait For First Frame: on
   - Target Texture: `RT_CabinPortraitA`

2. Add `VideoPlayer` to `VideoPlayerB`.
   - Source: URL
   - Play On Awake: off
   - Wait For First Frame: on
   - Target Texture: `RT_CabinPortraitB`

3. Add `CabinPortraitVideoDisplayController` to `VideoDisplay`.
   - For a single display mesh, assign `Shared Display Renderer`.
   - For two display meshes, assign `Player A Display Renderer` and `Player B Display Renderer`.
   - Assign fallback textures `RT_CabinPortraitA` and `RT_CabinPortraitB`.

4. Add `CabinPortraitVideoCycleController` to `FlowController`.
   - Sequence Config: `CabinPortraitVideoSequenceConfig`
   - Video Player A: `VideoPlayerA`
   - Video Player B: `VideoPlayerB`
   - Display Controller: `VideoDisplay`
   - Keyboard Input: enabled
   - Switch Key: Space

5. Use the Unity Events on `CabinPortraitVideoCycleController` to trigger transition visuals.
   - `On Switch Requested`: accepted Space input.
   - `On Transition Started`: fire animation, VFX, shader change, or sound.
   - `On Video Index Changed`: the visible video has switched.
   - `On Input Cooldown Started` and `On Input Cooldown Ended`: lock/unlock UI hints if needed.

## Runtime Flow

On Start, the controller prepares index `0` on player A, prepares index `1` on player B, and plays index `0` when its first frame is ready.

When Space is accepted:

```text
invoke On Switch Requested
invoke On Transition Started
wait transitionSwitchDelay
show prepared next player
stop old player
prepare following index on old player
wait remaining inputCooldown
accept Space again
```

The index loops as:

```text
0 -> 1 -> 2 -> 3 -> 4 -> 5 -> 0
```

