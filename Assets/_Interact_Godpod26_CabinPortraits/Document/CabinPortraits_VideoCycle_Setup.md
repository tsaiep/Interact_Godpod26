# Cabin Portraits Video Cycle Setup

## Assets

- Config: `Assets/_Interact_Godpod26_CabinPortraits/Data/System/CabinPortraitVideoSequenceConfig.asset`
- RenderTexture A: `Assets/_Interact_Godpod26_CabinPortraits/RenderTextures/RT_CabinPortraitA.renderTexture`
- RenderTexture B: `Assets/_Interact_Godpod26_CabinPortraits/RenderTextures/RT_CabinPortraitB.renderTexture`
- Expected video folder: `Assets/StreamingAssets/CabinPortraits/Videos/`
- Default files:
  - `Video_01.mp4`
  - `Video_02.mp4`
  - `Video_03.mp4`
  - `Video_04.mp4`
  - `Video_05.mp4`
  - `Video_06.mp4`

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
   - Render Mode: Render Texture
   - Play On Awake: off
   - Wait For First Frame: on
   - Target Texture: `RT_CabinPortraitA`

2. Add `VideoPlayer` to `VideoPlayerB`.
   - Source: URL
   - Render Mode: Render Texture
   - Play On Awake: off
   - Wait For First Frame: on
   - Target Texture: `RT_CabinPortraitB`

3. Add `CabinPortraitVideoDisplayController` to `VideoDisplay`.
   - Leave `Shared Display Renderer` empty for the recommended A/B renderer flow.
   - Assign `Player A Display Renderer` to the mesh that displays `RT_CabinPortraitA`.
   - Assign `Player B Display Renderer` to the mesh that displays `RT_CabinPortraitB`.
   - Assign fallback textures `RT_CabinPortraitA` and `RT_CabinPortraitB`.
   - Keep both display GameObjects active. The controller switches visibility with `Renderer.enabled` so each renderer keeps its own RenderTexture binding.
   - Only use `Shared Display Renderer` when you intentionally want a single mesh whose texture is swapped at runtime.
   - If Console logs say `Source=fallback`, the active VideoPlayer did not provide a runtime texture. Check its Target Texture and Render Mode.

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
   - `On Ready To Reveal`: the next video is visible behind the fully covered transition; start revealing the mask here.
   - `On Video Index Changed`: the visible video has switched.
   - `On Input Locked` and `On Input Unlocked`: lock/unlock UI hints if needed. There is no fixed cooldown delay.

## Runtime Flow

On Start, the controller prepares the start index to its first frame and plays it on player A. It does not prepare the next video in the background while the active video is visible.

When Space is accepted:

```text
invoke On Switch Requested
invoke On Transition Started
wait Transition Cover Delay
stop the current active player while the transition fully covers the display
prepare the next video to its first frame on the inactive player
play the next video and show its dedicated renderer behind the covered transition
invoke On Ready To Reveal
swap active and standby players
accept Space again
```

This keeps the visible playback path clean: while a video is visible, no other VideoPlayer is preparing, pre-rolling, seeking, or decoding in the background. The old video continues during `Transition Cover Delay`; once the transition should fully cover the display, the old player is stopped and the next player is prepared under the mask. `Prepare Warning Timeout` and `First Frame Warning Timeout` only print warnings and continue waiting; they do not enter `ErrorRecovery`.

The index loops as:

```text
0 -> 1 -> 2 -> 3 -> 4 -> 5 -> 0
```
