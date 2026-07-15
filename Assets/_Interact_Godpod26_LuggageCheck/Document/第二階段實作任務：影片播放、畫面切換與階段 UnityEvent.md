# 第二階段實作任務：影片播放、畫面切換與階段 UnityEvent

請先閱讀並理解以下內容：

1. 《Unity RFID 行李安檢互動系統規格書》
2. 《第一階段實作任務：系統流程與關卡資料骨架》
3. 第一階段完成後的腳本、場景結構及測試結果

本階段必須延續第一階段建立的：

* `GameState`
* `GameFlowManager`
* `LevelConfig`
* `LevelDatabase`
* `DebugLevelInput`

不得重新建立另一套流程控制系統，也不得繞過原有狀態機。

---

## 1. 本階段目標

本階段建立影片播放與畫面切換系統，使專案可以：

1. 從 StreamingAssets 讀取影片。
2. 播放待機 Loop 影片。
3. 根據目前關卡播放對應的 Intro 影片。
4. Intro 結束後切換為最後一幀靜態背景。
5. 在 Gameplay 階段以測試按鍵模擬成功或失敗。
6. 播放對應的 Success 或 Failure 影片。
7. 結果影片播放完成後返回待機影片。
8. 所有影片播放前執行 `VideoPlayer.Prepare()`。
9. 影片準備與切換期間保留上一個有效畫面。
10. 避免影片切換時出現黑畫面或空白幀。
11. 為各個流程階段提供可由 Inspector 設定的 UnityEvent。
12. 保持核心流程、影片控制和展示反應彼此分離。

本階段完成後，應能透過數字鍵選擇關卡，實際播放完整的影片流程：

```text
Idle Loop
→ Intro Video
→ Static Gameplay Background
→ Success Video 或 Failure Video
→ Idle Loop
```

本階段的 Gameplay 成功與失敗仍使用 Debug 按鍵模擬。

---

## 2. 本階段暫不實作

本階段不要實作：

* 真實 RFID 硬體通訊
* 行李內容物件同步移動
* 不規則物件方向選取
* 違禁品資料與判定
* 正確或錯誤選擇
* 倒數計時
* 倒數計時 UI
* 違禁品剪影 UI
* 正式粒子特效
* 正式遊戲音效
* 最終關卡美術 Prefab

上述項目會在後續階段實作。

本階段可以透過 UnityEvent 綁定測試用 UI、Animator、AudioSource 或 GameObject 顯示狀態，但不要提前製作正式 Gameplay 功能。

---

# 3. 核心架構原則

## 3.1 模組責任必須分離

不得讓 `GameFlowManager` 直接控制所有 VideoPlayer、RawImage、RenderTexture 或圖片載入。

建議架構如下：

```text
GameFlowManager
        │
        │ 狀態切換事件
        ▼
VideoFlowCoordinator
        │
        ├── VideoPlaybackManager
        ├── StreamingImageLoader
        └── VideoTransitionController
```

各模組完成工作後，再透過正式方法回報：

```text
VideoPlayer 播放完成
        ↓
VideoPlaybackManager
        ↓
VideoFlowCoordinator
        ↓
GameFlowManager.NotifyIntroCompleted()
```

`GameFlowManager` 只負責：

* 保存目前狀態
* 驗證狀態切換
* 保存目前關卡
* 接收各模組完成通知
* 決定下一個系統狀態
* 防止重複或非法流程

`GameFlowManager` 不負責：

* 設定 VideoPlayer URL
* 呼叫 VideoPlayer Prepare
* 控制 RawImage
* 載入 StreamingAssets 圖片
* 控制 RenderTexture
* 判斷影片第一幀是否可見
* 管理影片解碼或逾時

---

## 3.2 UnityEvent 的定位

UnityEvent 用於執行各階段附加的展示行為，例如：

* 顯示或隱藏 UI
* 啟用或停用 GameObject
* 播放 Animator
* 播放 AudioSource
* 播放粒子系統
* 重置場景展示物件
* 啟動行李物件動畫
* 顯示階段提示文字

UnityEvent 不取代核心狀態機。

推薦關係：

```text
GameFlowManager 切換狀態
        │
        ├── C# event：通知程式模組
        │
        └── UnityEvent：執行 Inspector 綁定的展示反應
```

例如進入 `IntroPlaying`：

```text
GameFlowManager
        ├── 通知 VideoFlowCoordinator 播放 Intro
        └── 觸發 onIntroPlayingEntered
                ├── 隱藏待機提示 UI
                ├── 啟用 Intro 裝飾物件
                └── 播放測試 Animator
```

但 Intro 播放完成後，必須由實際影片播放模組回報：

```csharp
gameFlowManager.NotifyIntroCompleted();
```

不得僅依靠 UnityEvent、固定延遲或 Timeline Signal 假設影片已播放完成。

---

## 3.3 事件分層

本系統保留三種溝通方式。

### 公開方法

用於重要流程完成通知：

```csharp
NotifyIdlePrepared();
NotifyLevelInitialized();
NotifyIntroPrepared();
NotifyIntroCompleted();
NotifyGamePrepared();
NotifyGameSuccess();
NotifyGameFailure();
NotifyResultPrepared();
NotifyResultCompleted();
ReportRecoverableError(string message);
```

這些方法由 `GameFlowManager` 驗證目前狀態是否合法。

### C# event

用於程式模組之間的通知，例如：

```csharp
public event Action<GameState, GameState> StateChanged;
public event Action<LevelConfig> LevelStarted;
```

### UnityEvent

用於 Inspector 中可以自由配置的場景展示反應。

不得只保留 UnityEvent，而移除核心 C# 方法與 C# event。

---

# 4. 專案資料夾

在第一階段架構下新增：

```text
Assets/
└── _Interact_Godpod26/
    ├── Scripts/
    │   ├── Core/
    │   ├── Levels/
    │   ├── Input/
    │   ├── Debug/
    │   ├── Video/
    │   ├── Presentation/
    │   └── Utilities/
    │
    └── Data/
        ├── Levels/
        └── System/
```

StreamingAssets 建議結構：

```text
Assets/
└── StreamingAssets/
    ├── Videos/
    │   ├── Common/
    │   │   └── Idle_Loop.mp4
    │   │
    │   ├── Level01/
    │   │   ├── Level01_Intro.mp4
    │   │   ├── Level01_Success.mp4
    │   │   └── Level01_Failure.mp4
    │   │
    │   ├── Level02/
    │   │   ├── Level02_Intro.mp4
    │   │   ├── Level02_Success.mp4
    │   │   └── Level02_Failure.mp4
    │   │
    │   └── ...
    │
    └── Images/
        ├── Level01/
        │   └── Level01_FinalFrame.png
        ├── Level02/
        │   └── Level02_FinalFrame.png
        └── ...
```

所有資料設定只保存相對路徑。

不得保存專案或電腦的絕對路徑。

---

# 5. 更新 GameFlowManager

延續第一階段的 `GameFlowManager`。

## 5.1 核心責任不變

`GameFlowManager` 仍是唯一可以修改主要 `GameState` 的元件。

其他模組不得直接執行：

```csharp
CurrentState = ...
```

也不得透過反射、公開 setter 或 Inspector 修改狀態。

---

## 5.2 狀態完成通知

確認或新增以下方法：

```csharp
public void NotifyIdlePrepared();
public void NotifyLevelInitialized();
public void NotifyIntroPrepared();
public void NotifyIntroCompleted();
public void NotifyGamePrepared();

public void NotifyGameSuccess();
public void NotifyGameFailure();

public void NotifyResultPrepared();
public void NotifyResultCompleted();

public void ReportRecoverableError(string message);
public void ReturnToIdle();
```

每個方法都必須驗證目前狀態。

例如：

* 只有 `IntroPreparing` 可以接受 `NotifyIntroPrepared()`。
* 只有 `IntroPlaying` 可以接受 `NotifyIntroCompleted()`。
* 只有 `Gameplay` 可以接受成功或失敗。
* 只有 `SuccessPreparing` 或 `FailurePreparing` 可以接受 `NotifyResultPrepared()`。
* 只有 `SuccessPlaying` 或 `FailurePlaying` 可以接受 `NotifyResultCompleted()`。

不合法通知：

* 不改變狀態
* 不觸發 UnityEvent
* 在 Console 顯示 Warning

---

# 6. GameFlowManager 階段 UnityEvent

在 `GameFlowManager` 提供 Inspector 可設定的狀態事件。

可以建立：

```csharp
[Serializable]
public class GameStateUnityEvent : UnityEvent<GameState>
{
}
```

至少提供通用事件：

```csharp
[SerializeField]
private GameStateUnityEvent onStateEntered;
```

並提供以下個別事件：

```csharp
[SerializeField] private UnityEvent onSystemInitializingEntered;
[SerializeField] private UnityEvent onIdlePreparingEntered;
[SerializeField] private UnityEvent onIdleEntered;
[SerializeField] private UnityEvent onLevelInitializingEntered;
[SerializeField] private UnityEvent onIntroPreparingEntered;
[SerializeField] private UnityEvent onIntroPlayingEntered;
[SerializeField] private UnityEvent onGamePreparingEntered;
[SerializeField] private UnityEvent onGameplayEntered;
[SerializeField] private UnityEvent onSuccessPreparingEntered;
[SerializeField] private UnityEvent onSuccessPlayingEntered;
[SerializeField] private UnityEvent onFailurePreparingEntered;
[SerializeField] private UnityEvent onFailurePlayingEntered;
[SerializeField] private UnityEvent onResettingEntered;
[SerializeField] private UnityEvent onErrorRecoveryEntered;
```

若 Inspector 欄位過多，可將 UnityEvent 集中在一個可序列化類別中，但事件名稱必須清楚。

---

## 6.1 UnityEvent 觸發順序

每次合法切換狀態後，執行順序必須一致：

1. 保存舊狀態。
2. 更新 `CurrentState`。
3. 輸出 Console 狀態紀錄。
4. 觸發 C# `StateChanged`。
5. 觸發通用 `onStateEntered`。
6. 觸發對應狀態的個別 UnityEvent。

例如：

```text
[GameFlow] IntroPreparing -> IntroPlaying
```

之後才觸發：

```text
StateChanged
→ onStateEntered
→ onIntroPlayingEntered
```

不合法或重複的狀態切換不得觸發任何 UnityEvent。

---

## 6.2 UnityEvent 使用限制

Inspector UnityEvent 可以綁定：

* `GameObject.SetActive`
* UI 顯示及隱藏方法
* Animator Trigger 方法
* AudioSource 播放方法
* ParticleSystem 播放或停止
* 場景展示物件的 Reset 方法
* 無核心流程責任的自訂公開方法

Inspector UnityEvent 不得綁定：

* 直接修改 `CurrentState`
* 直接修改 `CurrentLevel`
* 同時呼叫成功及失敗
* 以固定秒數呼叫下一階段
* 繞過 VideoPlayer 事件宣告影片完成
* 反覆呼叫目前狀態的進入方法
* 可能形成循環狀態切換的方法
* `NotifyIntroCompleted()` 等核心完成通知，除非只是專門的 Debug 測試

正式流程的完成通知，必須由實際負責工作的模組呼叫。

---

# 7. 建立 VideoSystemConfig

建立 `VideoSystemConfig.cs`，型別為 `ScriptableObject`。

建議內容：

```csharp
[CreateAssetMenu(
    fileName = "VideoSystemConfig",
    menuName = "RFID Baggage/Video System Config"
)]
public class VideoSystemConfig : ScriptableObject
{
    [SerializeField]
    private string idleVideoRelativePath;

    [SerializeField]
    private float prepareTimeout = 10f;
}
```

需要提供唯讀 public property。

至少包含：

* 待機影片相對路徑
* Prepare 逾時時間
* 是否輸出詳細影片 Log
* 必要時加入第一幀等待逾時時間

不得在六個 `LevelConfig` 中重複保存相同的待機影片路徑。

---

# 8. LevelConfig 影片欄位

確認 `LevelConfig` 至少包含：

```csharp
string introVideoRelativePath;
string successVideoRelativePath;
string failureVideoRelativePath;
string finalFrameImageRelativePath;
```

路徑範例：

```text
Videos/Level01/Level01_Intro.mp4
Videos/Level01/Level01_Success.mp4
Videos/Level01/Level01_Failure.mp4
Images/Level01/Level01_FinalFrame.png
```

本階段應檢查：

* 路徑是否為空
* 路徑是否包含無效格式
* Windows Standalone Build 中檔案是否存在

若資料錯誤，不得直接進入影片播放狀態。

---

# 9. 建立 StreamingAssetsPathUtility

建立 `StreamingAssetsPathUtility.cs`。

責任：

* 將相對路徑轉為完整 StreamingAssets 路徑。
* 正規化 Windows 路徑分隔符號。
* 拒絕空白路徑。
* 避免重複加入 StreamingAssets 根路徑。
* 提供檔案存在性檢查。
* 必要時建立可供 VideoPlayer 使用的 URL。

建議介面：

```csharp
public static bool TryBuildFilePath(
    string relativePath,
    out string fullPath
);

public static bool TryBuildFileUri(
    string relativePath,
    out string fileUri
);

public static bool FileExists(
    string relativePath
);
```

主要根路徑使用：

```csharp
Application.streamingAssetsPath
```

不得在關卡資料中保存：

```text
C:/Unity_Interact_Godpod26s/...
```

---

# 10. 建立 VideoPlaybackManager

建立 `VideoPlaybackManager.cs`。

## 10.1 主要責任

* 管理 VideoPlayer。
* 設定 StreamingAssets URL。
* 執行 `Prepare()`。
* 等待 Prepare 完成。
* 管理 Prepare 逾時。
* 確認第一個有效畫面。
* 開始播放影片。
* 監聽影片播放完成。
* 監聽 VideoPlayer 錯誤。
* 防止舊的播放事件影響新流程。
* 防止完成事件重複回報。

---

## 10.2 不負責內容

`VideoPlaybackManager` 不負責：

* 切換 GameState
* 判定成功或失敗
* 選擇目前關卡
* 處理 RFID
* 管理違禁品
* 啟動倒數計時
* 直接控制所有場景 UI
* 決定播放哪個流程階段

播放哪一種影片，由 `VideoFlowCoordinator` 根據目前狀態決定。

---

## 10.3 影片用途列舉

建立：

```csharp
public enum VideoContentType
{
    None,
    Idle,
    Intro,
    Success,
    Failure
}
```

播放器必須記錄：

* 目前準備中的影片類型
* 目前播放中的影片類型
* 目前操作 Token 或版本序號
* 是否已回報完成
* 是否由正常播放結束
* 是否為 Loop 影片

如果舊的 Prepare 或播放事件晚於新要求回傳，必須忽略舊事件。

---

# 11. VideoPlayer 配置

本階段預設使用兩組 VideoPlayer：

```text
IdleVideoPlayer
ContentVideoPlayer
```

用途：

| VideoPlayer        | 用途                       |
| ------------------ | ------------------------ |
| IdleVideoPlayer    | 播放待機 Loop                |
| ContentVideoPlayer | 播放 Intro、Success、Failure |

每組 VideoPlayer：

* 使用 URL Source
* 播放 StreamingAssets 影片
* 輸出至固定 RenderTexture
* `Play On Awake` 關閉
* `Wait For First Frame` 開啟
* Idle 開啟 Loop
* Content 關閉 Loop
* 不在每次播放時建立新 RenderTexture

不建議在本階段無條件同時建立四個高解析度 VideoPlayer。

若日後測試證明雙播放器在切換 Success／Failure 時仍有等待，再評估增加結果預載播放器。

---

# 12. VideoPlaybackManager 公開介面

可依實際設計調整，但需具備以下語意：

```csharp
public bool IsPreparing { get; }
public bool IsPlaying { get; }
public VideoContentType CurrentContentType { get; }

public void Prepare(
    VideoContentType contentType,
    string relativePath,
    bool loop
);

public bool PlayPrepared(
    VideoContentType expectedContentType
);

public void StopContent();
public void StopAll();
```

也可以增加較清楚的包裝方法：

```csharp
public void PrepareIdle(string relativePath);
public void PrepareIntro(string relativePath);
public void PrepareSuccess(string relativePath);
public void PrepareFailure(string relativePath);
```

公開 API 不得讓 Inspector 使用者任意傳入容易造成錯誤的狀態組合。

---

# 13. VideoPlaybackManager 事件

提供 C# event：

```csharp
public event Action<VideoContentType> VideoPrepared;
public event Action<VideoContentType> FirstFrameReady;
public event Action<VideoContentType> VideoStarted;
public event Action<VideoContentType> VideoCompleted;
public event Action<VideoContentType, string> VideoFailed;
```

必要時提供 Inspector UnityEvent：

```csharp
[Serializable]
public class VideoContentTypeUnityEvent
    : UnityEvent<VideoContentType>
{
}
```

例如：

```csharp
[SerializeField]
private VideoContentTypeUnityEvent onVideoPrepared;

[SerializeField]
private VideoContentTypeUnityEvent onFirstFrameReady;

[SerializeField]
private VideoContentTypeUnityEvent onVideoStarted;

[SerializeField]
private VideoContentTypeUnityEvent onVideoCompleted;

[SerializeField]
private UnityEvent onVideoError;
```

分工：

* C# event：正式程式流程
* UnityEvent：額外展示與測試反應

不得依靠 Inspector UnityEvent 連接核心狀態流程。

---

# 14. VideoPlayer 事件處理

至少監聽：

```csharp
prepareCompleted
loopPointReached
errorReceived
```

必要時使用：

```csharp
frameReady
seekCompleted
```

規則：

* Idle 的 `loopPointReached` 只是下一次循環，不代表流程完成。
* Intro、Success、Failure 的 `loopPointReached` 代表自然播放完成。
* 手動呼叫 Stop 不得誤判為自然播放完成。
* 每支影片只能回報一次完成。
* 過期 Token 對應的事件必須被忽略。
* 所有事件需在 `OnDisable` 或 `OnDestroy` 正確解除訂閱。
* 重新啟用後不得重複訂閱。

不得使用：

```csharp
yield return new WaitForSeconds(videoLength);
```

作為正式播放完成判斷。

---

# 15. Prepare 與逾時機制

所有影片播放前必須：

1. 設定 URL。
2. 呼叫 `Prepare()`。
3. 等待 `prepareCompleted`。
4. 確認 `isPrepared`。
5. 等待第一個有效畫面。
6. 才能進行畫面切換與播放。

Prepare 期間：

* 保留上一個有效畫面
* 不先隱藏目前背景
* 不顯示未準備完成的 VideoPlayer
* 不啟動下一階段 Gameplay

若超過 `VideoSystemConfig.prepareTimeout`：

1. 取消目前操作。
2. 忽略之後抵達的舊 Prepare 事件。
3. 保留上一個有效畫面。
4. 輸出錯誤資訊。
5. 透過協調器呼叫：

```csharp
gameFlowManager.ReportRecoverableError(message);
```

6. 系統進入安全重置流程。

---

# 16. 第一個有效畫面確認

僅有 `prepareCompleted` 不一定代表 RawImage 已經顯示有效像素。

因此切換前需確認第一個有效畫面。

可採用：

* `sendFrameReadyEvents = true`
* 監聽 `frameReady`
* Prepare 完成後定位至第 0 幀
* 確認 VideoPlayer texture 或 RenderTexture 已有有效內容
* 設定第一幀等待逾時

執行順序：

```text
Prepare completed
→ 等待第一個有效畫面
→ 顯示新影片畫面
→ 開始播放
→ 隱藏上一個畫面
```

不得在 `prepareCompleted` 同一幀先清空上一個畫面。

實作完成後需說明採用哪一種第一幀確認方式。

---

# 17. 建立 StreamingImageLoader

建立 `StreamingImageLoader.cs`。

責任：

* 從 StreamingAssets 載入 PNG 或 JPG。
* 接受相對路徑。
* 使用非同步方式讀取。
* 建立可供 RawImage 顯示的 Texture2D。
* 載入期間保留目前畫面。
* 載入失敗時回報錯誤。
* 避免同一圖片重複載入。
* 關卡結束時釋放不再使用的動態 Texture2D。

建議介面：

```csharp
public bool IsLoading { get; }
public Texture2D CurrentTexture { get; }

public void Load(
    string relativePath,
    Action<Texture2D> onCompleted,
    Action<string> onFailed
);

public void ReleaseCurrentTexture();
```

不得假設 StreamingAssets 圖片是 Unity Inspector 可直接引用的 Sprite。

若圖片改為一般 Unity Asset 直接引用，需先說明並取得規格變更，不要自行改變資料方案。

---

# 18. 建立 VideoTransitionController

建立 `VideoTransitionController.cs`。

## 18.1 責任

管理以下顯示層：

* Idle Video RawImage
* Content Video RawImage
* Static Background RawImage
* Loading 或 Error Overlay

建議方法：

```csharp
public void ShowIdleVideo();
public void ShowContentVideo();
public void ShowStaticBackground(Texture texture);
public void ShowLoadingOverlay();
public void HideLoadingOverlay();
public void HideAll();
```

---

## 18.2 切換原則

所有切換遵守：

1. 下一個內容完成載入。
2. 下一個內容已有有效畫面。
3. 先顯示下一個內容。
4. 確認下一個內容已啟用。
5. 再隱藏上一個內容。

禁止流程：

```text
先隱藏目前內容
→ 顯示黑畫面
→ 等待下一支影片 Prepare
```

本階段可以直接切換 GameObject 或 CanvasGroup，不必製作正式淡入淡出。

但需保留未來加入轉場動畫的接口。

---

# 19. 建立 VideoFlowCoordinator

建立 `VideoFlowCoordinator.cs`。

這是本階段的主要流程協調模組。

## 19.1 模組關係

`VideoFlowCoordinator` 連接：

```text
GameFlowManager
LevelConfig
VideoSystemConfig
VideoPlaybackManager
StreamingImageLoader
VideoTransitionController
```

它負責理解目前 GameState 對應的影片工作，但不得直接修改 `GameState`。

只能透過 `GameFlowManager` 的正式通知方法推進流程。

---

## 19.2 訂閱方式

`VideoFlowCoordinator` 訂閱：

```csharp
GameFlowManager.StateChanged
```

並根據新狀態執行工作。

同時訂閱：

```csharp
VideoPlaybackManager.VideoPrepared
VideoPlaybackManager.FirstFrameReady
VideoPlaybackManager.VideoCompleted
VideoPlaybackManager.VideoFailed
```

所有訂閱必須正確解除。

不要同時透過 C# event 與 UnityEvent 重複執行同一核心影片操作。

---

## 19.3 IdlePreparing

當進入 `IdlePreparing`：

1. 取得 `VideoSystemConfig.idleVideoRelativePath`。
2. 檢查檔案是否存在。
3. 要求 `VideoPlaybackManager` 準備 Idle。
4. 保留目前有效畫面。
5. 等待 Prepare 與第一幀完成。
6. 顯示 Idle Video。
7. 播放 Idle Loop。
8. 呼叫：

```csharp
gameFlowManager.NotifyIdlePrepared();
```

9. 系統進入 Idle。

若 Idle 影片本來仍在播放且有效，可避免不必要的重新 Prepare。

---

## 19.4 LevelInitializing

當進入 `LevelInitializing`：

1. 取得 `GameFlowManager.CurrentLevel`。
2. 驗證目前關卡不為 null。
3. 驗證 Intro、Success、Failure 與 Final Frame 路徑。
4. 清除上一關卡的圖片資源。
5. 重置本階段內部 Token 與完成旗標。
6. 基礎初始化完成後呼叫：

```csharp
gameFlowManager.NotifyLevelInitialized();
```

本階段尚無正式 `LevelController` 時，可只做影片資料初始化。

---

## 19.5 IntroPreparing

當進入 `IntroPreparing`：

1. Idle 影片繼續顯示。
2. 準備目前關卡 Intro 影片。
3. 同時非同步載入 Final Frame 圖片。
4. 等待 Intro Prepare 完成。
5. 等待 Intro 第一個有效畫面。
6. 圖片載入若尚未完成，可繼續在背景處理。
7. Intro 已可播放後呼叫：

```csharp
gameFlowManager.NotifyIntroPrepared();
```

不得先停止 Idle 影片再等待 Intro 載入。

---

## 19.6 IntroPlaying

當進入 `IntroPlaying`：

1. 確認 Intro 仍為目前準備完成的影片。
2. 先顯示 Content Video RawImage。
3. 開始播放 Intro。
4. 確認 Intro 畫面已顯示。
5. 再隱藏或暫停 Idle Video。
6. 等待 Intro 自然播放完成。
7. 呼叫：

```csharp
gameFlowManager.NotifyIntroCompleted();
```

不得使用固定秒數推進。

---

## 19.7 GamePreparing

當進入 `GamePreparing`：

1. 確認 Final Frame 圖片已載入。
2. 將圖片設定至 Static Background RawImage。
3. 先顯示靜態背景。
4. 再隱藏 Content Video RawImage。
5. 停止或清理 Intro 播放狀態。
6. 確認背景切換完成。
7. 呼叫：

```csharp
gameFlowManager.NotifyGamePrepared();
```

8. 系統進入 Gameplay。

如果圖片尚未載入完成：

* 保留 Intro 最後畫面
* 繼續等待圖片
* 不顯示黑畫面
* 超時後進入錯誤恢復

---

## 19.8 Gameplay

Gameplay 階段：

* 維持 Static Background 顯示。
* 本階段不啟動正式倒數。
* 本階段不執行物件選取。
* 使用 Debug 按鍵 S 或 F 回報成功／失敗。
* 可以透過 `onGameplayEntered` 顯示測試 UI。

---

## 19.9 SuccessPreparing

當進入 `SuccessPreparing`：

1. 保持 Static Background 顯示。
2. 準備目前關卡 Success 影片。
3. 等待 Prepare 完成。
4. 等待第一個有效畫面。
5. 呼叫：

```csharp
gameFlowManager.NotifyResultPrepared();
```

---

## 19.10 FailurePreparing

當進入 `FailurePreparing`：

1. 保持 Static Background 顯示。
2. 準備目前關卡 Failure 影片。
3. 等待 Prepare 完成。
4. 等待第一個有效畫面。
5. 呼叫：

```csharp
gameFlowManager.NotifyResultPrepared();
```

---

## 19.11 SuccessPlaying 與 FailurePlaying

進入結果播放狀態後：

1. 確認目前影片類型與狀態相符。
2. 先顯示 Content Video。
3. 開始播放結果影片。
4. 再隱藏 Static Background。
5. 等待影片自然播放完成。
6. 呼叫：

```csharp
gameFlowManager.NotifyResultCompleted();
```

禁止 Success 狀態播放 Failure 影片，反之亦然。

---

## 19.12 Resetting

當進入 `Resetting`：

1. 停止 Content Video。
2. 隱藏 Content Video 顯示層。
3. 清除 Static Background。
4. 釋放目前關卡動態載入的 Texture2D。
5. 清除影片操作 Token。
6. 清除完成旗標。
7. 停止本關卡相關測試展示。
8. 確認 Idle Video 是否仍可使用。
9. 完成後讓 `GameFlowManager` 進入 `IdlePreparing`。
10. 重新顯示及播放待機影片。

重置過程不可重新開放關卡選擇，直到正式進入 Idle。

---

## 19.13 ErrorRecovery

當影片發生以下狀況：

* 路徑不存在
* Prepare 失敗
* Prepare 逾時
* 第一幀逾時
* 播放過程收到錯誤
* Final Frame 圖片載入失敗

`VideoFlowCoordinator` 必須：

1. 保留目前最後有效畫面。
2. 停止目前影片操作。
3. 清除過期 Token。
4. 輸出完整錯誤。
5. 呼叫：

```csharp
gameFlowManager.ReportRecoverableError(message);
```

6. 配合 `ErrorRecovery → Resetting → IdlePreparing → Idle` 返回待機。

不得永久停留在 Preparing 狀態。

---

# 20. 無黑畫面切換流程

## 20.1 Idle 至 Intro

```text
Idle 持續播放
→ Intro Prepare
→ Intro 第一幀有效
→ 顯示 Intro
→ 播放 Intro
→ 隱藏或暫停 Idle
```

---

## 20.2 Intro 至 Static Background

```text
Intro 播放完成
→ Final Frame 圖片已載入
→ 顯示 Static Background
→ 隱藏 Content Video
```

---

## 20.3 Static Background 至 Result

```text
Static Background 保持顯示
→ Success 或 Failure Prepare
→ Result 第一幀有效
→ 顯示 Result Video
→ 播放 Result
→ 隱藏 Static Background
```

---

## 20.4 Result 至 Idle

```text
結果影片播放完成
→ 準備或恢復 Idle
→ Idle 第一幀有效
→ 顯示 Idle
→ 播放 Idle Loop
→ 隱藏 Result
```

任何切換都不得先呼叫 `HideAll()` 再等待新內容。

---

# 21. DebugLevelInput 更新

第二階段完成後，下列手動流程按鍵應取消或預設停用：

| 第一階段按鍵 | 第二階段處理                            |
| ------ | --------------------------------- |
| I      | 關卡初始化由 Coordinator 自動完成           |
| P      | Intro Prepare 由 VideoPlayer 事件完成  |
| O      | Intro 播放完成由 VideoPlayer 事件完成      |
| G      | 靜態背景完成後自動進入 Gameplay              |
| R      | Result Prepare 由 VideoPlayer 事件完成 |
| E      | Result 播放完成由 VideoPlayer 事件完成     |

保留：

| 按鍵      | 功能              |
| ------- | --------------- |
| 數字鍵 1～6 | 在 Idle 選擇測試關卡   |
| S       | 在 Gameplay 模擬成功 |
| F       | 在 Gameplay 模擬失敗 |
| Escape  | 執行安全重置          |

Debug 輸入仍必須透過 `GameFlowManager` 的正式介面，不得直接修改狀態。

---

# 22. 場景階層建議

在主場景建立：

```text
Systems
├── GameFlowManager
├── VideoFlowCoordinator
├── VideoPlaybackManager
├── StreamingImageLoader
├── VideoTransitionController
└── DebugLevelInput

Presentation
├── IdleVideo
│   └── IdleVideoPlayer
│
├── ContentVideo
│   └── ContentVideoPlayer
│
└── BackgroundCanvas
    ├── IdleVideoRawImage
    ├── ContentVideoRawImage
    ├── StaticBackgroundRawImage
    ├── LoadingOverlay
    └── DebugStateText
```

RenderTexture 建議作為專案資產預先建立，例如：

```text
Assets/_Interact_Godpod26/RenderTextures/
├── RT_IdleVideo.renderTexture
└── RT_ContentVideo.renderTexture
```

不可每次進入關卡都建立新的 RenderTexture。

---

# 23. UnityEvent 測試配置

本階段至少配置以下測試事件。

## onIdleEntered

* 顯示 Idle 測試文字。
* 隱藏 Gameplay 測試提示。
* 停止測試 Animator。

## onLevelInitializingEntered

* 顯示目前載入中的關卡名稱。
* 重置測試展示物件。

## onIntroPlayingEntered

* 顯示 Intro 測試文字。
* 可播放測試 Animator Trigger。
* 隱藏 Idle 提示。

## onGameplayEntered

* 顯示 Gameplay 測試文字。
* 顯示「S：Success／F：Failure」提示。

## onSuccessPreparingEntered

* 顯示 Success Preparing 測試文字。

## onSuccessPlayingEntered

* 顯示 Success 測試文字。
* 可播放成功測試音效。

## onFailurePreparingEntered

* 顯示 Failure Preparing 測試文字。

## onFailurePlayingEntered

* 顯示 Failure 測試文字。
* 可播放失敗測試音效。

## onResettingEntered

* 隱藏所有關卡測試提示。
* 重置測試 Animator。
* 停止測試音效。

UnityEvent 的 Listener 即使全部移除，影片核心流程仍必須正常運作。

---

# 24. Console 紀錄

影片準備：

```text
[Video] Preparing Intro:
<完整影片路徑>
```

Prepare 完成：

```text
[Video] Intro prepared.
```

第一幀完成：

```text
[Video] Intro first frame ready.
```

開始播放：

```text
[Video] Intro started.
```

播放完成：

```text
[Video] Intro completed.
```

畫面切換：

```text
[Transition] IdleVideo -> ContentVideo
```

```text
[Transition] ContentVideo -> StaticBackground
```

```text
[Transition] StaticBackground -> ContentVideo
```

載入錯誤：

```text
[Video] File not found:
<完整路徑>
```

Prepare 逾時：

```text
[Video] Prepare timeout:
Videos/Level01/Level01_Intro.mp4
```

過期事件：

```text
[Video] Ignored stale callback. Token: 4
```

不需要每幀輸出影片時間或播放幀。

---

# 25. 本階段驗收流程

## 測試一：待機影片

1. 進入 Play Mode。
2. 系統進入 `IdlePreparing`。
3. 系統自動準備待機影片。
4. 待機影片第一幀準備完成。
5. 畫面顯示待機影片。
6. 系統進入 Idle。
7. 待機影片持續 Loop。
8. Loop 結束事件不會推進狀態。

---

## 測試二：進場影片

1. 在 Idle 按數字鍵 1。
2. 系統取得 Level_01。
3. 系統進入 LevelInitializing。
4. 自動進入 IntroPreparing。
5. 待機影片保持顯示。
6. Intro 影片執行 Prepare。
7. Intro 第一幀有效後進入 IntroPlaying。
8. 顯示及播放 Intro。
9. 再隱藏待機影片。
10. Intro 播放完成後自動進入 GamePreparing。
11. 不需要使用 I、P 或 O。

---

## 測試三：靜態背景

1. Intro 播放期間載入 Final Frame。
2. Intro 播放完成。
3. Final Frame 先顯示。
4. Content Video 再隱藏。
5. 畫面不出現黑色或空白幀。
6. 系統自動進入 Gameplay。
7. 不需要使用 G。

---

## 測試四：成功流程

1. 在 Gameplay 按 S。
2. 系統進入 SuccessPreparing。
3. Static Background 持續顯示。
4. Success 影片執行 Prepare。
5. 第一幀有效後進入 SuccessPlaying。
6. 顯示及播放 Success。
7. 再隱藏 Static Background。
8. 播放完成後自動進入 Resetting。
9. 系統返回 Idle。
10. 不需要使用 R 或 E。

---

## 測試五：失敗流程

1. 在 Gameplay 按 F。
2. 系統進入 FailurePreparing。
3. Static Background 持續顯示。
4. Failure 影片執行 Prepare。
5. 第一幀有效後進入 FailurePlaying。
6. 播放完成後自動返回 Idle。

---

## 測試六：UnityEvent

確認：

* 每次進入狀態時對應 UnityEvent 只執行一次。
* 不合法操作不會觸發 UnityEvent。
* UnityEvent 可以控制 UI、Animator、AudioSource 及 GameObject。
* 移除所有 Inspector Listener 後，核心影片流程仍正常。
* UnityEvent 不會直接改變 `GameState`。
* 重複遊玩後 Listener 不會每局增加一次。
* UnityEvent 執行的展示功能不會重複推進核心流程。

---

## 測試七：錯誤恢復

至少測試：

* Idle 影片路徑不存在。
* Intro 路徑不存在。
* Success 路徑不存在。
* Failure 路徑不存在。
* Final Frame 圖片不存在。
* 影片格式無法播放。
* Prepare 超過設定時間。
* 第一幀等待逾時。
* 播放過程收到 VideoPlayer error。

除 Idle 本身無法播放需使用備用錯誤畫面外，其餘錯誤均需：

* 保留最後有效畫面
* 不永久停在 Preparing
* 輸出清楚錯誤
* 進入安全重置
* 最終返回 Idle

---

## 測試八：重複遊玩

依序測試：

```text
Level_01 Success
Level_01 Failure
Level_03 Success
Level_06 Failure
```

確認：

* 每次讀取正確影片。
* 上一關圖片不殘留。
* UnityEvent 不重複訂閱。
* VideoPlayer 事件不重複回報。
* 舊 Prepare callback 不影響新關卡。
* 動態 Texture2D 不持續累積。
* 系統每次都能返回 Idle。
* Unity Console 沒有未處理例外。

---

# 26. 效能與穩定性要求

* 不在 Update 中重複組合檔案路徑。
* 不在 Update 中使用 `GameObject.Find`。
* 不在每次關卡建立新的 RenderTexture。
* 不在每次切換建立新的 VideoPlayer。
* 不重複訂閱 VideoPlayer 事件。
* 不保留上一關不再使用的動態 Texture2D。
* 不在影片尚未準備時關閉目前畫面。
* 不依靠固定影片秒數判斷播放完成。
* 不讓 `GameFlowManager` 承擔影片實作細節。
* 不讓 UnityEvent 成為唯一的核心流程連接方式。
* 多次遊玩後記憶體不可持續明顯增加。
* VideoPlayer 錯誤不得造成系統永久卡住。

---

# 27. 完成後回報內容

完成後請提供：

1. 新增及修改的檔案清單。
2. 每支腳本的責任。
3. `GameFlowManager`、`VideoFlowCoordinator` 與影片模組之間的關係。
4. VideoPlayer 數量與用途。
5. RenderTexture 及 RawImage 配置。
6. StreamingAssets 路徑組合方式。
7. Prepare 及逾時機制。
8. 第一個有效畫面的確認方式。
9. Final Frame 圖片載入方式。
10. 無黑畫面切換的實際執行順序。
11. C# event 與 UnityEvent 的分工。
12. Inspector 中需要人工設定的 UnityEvent。
13. 動態 Texture2D 的釋放方式。
14. 錯誤恢復流程。
15. Debug 按鍵操作方式。
16. 已知限制與風險。
17. 確認沒有繞過 `GameFlowManager` 修改狀態。
18. 確認 Unity Console 無編譯錯誤。
19. 確認未實作第三階段的倒數、選取與違禁品功能。
20. 確認未修改規格範圍外的既有功能。

不要在未說明的情況下繼續實作第三階段。
