# GODPOD 26｜技術美術 × 互動工程作品集文案

## 專案總覽

GODPOD 26 包含兩套以 Unity 製作的展場互動內容：`_Interact_Godpod26_CabinPortraits` 與 `_Interact_Godpod26_LuggageCheck`。兩者共享「狀態驅動流程、外部輸入、即時影片與場景演出」的設計核心，並分別聚焦於多影片互動展示，以及結合 RFID 與風格化 X-ray 畫面的限時辨識遊戲。

在此專案中，技術美術與互動工程不是兩條分離的工作線，而是共同服務於現場體驗：以模組化狀態機維持長時間運行的穩定性，以資料驅動方式管理展演內容，再透過 URP Renderer Feature、Shader Graph、Stencil Buffer、RenderTexture 與事件系統，把硬體訊號、遊戲流程及視覺回饋整合成一致的互動節奏。

**角色定位**：Technical Artist / Interactive Engineer  
**核心技術**：Unity 6000.4.4f1、C#、URP 17.4、Shader Graph、Scriptable Renderer Feature、RenderGraph、Stencil Buffer、Render Objects、ScriptableObject、VideoPlayer、RenderTexture、MaterialPropertyBlock、RFID USB HID / Keyboard Wedge

---

## 01｜Cabin Portraits

### 一句話介紹

以按鈕觸發與閒置計時共同驅動的動態艙房肖像，將多組角色影片、實體操作與遮擋轉場整合成可持續循環的展場內容。

### 玩法

畫面平時維持艙房肖像的初始構圖；觀眾按下實體按鈕後，系統依序播放一段角色動態影片，並透過艙門／遮擋動畫完成畫面切換。影片結束後回到初始狀態，等待下一次操作。

在無人操作時，系統會依計時器自動播放另一組環境演出，讓裝置保持動態；若觀眾在自動演出期間按下按鈕，手動互動會取得優先權，在可控的中斷延遲後接手播放。除主要循環內容外，系統亦保留可調機率的特殊影片分支，增加重複體驗時的變化。

### 作品集主文案

Cabin Portraits 是一套為長時間展演設計的多影片互動系統。我將手動按鈕、閒置計時與特殊機率事件拆分為獨立序列，再由單一狀態機統一仲裁播放權、輸入鎖定與回復流程。系統以持續存在的 VideoPlayer／RenderTexture 插槽管理手動與自動內容，播放前先完成 Prepare 與首幀確認，並在畫面被美術轉場完全遮住時切換顯示來源，降低解碼延遲造成的黑畫面或跳幀感。

影片顯示層與流程控制彼此分離，透過 MaterialPropertyBlock 將目前 RenderTexture 指派至對應 Renderer，不需為每次切換建立新材質。流程節點再以 UnityEvent 對接 Animator、音效與回饋演出，使美術可以在 Inspector 內調整開門、關門、遮擋及播放時序，而不必修改核心程式。

### 技術特色

- **狀態機與輸入仲裁**：明確區分初始化、初始畫面、手動／計時影片準備、播放、回場與錯誤復原；手動輸入可安全中斷自動內容，避免多個 Coroutine 或播放器同時爭用顯示畫面。
- **多序列資料設計**：主要影片、計時影片與機率影片分別由 ScriptableObject 管理，影片數量、起始索引、觸發間隔、轉場提前量與中斷延遲皆可由資料調整。
- **首幀預熱**：影片完成 Prepare 後，以靜音方式取得首幀並暫停，確認 RenderTexture 已有有效畫面後才開放顯示，提升外部影片播放的穩定度。
- **演出與邏輯解耦**：以狀態事件、影片事件與來源別事件串接 Animator／音效／Feedback，讓互動工程維持流程正確性，同時保留技術美術調整演出節奏的空間。
- **展場錯誤復原**：檢查 StreamingAssets 路徑、播放器狀態與事件回呼；失敗時停止當前操作、回到安全初始畫面並重新開放輸入，避免裝置卡在不可操作狀態。

### 架構摘要

`實體按鈕／閒置計時 → 輸入仲裁 → 影片序列選擇 → Prepare 與首幀確認 → RenderTexture 顯示 → 遮擋轉場 → 回到初始狀態`

---

## 02｜Luggage Check

### 一句話介紹

結合 RFID 關卡辨識、限時物件選擇與 Stencil X-ray 視覺的行李安檢互動，將硬體輸入、影片演出與即時遊戲整合為完整的展場循環。

### 玩法

裝置平時播放待機影片；觀眾將 RFID 卡片／物件靠近讀卡器後，系統辨識對應資料並載入六組行李關卡之一。關卡進場影片播放完成後，觀眾依提示啟動掃描，並使用方向控制在不規則排列的行李物件間移動焦點，再按下確認鍵判斷違禁品。

選對時，物件會完成掃描回饋並更新下方提示；選錯則保留物件並播放錯誤特效與音效。觀眾需在倒數結束前找出所有違禁品，系統依結果播放成功或失敗影片，隨後重置物件、UI、特效與輸入狀態並返回待機。

### 作品集主文案

Luggage Check 的核心挑戰，是在單一場景中協調 RFID、影片、限時玩法、場景物件與多層渲染。我以明確的流程狀態機管理 Idle、關卡初始化、影片準備／播放、Gameplay、Success／Failure、Reset 與 Error Recovery；各子系統只回報事件，不直接跨模組改寫狀態，降低影片回呼、玩家輸入與硬體訊號互相競爭所造成的流程錯亂。

六組關卡以 ScriptableObject 保存 RFID 對應、影片路徑、遊戲時間與操作參數，場景端則只維護各關卡的物件 View 與回饋事件。這讓內容資料、場景引用與核心流程各自維持清楚邊界，也能在不修改狀態機的前提下替換 RFID、媒體或關卡配置。

視覺上，我以 URP Renderer Feature 與 Stencil Buffer 建立單相機的區域化後處理。不可見的 Stencil Writer 只寫入遮罩、不輸出顏色與深度，並用不同 Reference Value 區分 X-ray 區域與背景模糊區域；後續 Full Screen Pass 取樣 Camera Color，只在對應 Stencil 測試通過的像素執行效果。搭配 Items／Upper Objects 的分層 Render Pass 排程，使行李物件、掃描視窗、UI 與前景遮擋能在正確順序合成，同時避免全畫面效果外溢。

### 架構設計

- **單一流程真相來源**：`GameFlowManager` 持有合法狀態轉移，影片、Gameplay 與顯示模組以事件訂閱回應狀態，並以通知回報完成，不自行跳轉流程。
- **資料與場景分離**：`LevelDatabase`／`LevelConfig` 管理 RFID、媒體與規則；`GameplayController` 管理 Scene View、物件集合、倒數與結果，使六個關卡共享同一套程式骨架。
- **事件驅動演出**：C# event 負責模組間通訊，UnityEvent 負責連接 Animator、材質參數、粒子、音效與 Feedback，兼顧程式可維護性與美術調整效率。
- **安全重置與復原**：統一處理重複結果、過期影片回呼、資源錯誤與中途重置；每輪結束後恢復可選物件、剪影提示、倒數與播放狀態，支援長時間反覆遊玩。

### RFID 硬體串接

- **USB HID／Keyboard Wedge**：RFID 讀卡器以鍵盤字元輸出標籤 ID，並以 Enter 作為一筆資料的結束訊號；Unity 端不綁定特定廠牌 SDK，也不直接依賴 Serial Port。
- **輸入緩衝與驗證**：接收層處理字元緩衝、最大長度、逾時清除、空白修剪與無效 ID，降低殘留字元或不完整掃描造成的誤觸發。
- **資料驅動映射**：讀取結果經統一入口送入 `LevelDatabase`，由正規化後的 RFID ID 對應 LevelConfig，並支援同一關卡配置多個有效標籤。
- **狀態閘門**：僅在 Idle 且輸入鎖定期結束後接受 RFID；關卡進行中或系統重置期間的重複掃描會被拒絕，避免卡片停留、連續刷卡或跨階段訊號破壞流程。
- **可替換的裝置邊界**：硬體輸入最終收斂為字串型關卡請求，因此未來若改用 Serial、網路服務或原廠 SDK，只需替換接收端，不必重寫關卡與遊戲流程。

### Render Feature / Stencil 渲染

- **雙 Stencil 區域**：使用 Reference 2 標記 X-ray 掃描範圍、Reference 3 標記背景 Blur 範圍；兩種效果共用 depth-stencil attachment，仍能保持彼此獨立。
- **零顏色遮罩寫入**：Stencil Writer 採 `ColorMask 0`、`ZWrite Off` 與 `ZTest LEqual`，只在可見幾何範圍更新 Stencil，不產生額外畫面內容。
- **X-ray Full Screen Pass**：在後處理前取樣 Camera Color，透過 Stencil Equal 測試限制效果範圍，再以 Shader Graph 組合色彩飽和度、對比、時間與數位噪訊，形成掃描螢幕的動態影像語言。
- **Custom Blur Renderer Feature**：自製 Volume Component 暴露 0–1 強度；Renderer Feature 只在 Volume 啟用時加入 Pass，並以 RenderGraph 建立 Camera Color copy、綁定 depth-stencil、繪製全螢幕三角形，保留未通過 Stencil 的原始像素。
- **Render Pass 排程**：主 Renderer 排除特定物件層，再以 Render Objects 將 Items 安排於 Opaque 後、Upper Objects 安排於 Post-processing 前，精確控制物件、濾鏡與前景框體的合成順序。
- **可動畫化 Shader 介面**：物件材質暴露 Selected、Wrong、Contraband 與 Global Alpha 等參數；共用控制器可依 AnimationCurve／Gradient 驅動全域 Shader 或 VFX 屬性，將程式狀態轉換為一致的視覺節奏。

### 影片與顯示系統

- 待機影片使用獨立 VideoPlayer；Intro、Gameplay、Success 與 Failure 共用 A／B 內容播放器，由 active／standby 插槽交替準備下一段內容。
- 影片在背景完成 Prepare、靜音首幀解碼與 RenderTexture 就緒後才切換顯示，避免直接更換 URL 造成黑畫面。
- 以 operation token 排除舊操作的延遲回呼，並對 Prepare、首幀與檔案路徑設定檢查及逾時復原。
- 顯示端以 MaterialPropertyBlock 切換 RenderTexture，不複製共享材質；影片播放、畫面顯示與遊戲狀態保持模組化。

### 互動選取設計

物件並非固定 UI Grid，而是散落於 3D 行李畫面中。選取系統先將物件位置轉換到螢幕座標，再以「輸入方向夾角＋距離」計分，找出視覺上最合理的下一個目標。這套方法能直接適應不同關卡的非規則排版，不需為每組行李手工建立上下左右鄰接表。

### 架構摘要

`RFID Reader → Keyboard-wedge 輸入緩衝 → RFID／Level Database → Flow State Machine → Video / Gameplay / Presentation → Renderer Features & Feedback → Result → Reset to Idle`

---

## 兩套內容的共同技術價值

這兩套互動的重點不只在完成單次播放或一次遊戲，而是在建立可被展場長時間使用的內容架構：外部輸入不直接控制畫面，視覺演出不直接改寫遊戲狀態，影片回呼也不能越過流程邊界。所有輸入先經過狀態仲裁，內容由資料設定，顯示由獨立模組處理，再透過可視化事件連接美術演出。

對技術美術而言，這個專案展現了如何把 Shader、Renderer Feature、Stencil、Volume 與動畫參數做成可被互動流程驅動的視覺系統；對互動工程而言，則展現了硬體輸入、資料架構、非同步影片與長時間運行復原機制的整合能力。

---

## 首頁卡片短版

### Cabin Portraits

以按鈕與閒置計時驅動的多影片艙房肖像。透過狀態機管理手動、自動與機率事件，搭配首幀預熱、RenderTexture 顯示與遮擋動畫，完成穩定且可持續循環的展場播放體驗。

### Luggage Check

結合 RFID、限時違禁品辨識與 Stencil X-ray 畫面的行李安檢互動。以資料驅動關卡、事件式狀態機與 URP Renderer Feature 串接硬體、影片、遊戲及即時視覺回饋。

---

## 履歷一句話版本

- **Cabin Portraits**：開發多序列影片互動架構，整合實體按鈕、閒置觸發、首幀預熱、RenderTexture 顯示與事件式轉場控制。
- **Luggage Check**：開發 RFID 鍵盤楔入式關卡系統與資料驅動遊戲流程，並以 URP Renderer Feature、RenderGraph 與 Stencil Buffer 製作區域化 X-ray／Blur 視覺。

---

## 技術標籤

`Unity` `C#` `URP` `Shader Graph` `Technical Art` `Interactive Installation` `RFID` `USB HID` `Keyboard Wedge` `State Machine` `Event-driven Architecture` `ScriptableObject` `VideoPlayer` `RenderTexture` `MaterialPropertyBlock` `Renderer Feature` `RenderGraph` `Render Objects` `Stencil Buffer` `Custom Volume` `Realtime VFX`

---

## 對外描述注意事項

- 本專案的 RFID 讀卡器目前採 **USB HID／Keyboard Wedge** 輸入；適合寫「RFID 硬體整合、資料解析與狀態防呆」，不建議寫成「自行開發 RFID 通訊協定」或「直接實作 Serial Port 驅動」。
- X-ray 使用的是 **Fullscreen Renderer Feature + Stencil 限域**；Blur 使用的是 **自製 Scriptable Renderer Feature + Volume + RenderGraph**。兩者可統稱為 URP 自訂渲染管線，但在技術面試中應保留這項差異。
- 主文案目前採第一人稱；若部分內容由團隊共同完成，建議依實際分工調整「我設計／我實作」的範圍，並補上團隊規模與製作期間。
