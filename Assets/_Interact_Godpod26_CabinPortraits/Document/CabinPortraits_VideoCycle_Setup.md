# Cabin Portraits Video Cycle Setup

## Assets

- Config: `Assets/_Interact_Godpod26_CabinPortraits/Data/System/CabinPortraitVideoSequenceConfig.asset`
- RenderTexture A: `Assets/_Interact_Godpod26_CabinPortraits/RenderTextures/RT_CabinPortraitA.renderTexture`
- RenderTexture B: `Assets/_Interact_Godpod26_CabinPortraits/RenderTextures/RT_CabinPortraitB.renderTexture`
- Timer RenderTexture A: `Assets/_Interact_Godpod26_CabinPortraits/RenderTextures/RT_CabinPortraitTimerA.renderTexture`
- Timer RenderTexture B: `Assets/_Interact_Godpod26_CabinPortraits/RenderTextures/RT_CabinPortraitTimerB.renderTexture`
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
    TimerVideoPlayerA optional
    TimerVideoPlayerB optional
    FlowController
  VideoDisplay
    VideoDisplayA or SharedDisplay
    VideoDisplayB optional
    TimerVideoDisplayA or TimerSharedDisplay optional
    TimerVideoDisplayB optional
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

3. Optional timer player pipeline:
   - `TimerVideoPlayerA` and `TimerVideoPlayerB` exist in the scene when timer playback should render to a separate RenderTexture set.
   - Source: URL, Render Mode: Render Texture, Play On Awake: off, Wait For First Frame: on.
   - Assign `RT_CabinPortraitTimerA` and `RT_CabinPortraitTimerB` as each Target Texture.
   - Leave these fields empty to make timer playback fall back to `VideoPlayerA/B`.

4. Add `CabinPortraitVideoDisplayController` to `VideoDisplay`.
   - Assign `Initial Display Renderer` to the static image Quad shown while idle.
   - Assign `Initial Texture` only if the Quad's material should be overwritten by the controller. Leave it empty when the Quad already has the correct material.
   - Leave `Shared Display Renderer` empty for the recommended A/B renderer flow.
   - Assign `Player A Display Renderer` to the mesh that displays `RT_CabinPortraitA`.
   - Assign `Player B Display Renderer` to the mesh that displays `RT_CabinPortraitB`.
   - Assign fallback textures `RT_CabinPortraitA` and `RT_CabinPortraitB`.
   - `Timer Player A/B Display Renderer` are assigned to `TimerVideoDisplayA/B` in the scene.
   - `Timer Player A/B Texture` are assigned to `RT_CabinPortraitTimerA/B`. If no timer display renderer is assigned, timer playback uses the manual display renderer set.
   - Keep both display GameObjects active. The controller switches visibility with `Renderer.enabled` so each renderer keeps its own RenderTexture binding.
   - Only use `Shared Display Renderer` when you intentionally want a single mesh whose texture is swapped at runtime.
   - If Console logs say `Source=fallback`, the active VideoPlayer did not provide a runtime texture. Check its Target Texture and Render Mode.

5. Add `CabinPortraitVideoCycleController` to `FlowController`.
   - Sequence Config: `CabinPortraitVideoSequenceConfig`
   - Video Player A: `VideoPlayerA`
   - Video Player B: `VideoPlayerB`
   - Timer Video Player A/B: optional timer-only VideoPlayers. Leave empty to fall back to `VideoPlayerA/B`.
   - Display Controller: `VideoDisplay`
   - Keyboard Input: enabled
   - Switch Key: `Space` or `Enter`
   - `Video Relative Paths`: manual button videos.
   - `Timer Video Relative Paths`: idle timer videos. This list is independent from the manual list.
   - `Transition Trigger Before Video End`: remaining seconds before a playing video ends when the return transition starts.
   - `Timer Video Delay`: seconds after entering the initial static screen before a timer video plays. Set to `0` to disable timer playback.

6. Use the Unity Events on `CabinPortraitVideoCycleController` to trigger transition visuals.
   - `On Switch Requested`: accepted manual button input.
   - `On Timer Video Requested`: accepted idle timer playback request.
   - `On Video Started`: compatibility hook fired when any one-shot video starts playing visibly.
   - `On Manual Video Started`: fire manual/button effects that must happen exactly when the manual video starts playing.
   - `On Timer Video Started`: fire timer-specific effects that must happen exactly when the timer video starts playing.
   - `On Manual Transition Started`: fire the manual/button transition when a manual video has the configured remaining seconds left.
   - `On Timer Transition Started`: fire the timer-specific transition when a timer video has the configured remaining seconds left.
   - `On Manual Ready To Reveal`: the initial static display is visible behind the fully covered manual transition; start revealing the manual mask here.
   - `On Timer Ready To Reveal`: the initial static display is visible behind the fully covered timer transition; start revealing the timer mask here.
   - `On Manual Initial Ready To Reveal`: same timing as `On Manual Ready To Reveal`, without an index argument.
   - `On Timer Initial Ready To Reveal`: same timing as `On Timer Ready To Reveal`, without an index argument.
   - `On Video Index Changed`: the visible one-shot video index has changed.
   - `On Input Locked` and `On Input Unlocked`: lock/unlock UI hints while startup, one-shot playback, or return transition is active.
   - `State Events`: use the Initial, Manual Video, Timer Video, Returning To Initial, and Covered Restoring Initial entries for phase-specific hooks. Manual/Timer-specific Returning and Covered hooks are available when the transition source matters.

## Runtime Flow

On Start, the controller enters the initial static image screen. No video is playing in this state. Entering this state starts or restarts the idle timer when `Timer Video Delay` is greater than `0` and `Timer Video Relative Paths` has at least one entry.

When Space or the configured external button is accepted:

```text
Initial
invoke On Switch Requested
prepare next manual video index
play that video once
invoke On Video Started and On Manual Video Started
when remaining seconds <= Transition Trigger Before Video End:
  invoke On Manual Transition Started
  wait Transition Cover Delay
  stop the video
  show Initial Display Renderer
  invoke On Manual Ready To Reveal and On Manual Initial Ready To Reveal
Initial
```

When the idle timer reaches `Timer Video Delay`, the controller uses the same one-shot playback flow with `Timer Video Relative Paths` and invokes `On Timer Video Requested`. When timer playback becomes visible, it invokes `On Video Started` and `On Timer Video Started`. When the timer video reaches `Transition Trigger Before Video End`, it invokes `On Timer Transition Started` instead of the manual transition event. After `Transition Cover Delay`, it invokes `On Timer Ready To Reveal` and `On Timer Initial Ready To Reveal` instead of the manual reveal events. If timer players and timer display renderers are assigned, this path uses the timer RenderTexture/display pipeline. If they are empty, it falls back to the manual A/B pipeline. Manual input is ignored from timer request through timer video completion and return to the initial screen.

For a button video event that must happen 1 second before the video ends, set `Transition Trigger Before Video End` to `1` and wire that event to `On Manual Transition Started`. Keep video-start reactions on `On Manual Video Started`.

Only one video is prepared and played during a request. The optional timer pipeline separates render targets and visible renderers, but it does not keep timer videos playing or preloading in the background.

Manual and timer indexes are tracked independently. A successful manual video advances only the manual index; a successful timer video advances only the timer index. Returning to Initial restarts the idle timer for both manual and timer playback paths.

Each list loops as:

```text
0 -> 1 -> 2 -> 3 -> 4 -> 5 -> 0
```
