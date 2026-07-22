# VideoPlayer 首幀預熱卡頓修正說明

## 適用範圍

本文說明 LuggageCheck 與 CabinPortraits 兩個內容的影片播放修正：

- `Assets/_Interact_Godpod26_LuggageCheck/Scripts/Video/VideoPlaybackManager.cs`
- `Assets/_Interact_Godpod26_CabinPortraits/Scripts/Video/CabinPortraitVideoCycleController.cs`
- `Assets/_Interact_Godpod26_LuggageCheck/Level/LV_LuggageCheck.unity`
- `Assets/_Interact_Godpod26_CabinPortraits/Level/LV_CabinPortraits.unity`

這次修正主要處理的症狀是：影片第一輪偶發畫面完全停住、音訊播放零點幾秒後卡住，第二輪 loop 或再次播放後恢復正常。

## 原本的症狀判斷

這個問題不像硬碟讀取或一般 GOP 設定造成的持續吞吐不足，原因是：

- SSD 和 HDD 上的發生率接近。
- 畫面不是只有前幾幀掉幀，而是第一輪整段看起來卡住。
- 音訊會先開始一小段，代表 VideoPlayer 已經進入播放狀態，但影像輸出管線沒有同步完成。
- 第二輪通常正常，代表 decoder、RenderTexture、材質輸出與音訊時鐘在第一次啟動後已經被建立。

比較合理的癥結點是：`VideoPlayer.prepareCompleted` 或 `VideoPlayer.isPrepared` 只代表播放前準備完成，不保證第一張影像幀已經真正送進 RenderTexture。當正式 `Play()` 時，音訊時鐘可能先開始，影像幀還沒進輸出管線，造成第一次播放偶發卡住。

## 修正前的做法

修正前的流程大致是：

```text
Prepare()
等待 isPrepared / prepareCompleted
StepForward 或 Play 一下
用 player.frame / player.texture 判斷看起來已有首幀
Pause()
標記影片可播放
正式切換時 Play()
```

這個做法不是沒有理由。它原本想保護幾件事：

- 避免未 Prepare 完成就播放，導致黑畫面或切換失敗。
- 讓 RenderTexture 先有內容，避免正式顯示時看到空白畫面。
- 使用雙 VideoPlayer / 雙 RenderTexture，避免切換 URL 或 RenderTexture 時影響正在顯示的播放器。
- 在 LuggageCheck 中延後 result video prepare，是為了避免 gameplay loop 還在播放時同時解碼下一支 result video，降低尖峰負載。
- CabinPortraits 保持 VideoPlayer 物件啟用，只切 `Renderer.enabled`，也是為了讓播放器管線不要因 GameObject 被停用而被重建。

問題在於，`StepForward()` 和 texture polling 不是可靠的「影像管線已收到新幀」訊號：

- `StepForward()` 觸發後實際解碼與輸出仍可能是非同步。
- `player.texture != null` 只能表示有 texture 物件，不一定表示這次播放的新影像幀已經輸出。
- `player.frame >= 0` 不等於 RenderTexture 已更新到可顯示幀。
- `prepareCompleted` 不等於 `frameReady`。

因此修正前的保護方向是正確的，但 readiness 判斷不夠精準。

## 修正後的做法

修正後改成用 Unity 的 `VideoPlayer.frameReady` 作為首幀確認：

```text
Prepare()
等待 isPrepared / prepareCompleted
預熱期間暫時靜音
Play() 讓 VideoPlayer 真正啟動解碼與輸出
等待 frameReady
收到第一個 frameReady 後立即 Pause()
標記影片可播放
正式切換時恢復音訊並 Play()
```

主要差異：

- 移除 `StepForward()` 作為預熱手段。
- 不再用 `player.frame` 或 `player.texture` 判斷首幀。
- 預熱階段真的呼叫 `Play()`，讓 decoder、音訊管線、影像輸出管線都進入實際播放路徑。
- 預熱期間把音訊暫時 mute，避免使用者聽到預熱音。
- 收到 `frameReady` 後立刻 `Pause()`，將播放器停在已確認輸出的狀態。
- 正式播放前恢復原本音訊狀態。
- `skipOnDrop` 改為 false，避免啟動瞬間音訊時鐘先跑時，VideoPlayer 嘗試跳幀追趕而放大不穩定性。

這個流程比較接近「影片已經真的能出畫面」才讓狀態機往下一步走，而不是只相信 Prepare 已完成。

## 為什麼能改善卡頓

偶發卡住的核心風險是正式播放時才第一次真正啟動影像輸出。修正後把這個風險提前到看不見的預熱階段處理：

- decoder 初始化提前發生。
- 第一張影像幀提前送進 RenderTexture。
- RenderTexture 和材質顯示鏈路提前被寫入有效畫面。
- 音訊在預熱階段被 mute，不會因 Play() 預熱被聽見。
- 正式播放時播放器已經經過一次實際 frame output，不再是冷啟動狀態。

也就是說，修正不是降低影片本身的解碼成本，而是把最容易出問題的第一次啟動成本從「正式顯示當下」移到「被遮住或尚未顯示的準備階段」。

## LuggageCheck 與 CabinPortraits 的差異

兩邊共同改成：

- `frameReady` 才視為首幀可用。
- 預熱播放時 mute。
- 正式播放前 restore mute state。
- `skipOnDrop = false`。

保留差異如下：

- LuggageCheck 仍由 `VideoPlaybackManager` 對外發出 `FirstFrameReady`，再由 `VideoFlowCoordinator` 推進 `GameFlowManager` 狀態。
- CabinPortraits 保留 coroutine 流程控制，因為它的 one-shot 播放、回到初始畫面、轉場遮罩與 `TimerVideoDelay` 都綁在同一套流程裡。
- CabinPortraits 的 prepare timeout 和 first-frame timeout 仍是 warning-only，不會直接進 ErrorRecovery。這是原本輪播內容的容錯設計：如果某支影片首幀比較慢，不希望整個展演流程立刻停在錯誤狀態。
- LuggageCheck 的 result prepare delay 設計保留，因為它是為了避免 gameplay video 還在播放時同時準備 result video，降低同時解碼造成的尖峰負載。

這些差異是流程需求不同，不是首幀預熱策略不同。

## 為什麼沒有強制 seek 回 frame 0

收到第一個 `frameReady` 後，理論上可以 `Pause()`，再設定 `frame = 0`，等待 `seekCompleted`，最後才標記 ready。

這次先沒有這樣做，理由是：

- seek 本身又是一個非同步操作，可能重新引入平台差異。
- 目前問題是第一輪啟動卡住，不是使用者明顯看到影片少了第一幀。
- 對 loop 背景和切換影片來說，穩定播放比嚴格從第 0 幀開始更重要。

如果之後有明確需求要每支影片都從絕對第 0 幀開始，可以再加一個可開關的 seek-back-to-zero 選項，但不建議在這一輪穩定性修正中一起加入。

## 注意事項

- 不要把 VideoPlayer 所在 GameObject 在預熱或播放中 `SetActive(false)`。
- 可以隱藏顯示用 Renderer，但 VideoPlayer 本體和 RenderTexture 應保持有效。
- 不要在正式顯示當下重新指定 URL 或 TargetTexture。
- 若要改影片編碼，仍建議使用 H.264、CFR 30fps、yuv420p、無 B-frame、短 GOP；但這次症狀的主要癥結不是硬碟或 GOP，而是首幀輸出 readiness 判斷。
- 若再次發生偶發卡頓，應優先查看 log 中 prepare、frameReady、started、frameDropped、clockResync 的時間差。
