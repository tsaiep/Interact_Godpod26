# StreamingAssets 多影片順暢播放使用說明

## 1. 修改與新增的檔案清單

1. 修改 `Assets/_Interact_Godpod26/Scripts/Video/VideoPlaybackManager.cs`
2. 修改 `Assets/_Interact_Godpod26/Scripts/Video/VideoFlowCoordinator.cs`
3. 修改 `Assets/_Interact_Godpod26/Scripts/Video/VideoSystemConfig.cs`
4. 修改 `Assets/_Interact_Godpod26/Scripts/Video/StreamingImageLoader.cs`
5. 修改 `Assets/_Interact_Godpod26/Scripts/Presentation/VideoTransitionController.cs`
6. 修改 `Assets/_Interact_Godpod26/Scripts/Utilities/StreamingAssetsPathUtility.cs`
7. 修改 `Assets/_Interact_Godpod26/Data/System/VideoSystemConfig.asset`
8. 修改 `Assets/_Interact_Godpod26/Level/LV_LuggageCheck.unity`
9. 新增 `Assets/_Interact_Godpod26/RenderTextures/RT_ContentA.renderTexture`
10. 新增 `Assets/_Interact_Godpod26/RenderTextures/RT_ContentB.renderTexture`
11. 新增本文件 `Assets/_Interact_Godpod26/Document/StreamingAssets 多影片順暢播放使用說明.md`

## 2. 每支影片腳本的責任

1. `VideoPlaybackManager`
   1. 管理 `IdleVideoPlayer`、`ContentVideoPlayerA`、`ContentVideoPlayerB` 三個常駐 `VideoPlayer`。
   2. 建立 StreamingAssets 完整路徑與本機 URI。
   3. 執行 `Stop()`、設定 `url`、設定 `isLooping`、啟用 `sendFrameReadyEvents`、呼叫 `Prepare()`。
   4. 監聽 `prepareCompleted`、`frameReady`、`loopPointReached`、`errorReceived`。
   5. 使用 operation token 忽略過期 callback。
   6. 在第一幀完成後關閉 `sendFrameReadyEvents`。
   7. 管理 Content A/B active 與 standby 交替。

2. `VideoFlowCoordinator`
   1. 監聽 `GameFlowManager.StateChanged`。
   2. 在 Idle、Intro、Gameplay、Success、Failure、Resetting 各狀態呼叫影片準備與播放。
   3. 保持目前畫面直到下一支影片第一幀完成。
   4. 只在 Idle 畫面顯示後才清理舊 Content 與靜態背景。

3. `VideoTransitionController`
   1. 控制 `IdleVideoSurface`、`ContentVideoSurface`、`StaticBackgroundSurface` 顯示。
   2. 只切換 Renderer 顯示與 MaterialPropertyBlock texture。
   3. 不更換任何 `VideoPlayer.targetTexture`。

4. `StreamingAssetsPathUtility`
   1. 將 LevelConfig 相對路徑轉成 `Application.streamingAssetsPath` 下的完整路徑。
   2. 處理 `\` 與 `/`、空格、中文檔名、重複 `StreamingAssets/` 前綴。
   3. 用 `new System.Uri(fullPath).AbsoluteUri` 建立 `VideoPlayer.url`。

5. `StreamingImageLoader`
   1. 從 StreamingAssets 載入 Gameplay 靜態背景圖片。
   2. `CancelLoading()` 只取消載入，不銷毀目前顯示中的最後有效背景。

## 3. 現有影片架構調整

1. 原本只有 `IdleVideoPlayer` 與單一 `ContentVideoPlayer`。
2. 現在改為三個常駐播放器：
   1. `IdleVideoPlayer`
   2. `ContentVideoPlayerA`
   3. `ContentVideoPlayerB`
3. 原本的 `contentVideoPlayer` Inspector 欄位已遷移為 `contentVideoPlayerA`。
4. `ContentVideoPlayerA` 與 `ContentVideoPlayerB` 不互換 RenderTexture。
5. 顯示層只把 `ContentVideoSurface` 的 texture 切到目前 active content player 的 RenderTexture。

## 4. 場景中的階層

在 `LV_LuggageCheck` 場景中，影片管理物件階層應為：

1. `VideoPlaybackManager`
   1. `IdleVideoPlayer`
   2. `ContentVideoPlayerA`
   3. `ContentVideoPlayerB`

顯示面階層維持原本場景中的三個 Renderer：

1. `IdleVideoSurface`
2. `ContentVideoSurface`
3. `StaticBackgroundSurface`

## 5. 三個 VideoPlayer 的 Inspector 設定

1. `IdleVideoPlayer`
   1. `Render Mode`: `Render Texture`
   2. `Target Texture`: `RT_IdleVideo`
   3. `Play On Awake`: off
   4. `Wait For First Frame`: on
   5. `Loop`: runtime 由 `VideoPlaybackManager` 設為 true
   6. `Source`: runtime 由 `VideoPlaybackManager` 設為 URL

2. `ContentVideoPlayerA`
   1. `Render Mode`: `Render Texture`
   2. `Target Texture`: `RT_ContentA`
   3. `Play On Awake`: off
   4. `Wait For First Frame`: on
   5. `Loop`: runtime 由 `VideoPlaybackManager` 設為 false
   6. `Source`: runtime 由 `VideoPlaybackManager` 設為 URL

3. `ContentVideoPlayerB`
   1. `Render Mode`: `Render Texture`
   2. `Target Texture`: `RT_ContentB`
   3. `Play On Awake`: off
   4. `Wait For First Frame`: on
   5. `Loop`: runtime 由 `VideoPlaybackManager` 設為 false
   6. `Source`: runtime 由 `VideoPlaybackManager` 設為 URL

三個 GameObject 必須保持 active，不要在流程中手動關閉。

## 6. 三張 RenderTexture 的建立與綁定方式

1. `RT_IdleVideo`
   1. 路徑：`Assets/_Interact_Godpod26/RenderTextures/RT_IdleVideo.renderTexture`
   2. 綁定到 `IdleVideoPlayer.Target Texture`
   3. 綁定到 `VideoTransitionController.idleVideoTexture`

2. `RT_ContentA`
   1. 路徑：`Assets/_Interact_Godpod26/RenderTextures/RT_ContentA.renderTexture`
   2. 綁定到 `ContentVideoPlayerA.Target Texture`
   3. 綁定到 `VideoTransitionController.contentVideoTextureA`

3. `RT_ContentB`
   1. 路徑：`Assets/_Interact_Godpod26/RenderTextures/RT_ContentB.renderTexture`
   2. 綁定到 `ContentVideoPlayerB.Target Texture`
   3. 綁定到 `VideoTransitionController.contentVideoTextureB`

## 7. StreamingAssets 資料夾及影片命名格式

影片放在：

```text
Assets/StreamingAssets/Videos
```

建議格式：

```text
Assets/StreamingAssets/Videos/Common/Idle_Loop.mp4
Assets/StreamingAssets/Videos/Level01/Level01_Intro.mp4
Assets/StreamingAssets/Videos/Level01/Level01_Success.mp4
Assets/StreamingAssets/Videos/Level01/Level01_Failure.mp4
```

靜態背景圖片放在：

```text
Assets/StreamingAssets/Images/Level01/Level01_FinalFrame.png
```

## 8. LevelConfig 中影片相對路徑的填寫方式

在每個 `LevelConfig` asset 中，只填 StreamingAssets 底下的相對路徑：

```text
introVideoRelativePath: Videos/Level01/Level01_Intro.mp4
successVideoRelativePath: Videos/Level01/Level01_Success.mp4
failureVideoRelativePath: Videos/Level01/Level01_Failure.mp4
finalFrameImageRelativePath: Images/Level01/Level01_FinalFrame.png
```

不要填：

```text
C:/UnityProject/Interact_Godpod26/Assets/StreamingAssets/Videos/Level01/Level01_Intro.mp4
```

## 9. 完整路徑與 URI 的建立方式

`StreamingAssetsPathUtility.TryBuildFilePath()` 會建立：

```csharp
string fullPath = Path.Combine(Application.streamingAssetsPath, relativePath);
```

`StreamingAssetsPathUtility.TryBuildFileUri()` 會建立：

```csharp
string videoUrl = new System.Uri(fullPath).AbsoluteUri;
```

`VideoPlaybackManager` 會先用 `File.Exists(fullPath)` 驗證，再設定：

```csharp
videoPlayer.url = videoUrl;
```

## 10. Content A/B 交替規則

1. `activeContentState` 代表目前已顯示或正在播放的 Content player。
2. `standbyContentState` 代表下一支要 Prepare 的 Content player。
3. Intro 播放時如果使用 A，進 Gameplay 並停止 Content 後，Result 會使用 B。
4. Result 播放時如果使用 B，回 Idle 並停止 Content 後，下一次 Intro 會使用 A。
5. 不會互換 `ContentVideoPlayerA.Target Texture` 與 `ContentVideoPlayerB.Target Texture`。

## 11. Prepare、第一幀與播放完成事件流程

每支影片的流程為：

1. `VideoPlaybackManager.Prepare(...)`
2. `videoPlayer.Stop()`
3. `videoPlayer.url = videoUrl`
4. `videoPlayer.isLooping = isLooping`
5. `videoPlayer.sendFrameReadyEvents = true`
6. `videoPlayer.Prepare()`
7. 等待 `prepareCompleted`
8. 對尚未顯示的 standby player 短暫 `Play()`，讓 RenderTexture 先輸出第一幀
9. 等待 `frameReady` 或可用 texture
10. 第一幀完成後 `Pause()` standby player
11. 第一幀完成後設定 `videoPlayer.sendFrameReadyEvents = false`
12. 觸發 `FirstFrameReady`
13. `VideoFlowCoordinator` 通知 `GameFlowManager` 進入 Playing 狀態
14. `PlayPrepared(...)`
15. 顯示對應 RenderTexture
16. 隱藏上一個畫面
17. 非循環影片收到 `loopPointReached` 後只回報一次完成

## 12. Prepare 與第一幀逾時設定位置

設定 asset：

```text
Assets/_Interact_Godpod26/Data/System/VideoSystemConfig.asset
```

Inspector 欄位：

1. `prepareTimeout`: 預設 10 秒
2. `firstFrameTimeout`: 預設 5 秒
3. `imageLoadTimeout`: 預設 10 秒
4. `verboseVideoLogs`: 是否輸出詳細影片 log

## 13. Inspector 中必須人工指定的欄位

在 `VideoPlaybackManager` component：

1. `videoSystemConfig`
2. `idleVideoPlayer`
3. `contentVideoPlayerA`
4. `contentVideoPlayerB`

在 `VideoFlowCoordinator` component：

1. `gameFlowManager`
2. `videoSystemConfig`
3. `videoPlaybackManager`
4. `streamingImageLoader`
5. `transitionController`

在 `VideoTransitionController` component：

1. `idleVideoRenderer`
2. `idleVideoTexture`
3. `contentVideoRenderer`
4. `contentVideoTextureA`
5. `contentVideoTextureB`
6. `staticBackgroundRenderer`
7. `texturePropertyName`
8. `alsoSetMainTex`

## 14. 如何測試 Idle、Intro、Success 與 Failure

1. 開啟 `LV_LuggageCheck`。
2. 進入 Play Mode。
3. Console 應看到 Idle Prepare、first frame ready、started。
4. 確認 `IdleVideoSurface` 顯示且循環。
5. 用既有 RFID 或 Debug Level Input 啟動 Level01。
6. Intro Prepare 時 Idle 畫面需持續顯示。
7. Intro 第一幀完成後才切到 `ContentVideoSurface`。
8. Intro 結束後切到 `StaticBackgroundSurface`。
9. 操作 Gameplay 觸發 Success 或 Failure。
10. Result Prepare 時靜態背景需持續顯示。
11. Result 第一幀完成後才切到 `ContentVideoSurface`。
12. Result 結束後切回 Idle。

## 15. Windows Standalone Build 驗證 StreamingAssets

1. 建立 Windows Standalone Build。
2. 確認 build 輸出資料夾包含：

```text
Interact_Godpod26_Data/StreamingAssets/Videos
Interact_Godpod26_Data/StreamingAssets/Images
```

3. 執行 exe。
4. 確認 Console log 或 player log 中完整路徑指向 build 的 `StreamingAssets`。
5. 使用含空格或中文檔名測試時，LevelConfig 仍只填相對路徑，例如：

```text
Videos/Level01/Level01 Intro 中文.mp4
```

## 16. 常見錯誤與排查方式

1. `Invalid StreamingAssets path`
   1. 檢查 LevelConfig 是否空白。
   2. 檢查是否填到錯誤磁碟或 StreamingAssets 外部。

2. `File not found`
   1. 檢查檔案是否真的存在於 `Assets/StreamingAssets`。
   2. 檢查大小寫、資料夾名稱與副檔名。
   3. 目前專案中 Level02 到 Level06 的路徑已填，但實體檔案尚未存在。

3. `Prepare timeout`
   1. 檢查影片格式是否可由 Unity/Windows 解碼。
   2. 暫時提高 `VideoSystemConfig.prepareTimeout`。

4. `First frame timeout`
   1. 檢查影片是否有有效影像軌。
   2. 檢查 RenderTexture 是否已指定到正確 VideoPlayer。
   3. 暫時提高 `VideoSystemConfig.firstFrameTimeout`。

5. 畫面黑掉
   1. 確認三個 VideoPlayer GameObject 都是 active。
   2. 確認 `ContentVideoPlayerA.Target Texture` 是 `RT_ContentA`。
   3. 確認 `ContentVideoPlayerB.Target Texture` 是 `RT_ContentB`。
   4. 確認 `VideoTransitionController.contentVideoRenderer` 指向 Content 顯示面。

## 17. 如何確認事件沒有重複訂閱

1. `VideoPlaybackManager.Subscribe()` 會先 `-=` 再 `+=`，避免同一個 VideoPlayer 重複加入 listener。
2. `OnDisable()` 會解除 `prepareCompleted`、`frameReady`、`loopPointReached`、`errorReceived`。
3. Play Mode 中重複遊玩同一關，Console 每支影片應只出現一次：
   1. prepared
   2. first frame ready
   3. started
   4. completed
4. 若同一支影片完成時出現多次 `completed`，檢查場景是否掛了第二個 `VideoPlaybackManager`。

## 18. 從目前場景完成設定的逐步操作流程

1. 開啟 `Assets/_Interact_Godpod26/Level/LV_LuggageCheck.unity`。
2. 選取場景中的 `VideoPlaybackManager` GameObject。
3. 確認子物件存在：
   1. `IdleVideoPlayer`
   2. `ContentVideoPlayerA`
   3. `ContentVideoPlayerB`
4. 選取 `IdleVideoPlayer`：
   1. `Target Texture` 指定 `RT_IdleVideo`
5. 選取 `ContentVideoPlayerA`：
   1. `Target Texture` 指定 `RT_ContentA`
6. 選取 `ContentVideoPlayerB`：
   1. `Target Texture` 指定 `RT_ContentB`
7. 回到 `VideoPlaybackManager` component：
   1. `idleVideoPlayer` 指定 `IdleVideoPlayer`
   2. `contentVideoPlayerA` 指定 `ContentVideoPlayerA`
   3. `contentVideoPlayerB` 指定 `ContentVideoPlayerB`
8. 在同物件的 `VideoTransitionController`：
   1. `idleVideoTexture` 指定 `RT_IdleVideo`
   2. `contentVideoTextureA` 指定 `RT_ContentA`
   3. `contentVideoTextureB` 指定 `RT_ContentB`
9. 開啟 `VideoSystemConfig.asset`：
   1. `prepareTimeout` 設為 10
   2. `firstFrameTimeout` 設為 5
10. 開啟 LevelConfig：
   1. 確認影片路徑都是 StreamingAssets 相對路徑。
11. 進 Play Mode 做 Idle、Intro、Success、Failure 測試。
12. Build Windows Standalone 後確認 build 內有 `StreamingAssets` 資料夾與影片檔。
