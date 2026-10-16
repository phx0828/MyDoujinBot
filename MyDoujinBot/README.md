# MyDoujin Bot - 開發與 AI 傳承指南 (Agent & Developer Context)

> **這份文件的目的**：為未來接手本專案的 AI (Agent) 或開發者提供系統全貌的入口索引（Master Index），確保在最短時間內精準掌握專案脈絡、檔案職責與技術邊界，**絕不破壞既有架構與防呆機制**。

---

## 1. 專案概述 (Project Overview)

**MyDoujin Bot** 是一個為網頁遊戲（`mydoujin.online`）設計的純 Windows 桌面端自動化輔助工具。
- **技術棧**：C# 12 / .NET 8.0 / Windows Forms (WinForms)。
- **高 DPI 支援**：支援 `PerMonitorV2`，任意 100% ~ 200% 縮放比例均維持精確排版與文字清晰。
- **發布模式**：可編譯為單一免安裝可執行檔（Portable Single-file `.exe`），完全無第三方執行環境依賴。
- **持久化機制**：設定檔與方案檔案統一儲存於 `.exe` 所在之執行目錄，隨檔案移動即可攜帶。

---

## 2. 專案專屬文檔索引 (Documentation Index)

為避免單一文檔過於龐雜，詳細技術規範已拆分為專屬子文件：

| 文件路徑 | 主題 | 核心內容摘要 |
| :--- | :--- | :--- |
| 📘 **[Architecture.md](file:///d:/MyDoujinBot/MyDoujinBot/docs/Architecture.md)** | **系統架構與技術核心** | 四層架構 (SoC)、非同步與執行緒安全、無鎖事件掛起、持久化策略 |
| 📗 **[Features.md](file:///d:/MyDoujinBot/MyDoujinBot/docs/Features.md)** | **系統功能與運作模組** | 自動訓練、自動戰鬥與潑屎判定、階梯排程與方案管理、兩階段事件決策、戰報統計、樓層獎勵 |
| 📙 **[ApiCatalog.md](file:///d:/MyDoujinBot/MyDoujinBot/docs/ApiCatalog.md)** | **API 端點與服務手冊** | 目前運作中 API、標頭封裝規範、401 攔截與安全機制 |
| 📑 **[GameApis_Spec.md](file:///d:/MyDoujinBot/MyDoujinBot/docs/GameApis_Spec.md)** | **遊戲延伸 API 規格** | 4 隻已驗證 GET API（`users/me`、`action/status`、`characters/active`、`characters`）欄位對照 |
| 📊 **[BattleStats_Spec.md](file:///d:/MyDoujinBot/MyDoujinBot/docs/BattleStats_Spec.md)** | **戰報解析與圖表規格** | 戰報事件逐行統計、全局 100% 長條圖繪製規則、PVP 陣營處理 |

---

## 3. 專案目錄結構 (Directory Structure)

```text
MyDoujinBot/
├── Forms/                    # UI 視窗與自訂渲染控制項
│   ├── MainForm.cs           # 主畫面（側邊欄、6 大分頁、即時儀表板、日誌區、內嵌事件 Overlay、狀態列）
│   ├── EventSelectionForm.cs # 獨立彈窗模式下之事件選項決策視窗
│   ├── BattleReportForm.cs   # 單場戰報彩色日誌彈出視窗
│   ├── BattleStatsForm.cs    # 獨立戰報數據查詢與圖表視窗
│   └── SettingsForm.cs       # 舊版獨立設定彈窗
├── Services/                 # 核心業務、背景工作迴圈與通訊層
│   ├── HttpService.cs        # 統一 HttpClient 封裝（Token 去前綴、偽裝標頭、401 中斷）
│   ├── UserService.cs        # 使用者驗證與樓層獎勵服務（呼叫 me, active, status, floor-reward）
│   ├── UserContext.cs        # 全域使用者個人資料與狀態上下文（快取玩家資料、等級、最高樓層與樓層獎勵冷卻）
│   ├── TrainingLoop.cs       # 自動訓練背景非同步迴圈
│   ├── BattleLoop.cs         # 自動戰鬥背景非同步迴圈
│   ├── PipelineLoop.cs       # 階梯排程多階段排程調度迴圈
│   ├── TrainingService.cs    # 訓練 API (POST /api/action/explore) 服務
│   ├── EventService.cs       # 事件選項 API (POST /api/action/resolve) 服務
│   ├── BattleService.cs      # 戰鬥與驗證 API 服務
│   ├── AppSettingsManager.cs # 全域組態管理 (settings.json，含樓層獎勵開關)
│   ├── PresetManager.cs      # 泛型方案管理 (data/presets/{category}/{id}.json)
│   └── BattleStats*.cs       # 戰報數據統計與解析引擎 (Context/Calculator/Index)
├── Models/                   # 資料模型 (DTO) 與列舉
│   ├── UserMeResponse.cs     # 玩家自身基本資料（GET /api/users/me）
│   ├── ActiveCharacterResponse.cs # 當前出戰角色詳情（GET /api/characters/active）
│   ├── CharactersResponse.cs # 帳號全角色清單（GET /api/characters）
│   ├── ActionStatusResponse.cs# 冷卻與即時狀態（GET /api/action/status）
│   ├── FloorRewardClaimResponse.cs# 樓層獎勵領取結果（POST /api/action/floor-reward）
│   ├── TrainingAction.cs     # 訓練動作項目定義
│   ├── BattleActionResponse.cs# 戰鬥結果與參與者 (participants) 模型 (含茶渡潑屎防誤判)
│   ├── PipelineStage.cs      # 排程單一階段資料模型
│   ├── PipelinePreset.cs     # 排程完整方案模型
│   └── BattleReportResponse.cs# 完整戰報結構模型
├── Utilities/                # 輔助工具類
│   ├── StatHelper.cs         # 遊戲屬性中英對照轉換
│   ├── EventUiHelper.cs      # 事件按鈕色彩與字級輔助
│   ├── PromptDialog.cs       # 方案命名與輸入對話框
│   └── BattleStatsChartBuilder.cs # ASCII 戰報長條圖生成器
└── docs/                     # 系統與 API 詳細規格文件庫
```

---

## 4. 給未來 AI Agent 的開發守則 (Golden Rules for AI)

1. **嚴守非同步與執行緒安全**：
   - 計時等待一律使用 `await Task.Delay`，**絕對不可使用 `Thread.Sleep`**。
   - 所有自背景執行緒觸發之 UI 更新，必須透過 `Control.Invoke` 或 `Control.BeginInvoke` 調度。
2. **保持關注點分離 (SoC)**：
   - 網路呼叫統一經過 `Services/*Service.cs` 與 `HttpService.cs`，禁止在 Form 內直接實例化 `HttpClient` 發送請求。
3. **保持綠色可攜原則 (Portable)**：
   - 本機讀寫路徑一律以 `AppDomain.CurrentDomain.BaseDirectory` 為基準，禁止寫入使用者個人資料夾或 Windows 登錄檔。
4. **維護自動存檔與防呆邊界**：
   - 排程卡片修改即時觸發儲存，若卡片處於編輯中或戰鬥 ID 未驗證，必須維持開始按鈕之防呆鎖定。
