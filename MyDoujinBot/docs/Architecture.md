# MyDoujin Bot - 系統架構與技術規範 (Architecture Specification)

> **目標讀者**：後續維護之開發者或接手之 AI Agent。  
> **核心準則**：本文件僅記錄既有已實作之技術架構與設計規則，不包含未經證實之假設。

---

## 1. 專案基本技術棧

- **開發語言**：C# 12 / .NET 8.0
- **應用程式類型**：Windows Forms (WinForms) 純桌面端應用程式
- **高 DPI 支援**：`PerMonitorV2`（於 `.csproj` 中啟用，搭配內部 `ScaleDpi()` 進行所有控制項間距與尺寸縮放）
- **發布模式**：支援單一免安裝可執行檔（Self-contained Single-file `.exe`），完全無第三方運行環境依賴。

---

## 2. 四層架構設計 (Layered Architecture)

專案嚴格遵循關注點分離（Separation of Concerns, SoC），禁止將業務邏輯或網路請求混寫於視窗類別中：

```text
┌─────────────────────────────────────────────────────────────────┐
│                     1. UI 介面層 (Forms)                        │
│   MainForm, EventSelectionForm, BattleReportForm, BattleStatsForm│
└─────────────────────────────────┬───────────────────────────────┘
                                  ▼
┌─────────────────────────────────────────────────────────────────┐
│                   2. 背景控制層 (Services Loops)                 │
│              TrainingLoop, BattleLoop, PipelineLoop             │
└─────────────────────────────────┬───────────────────────────────┘
                                  ▼
┌─────────────────────────────────────────────────────────────────┐
│                   3. 業務與網路層 (Services)                    │
│   HttpService, TrainingService, EventService, BattleService,    │
│   AppSettingsManager, PresetManager, BattleStatsAnalyzer/Calc   │
└─────────────────────────────────┬───────────────────────────────┘
                                  ▼
┌─────────────────────────────────────────────────────────────────┐
│                     4. 資料模型層 (Models)                      │
│   DTOs, PipelineStage, PipelinePreset, LoopSettings, LoopStats  │
└─────────────────────────────────────────────────────────────────┘
```

### 2.1 UI 介面層 (`Forms/`)
- **[MainForm.cs](file:///d:/MyDoujinBot/MyDoujinBot/Forms/MainForm.cs)**：應用程式主視窗。
  - 左側：側邊欄導航（6 大分頁：自動訓練、自動戰鬥、行動排程、事件應對、戰報分析、設定）。
  - 右側：上方面板呈現即時執行狀態、冷卻倒數與指標儀表板；下方為彩色 `RichTextBox` 日誌區與內嵌事件 Overlay。
  - 底端：狀態列呈現 Token 狀態、樓層獎勵狀態（含 Tooltip）與角色等級／最高樓層摘要。
- **[EventSelectionForm.cs](file:///d:/MyDoujinBot/MyDoujinBot/Forms/EventSelectionForm.cs)**：手動獨立彈窗模式下的事件決策視窗，具備屬性檢定色階與自適應高度。
- **[BattleReportForm.cs](file:///d:/MyDoujinBot/MyDoujinBot/Forms/BattleReportForm.cs)**：單場戰鬥結果與彩色戰鬥 LOG 檢視彈窗。
- **[BattleStatsForm.cs](file:///d:/MyDoujinBot/MyDoujinBot/Forms/BattleStatsForm.cs)**：獨立戰報數據查詢與 ASCII 長條圖渲染視窗。
- **[PipelineStageEditForm.cs](file:///d:/MyDoujinBot/MyDoujinBot/Forms/PipelineStageEditForm.cs)**：排程階段獨立新增與編輯對話框，提供充裕設定版面、即時排程卡片預覽與對手驗證機制。
- **[SettingsForm.cs](file:///d:/MyDoujinBot/MyDoujinBot/Forms/SettingsForm.cs)**：舊版相容之獨立設定對話框。
- **流暢渲染與視窗拖曳原生零延遲機制**：
  - 表單樣式加入 `WS_CLIPCHILDREN`（`0x02000000`）與 `OptimizedDoubleBuffer`，並關閉衝突的 `UserPaint`，使視窗平移完全交由 Windows DWM GPU 硬體加速合成。
  - 攔截 `WM_ENTERSIZEMOVE` / `WM_EXITSIZEMOVE`，在拖曳移動或邊框縮放期間主動掛起排版（`SuspendLayout`），並在 `OnResize` 阻擋 `base.OnResize` 觸發的遞迴排版與 RichTextBox 重新排字；放開滑鼠時才一次性完成平滑排版，徹底杜絕拖曳延遲。
  - 攔截 `WM_ERASEBKGND` 抑制重複的背景抹除，消除視窗移動時的 GDI 摩擦與閃爍。
  - **主畫布動態單頁籤掛載（Dynamic Single-View Mounting）**：6 個頁籤之元件物件常駐記憶體保持各項設定與輸入狀態，但中央主畫布 `pnlMainCanvas` 永遠只掛載當前顯示之單一頁籤，將非作用中頁籤從 Win32 子視窗樹卸載（避免累積 500+ HWND），確保在使用者頻繁切換分頁後，拖曳與重繪依然維持與首次啟動時完全一致的原生絲滑流暢度。

### 2.2 背景控制層 (`Services/*Loop.cs`)
- 負責迴圈生命週期、冷卻倒數、停止信號攔截、統計數值累計，並透過委派（Action）回傳給 UI 執行緒。
- **[TrainingLoop.cs](file:///d:/MyDoujinBot/MyDoujinBot/Services/TrainingLoop.cs)**：自動訓練探險之無窮迴圈控制。
- **[BattleLoop.cs](file:///d:/MyDoujinBot/MyDoujinBot/Services/BattleLoop.cs)**：自動切磋/茶渡戰鬥迴圈控制。
- **[PipelineLoop.cs](file:///d:/MyDoujinBot/MyDoujinBot/Services/PipelineLoop.cs)**：行動多階段排程之自動化調度器，負責串接訓練與戰鬥階段。

### 2.3 業務與網路層 (`Services/`)
- **[HttpService.cs](file:///d:/MyDoujinBot/MyDoujinBot/Services/HttpService.cs)**：全域 HTTP 通訊核心。
  - 封裝 `HttpClient`，負責統一寫入偽裝標頭（`User-Agent`, `Origin`, `Referer`, `Content-Type`）。
  - 自動清除 Bearer Token 前綴空格。
  - 統一攔截 HTTP 401（未授權）並通知中斷。
- **[TrainingService.cs](file:///d:/MyDoujinBot/MyDoujinBot/Services/TrainingService.cs)**：封裝 `POST /api/action/explore`。
- **[EventService.cs](file:///d:/MyDoujinBot/MyDoujinBot/Services/EventService.cs)**：封裝 `POST /api/action/resolve`。
- **[BattleService.cs](file:///d:/MyDoujinBot/MyDoujinBot/Services/BattleService.cs)**：封裝玩家驗證、戰鬥發起與戰報取得。
- **[UserService.cs](file:///d:/MyDoujinBot/MyDoujinBot/Services/UserService.cs)**：封裝 Token 驗證與使用者資訊拉取（呼叫 `users/me`, `characters/active`, `action/status`）與樓層獎勵領取（呼叫 `action/floor-reward`）。
- **[UserContext.cs](file:///d:/MyDoujinBot/MyDoujinBot/Services/UserContext.cs)**：全域使用者資料與狀態上下文（提供執行緒安全之玩家資料、等級、最高樓層、樓層獎勵冷卻時間戳記快取與即時更新事件通知）。
- **[AppSettingsManager.cs](file:///d:/MyDoujinBot/MyDoujinBot/Services/AppSettingsManager.cs)**：全域組態管理（讀寫 `settings.json`，含自動領取樓層獎勵等設定）。
- **[PresetManager.cs](file:///d:/MyDoujinBot/MyDoujinBot/Services/PresetManager.cs)**：泛型方案檔案管理器（支援方案列舉、讀寫、複製、刪除、匯入/匯出）。
- **`BattleStats*` 系列**：純數據解析器與統計引擎，不依賴 UI，負責解析戰報 JSON 並計算傷害、承傷與治療圖表。

### 2.4 資料模型層 (`Models/`)
- 包含純資料類別（DTO）、列舉型別與持久化結構，如 `UserMeResponse`、`ActiveCharacterResponse`、`CharactersResponse`、`ActionStatusResponse`、`FloorRewardClaimResponse`、`PipelineStage`、`PipelinePreset`、`BattleActionResponse`、`TrainingAction` 等。

### 2.5 工具與輔助層 (`Utilities/`)
- **[LoopLogHelper.cs](file:///d:/MyDoujinBot/MyDoujinBot/Utilities/LoopLogHelper.cs)**：統一自動訓練、自動戰鬥與行動排程之日誌輸出格式化、色彩定義、隨機冷卻計算與事件自動選擇策略。
- **[StatHelper.cs](file:///d:/MyDoujinBot/MyDoujinBot/Utilities/StatHelper.cs)**：遊戲屬性中英名稱快取與對照。
- **[EventUiHelper.cs](file:///d:/MyDoujinBot/MyDoujinBot/Utilities/EventUiHelper.cs)**：事件決策介面動態外觀與字型排版輔助。
- **[PromptDialog.cs](file:///d:/MyDoujinBot/MyDoujinBot/Utilities/PromptDialog.cs)**：通用文字輸入與方案管理對話框，具備 High-DPI 自適應縮放、輸入提示字元與邊界自動換行保護。
- **[PillBadgeControl.cs](file:///d:/MyDoujinBot/MyDoujinBot/Utilities/PillBadgeControl.cs)**：平滑膠囊風格標籤元件，支援點擊移除與唯讀清單展示，具備 GDI+ 防鋸齒與 High-DPI 縮放支援。
- **[BattleStatsChartBuilder.cs](file:///d:/MyDoujinBot/MyDoujinBot/Utilities/BattleStatsChartBuilder.cs)**：戰報 ASCII 圖表生成器。

---

## 3. 執行緒與非同步並行模型 (Threading Model)

專案嚴格遵守 Windows Forms 執行緒安全原則：

1. **禁止阻塞 UI 執行緒**：
   - 所有的計時等待一律使用 `await Task.Delay(ms, cancellationToken)`。
   - **嚴格禁止使用 `Thread.Sleep`**。
2. **跨執行緒呼叫 UI**：
   - 背景工作透過委派通知 UI 時，一律在接收端檢查 `InvokeRequired` 並透過 `Control.Invoke` 或 `Control.BeginInvoke` 調度回主執行緒。
3. **無鎖非同步事件掛起 (`TaskCompletionSource`)**：
   - 當訓練或戰鬥觸發「手動事件選擇」時，背景執行緒會建立 `TaskCompletionSource<string?>` 並 `await tcs.Task` 掛起。
   - UI 呈現對話框或 Overlay，待玩家點選選項後呼叫 `tcs.SetResult(optionId)`，背景迴圈隨即無縫恢復執行，完全不消耗 CPU 也不凍結視窗。
4. **即時取消信號 (`CancellationTokenSource`)**：
   - 點擊「停止」時呼叫 `_cts?.Cancel()`，所有等待中的 Delay 與 HTTP 請求皆能立即收到取消信號並乾淨退出。

---

## 4. 資料儲存與持久化策略 (Persistence Strategy)

- **基底路徑**：一律以 `AppDomain.CurrentDomain.BaseDirectory` 為基準，落實**綠色可攜（Portable）**原則。發布後的 `.exe` 搬移至任何資料夾皆能在其同層目錄讀寫設定，不污染註冊表或使用者 AppData。
- **儲存實體位置**：
  - `settings.json`：全域設定檔（儲存 Token、模式、冷卻延遲、當前排程階段）。
  - `data/presets/pipeline/{id}.json`：獨立行動排程方案檔案。
- **自動存檔機制**：
  - 任何介面控制項變更、數值微調、排程卡片編輯完成或順序移動，均會觸發即時儲存至磁碟，無須使用者手動點擊「儲存」按鈕。
