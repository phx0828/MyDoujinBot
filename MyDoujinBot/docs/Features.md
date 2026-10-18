# MyDoujin Bot - 系統功能與運作模組 (Features Specification)

> **目標讀者**：後續維護之開發者或接手之 AI Agent。  
> **核心準則**：本文件依據目前程式碼實際實作之六大功能模組進行系統化記錄。

---

## 1. 自動訓練模組 (Training Loop)

- **核心程式碼**：[TrainingLoop.cs](file:///d:/MyDoujinBot/MyDoujinBot/Services/TrainingLoop.cs)、[TrainingService.cs](file:///d:/MyDoujinBot/MyDoujinBot/Services/TrainingService.cs)
- **API 端點**：`POST /api/action/explore`，酬載為 `{"actionId": "動作ID"}`
- **功能特性**：
  1. **動作項目與序列輪替**：
     - 支援單一訓練項目或多項目自訂輪替清單（例如：狩獵 $\rightarrow$ 圖書館 $\rightarrow$ 健身房）。
     - 介面採用平滑抗鋸齒 `PillBadge` 膠囊標籤呈現序列，點擊任一標籤可直接自清單移除。
  2. **執行限制模式**：
     - **指定時間**：達到設定的分鐘數後自動停止（預設支援至 9999 分鐘）。
     - **指定次數**：達到設定的執行次數後自動停止（預設支援至 99999 次）。
  3. **額外延遲機制**：
     - 在伺服器基礎冷卻時間之上，可設定 `0 ~ N 秒` 之隨機額外延遲（預設 1 秒），模擬真人操作節奏。
  4. **能力成長監控**：
     - 解析每次探險回傳之 `exp`、`level` 與 `gains`，即時在中間狀態欄與 LOG 中呈現屬性成長。

---

## 2. 自動戰鬥模組 (Battle Loop)

- **核心程式碼**：[BattleLoop.cs](file:///d:/MyDoujinBot/MyDoujinBot/Services/BattleLoop.cs)、[BattleService.cs](file:///d:/MyDoujinBot/MyDoujinBot/Services/BattleService.cs)
- **API 端點**：
  - 切磋：`POST /api/players/{id}/challenge`
  - 茶渡：`POST /api/players/{id}/chado`
- **功能特性**：
  1. **對手玩家 ID 驗證**：
     - 輸入對手 ID 後點擊「驗證」，透過 `GET /api/players/{id}` 即時拉取玩家暱稱、角色與等級。
     - 驗證結果自動持久化儲存，重新啟動視窗時不需重新輸入或再次驗證。
  2. **茶渡潑屎成功判定（防誤判）**：
     - 透過 `response.IsPlayerChadoSuccess()` 擴充方法檢驗最後 10 筆戰鬥 LOG。
     - 提取 API 回傳之 `participants.players` 與 `participants.enemies`，精準確認文字訊息為「**我方角色對敵方角色丟了小零食**」，徹底杜絕敵方反殺潑屎造成的計數誤差。
  3. **戰鬥報告即時檢視**：
     - 單場戰鬥結束後，主畫面日誌欄上方會顯示「📊 查看上次戰報」按鈕，點擊彈出 [BattleReportForm.cs](file:///d:/MyDoujinBot/MyDoujinBot/Forms/BattleReportForm.cs)，以深色主題與彩色文字檢視完整戰鬥逐行日誌。

---

## 3. 行動排程模組 (Pipeline Loop & Presets)

- **核心程式碼**：[PipelineLoop.cs](file:///d:/MyDoujinBot/MyDoujinBot/Services/PipelineLoop.cs)、[PipelineStageEditForm.cs](file:///d:/MyDoujinBot/MyDoujinBot/Forms/PipelineStageEditForm.cs)、[PresetManager.cs](file:///d:/MyDoujinBot/MyDoujinBot/Services/PresetManager.cs)、[PipelineStage.cs](file:///d:/MyDoujinBot/MyDoujinBot/Models/PipelineStage.cs)
- **功能特性**：
  1. **多階段循序執行**：
     - 每個階段可獨立設定為【訓練】或【戰鬥（友好切磋 / 我要茶渡你）】。
     - 階段自動切換條件支援：
       - `LevelReach`：角色達到指定等級（例如升至 Lv.80 即切換）。
       - `RunCountReach`：執行達到指定次數。
       - `MinutesReach`：執行達到指定分鐘數。
  2. **獨立對話框設定與自訂循環清單 (PipelineStageEditForm)**：
     - 點擊「➕ 新增階段」或卡片「✏ 編輯」按鈕時，跳出專屬獨立對話框進行階段調整。
     - **自訂訓練清單**：訓練行動支援自訂循環清單，可隨意加入多個訓練動作（如狩獵、自主訓練），執行時依序循環執行；排程卡片同步動態呈現循環順位 Pill Badge。
     - 提供行動模式（純中文切換：自動訓練／自動戰鬥）、對手 ID 驗證回饋，以及達成切換條件數值配置。
     - 卡片統一標示固定順序編號（`#1 階段`、`#2 階段`），編輯更直觀。
  3. **全面即時自動存檔**：
     - 階段內容新增、修改儲存、上下移動排序與刪除均會在記憶體與磁碟檔案同步儲存，無須手動儲存按鈕。
  4. **方案管理器 (Preset Manager)**：
     - 支援多套排程方案切換，檔案以獨立 JSON 儲存於 `data/presets/pipeline/`。
     - 具備「另存新方案」、「重新命名」、「刪除方案」功能。
     - 支援「📤 匯出方案 (JSON)」與「📥 匯入方案 (JSON)」，方便玩家社群彼此分享設定檔。

---

## 4. 遭遇事件應對決策系統 (Event Response)

- **核心程式碼**：[EventService.cs](file:///d:/MyDoujinBot/MyDoujinBot/Services/EventService.cs)、[EventSelectionForm.cs](file:///d:/MyDoujinBot/MyDoujinBot/Forms/EventSelectionForm.cs)、[EventUiHelper.cs](file:///d:/MyDoujinBot/MyDoujinBot/Utilities/EventUiHelper.cs)
- **API 端點**：`POST /api/action/resolve`，酬載為 `{"optionId": "選項ID"}`
- **功能特性**：
  1. **兩階段階層式選取架構**：
     - **第一層**：`[自動選擇]` vs `[手動選擇]`。
     - **第二層（自動）**：
       - 優先選最高成功率（自動評估所有選項之 `SuccessChance` 欄位）。
       - 完全隨機。
     - **第二層（手動）**：
       - 主畫面內嵌選擇（零干擾模式，不搶焦點）。
       - 獨立彈出視窗（強制置頂奪取焦點）。
     - 未選取的類別子選項自動反灰禁用，選取切換時即時存檔。
  2. **零干擾內嵌 Overlay**：
     - 當選擇內嵌模式時，事件直接平滑覆蓋於右側 LOG 面板上方，不建立新視窗，不打斷玩家在其他視窗（如全螢幕遊戲或辦公）的焦點。
     - 自訂繪製按鈕具備成功率色階（綠色 $\ge 70\%$、黃色 $\ge 40\%$、紅色 $< 40\%$）、雙層字級與屬性中文名稱展示。
  3. **系統通知與音效**：
     - 可開啟 Windows 系統托盤氣泡提示（BalloonTip）與提示音效，事件觸發時在背景默默提醒玩家。

---

## 5. 戰報分析與視覺化圖表 (Battle Analytics)

- **核心程式碼**：[BattleStatsForm.cs](file:///d:/MyDoujinBot/MyDoujinBot/Forms/BattleStatsForm.cs)、[BattleStatsCalculator.cs](file:///d:/MyDoujinBot/MyDoujinBot/Services/BattleStatsCalculator.cs)、[BattleStatsChartBuilder.cs](file:///d:/MyDoujinBot/MyDoujinBot/Utilities/BattleStatsChartBuilder.cs)
- **API 端點**：`GET /api/battle-reports/{reportId}`
- **功能特性**：
  1. **雙入口體驗**：可於主視窗「📊 戰報分析」分頁直接輸入編號查詢，亦可點擊獨立戰鬥數據視窗操作。
  2. **全場統一最大值基準 (Global Scale)**：
     - 輸出（紅條）、承傷（藍條）、治療（綠條）統一以全場所有單位中的單一最高數值為 100% 滿格，直觀反映各單位真實輸出落差。
  3. **命中與爆擊統計**：
     - 輸出數據行格式：`出手｜命中｜命中率｜爆擊｜爆擊率`（爆擊率為 爆擊 $\div$ 命中）。
  4. **PVP 與 BOSS 區塊隔離**：
     - 敵方單位自動依陣營隔離為【BOSS 統計】或【對手統計】，支援召喚物傷害歸屬原召喚者計算。
  5. **誤差免責提示**：
     - 固定標註警語：`💡 提示：戰鬥 LOG 種類繁多，部分特殊技能或效果可能存在無法精確辨識歸屬的誤差，統計數據僅供參考。`

---

## 6. 狀態儀表板與即時日誌 (Telemetry & Logging)

- **頂部即時狀態列**：
  - 主狀態燈號（● 執行中 / 等待冷卻 / 已完成 / 發生錯誤 / 已停止）。
  - 獨立冷卻秒數倒數標籤（文字不再跳動跳格）。
  - 運行總時間計時器 (`HH:mm:ss`)。
  - Token 就緒標記與即時等級展示 (`Lv.X`)。
- **底部狀態列與 Tooltip**：
  - 左側：Token 狀態燈號（● Token： 已驗證 / 未設定 / 失效）。
  - 右側：
    - 樓層獎勵狀態（`樓層獎勵：自動領取` / `樓層獎勵：可領取` / `樓層獎勵：冷卻中`），具備 Tooltip 顯示下次可領取時間（`下次領取時間：yyyy/MM/dd HH:mm:ss`）。
    - 角色資料摘要標籤（黃色呈現：`玩家名 ｜ Lv.X 角色名 ｜ 目前樓層：XXF`）。
- **中間指標統計**（冒號皆統一為全形 `：`）：
  - 訓練模式：`訓練次數：X / Y 次`、`成功 / 失敗`。
  - 戰鬥模式：`戰鬥次數：X / Y 次`、勝利（綠色）/ 失敗（紅色），兩字嚴格對齊。
  - 行動排程模式：
    - 當前階段：`#? 階段 執行什麼 直到什麼`（僅在排程執行中顯示，停止時自動收合隱藏）。
    - 總計訓練列：`總計訓練次數：X 次` ｜ `X 成功 / Y 失敗`。
    - 總計戰鬥列：`總計戰鬥次數：X 次` ｜ `X 勝利 / Y 失敗`（若有茶渡則附帶 `💩 N`）。
  - 茶渡成功次數：`丟下小零食並螺旋下指：X 次`。
  - 觸發事件計數：`觸發事件：X 次`、`成功 / 失敗`。
- **彩色日誌面板 (RichTextBox)**：
  - 成功為綠色、失敗/錯誤為紅色、冷卻/狀態為藍色、事件為紫色、樓層獎勵為黃色。
  - 行數保護機制：上限 1000 行，超過自動精簡最舊的 500 行，唯讀鎖定避免 Windows 系統錯誤音效。

---

## 7. 樓層獎勵與使用者資料同步模組 (Floor Reward & User Context)

- **核心程式碼**：[UserService.cs](file:///d:/MyDoujinBot/MyDoujinBot/Services/UserService.cs)、[UserContext.cs](file:///d:/MyDoujinBot/MyDoujinBot/Services/UserContext.cs)、[MainForm.cs](file:///d:/MyDoujinBot/MyDoujinBot/Forms/MainForm.cs)
- **API 端點**：
  - 使用者資料與狀態同步：`GET /api/users/me`、`GET /api/characters/active`、`GET /api/action/status`
  - 領取樓層獎勵：`POST /api/action/floor-reward`
- **功能特性**：
  1. **全域使用者快取 (UserContext)**：
     - 快取玩家名稱、出戰角色等級、最高樓層及樓層獎勵下次可領取時間戳記 (`NextFloorRewardClaimTime`)。
     - 各行動迴圈（訓練、戰鬥、排程）與領取獎勵回傳新等級時，即時寫入快取並通知 UI 更新。
  2. **自動領取樓層獎勵機制**：
     - 於「設定」頁面提供「自動領取樓層獎勵」開關，並持久化儲存於 `settings.json`。
     - 系統每 60 秒比對本地時間與冷卻時間戳記；已達領取時間且開啟自動時自動呼叫領取 API。
     - 切換勾選或 Token 驗證完成時，若已達領取條件即時觸發領取。
     - 領取成功時記錄黃色 LOG：`[樓層獎勵] 已領取 {floor}F 樓層獎勵：獲得經驗值 +{exp}`（若升級則接續：`，升級 +{levelUps}，目前等級 {level}`）。

---

## 8. 自訂應用程式圖示 (Custom Application Icons)

- **核心程式碼**：[IconSelectionForm.cs](file:///d:/MyDoujinBot/MyDoujinBot/Forms/IconSelectionForm.cs)、[MainForm.cs](file:///d:/MyDoujinBot/MyDoujinBot/Forms/MainForm.cs)、[AppSettingsManager.cs](file:///d:/MyDoujinBot/MyDoujinBot/Services/AppSettingsManager.cs)、[MyDoujinBot.csproj](file:///d:/MyDoujinBot/MyDoujinBot/MyDoujinBot.csproj)
- **功能特性**：
  1. **精簡乾淨的設定介面**：
     - 在「設定」頁籤的「應用程式圖示設定」區塊，僅顯示目前圖示縮圖、目前圖示檔名與【更改圖示】按鈕，版面清爽不繁雜。
  2. **獨立圖示選擇對話框 (IconSelectionForm)**：
     - 點擊【更改圖示】後開啟專用對話框，每次開啟皆會自動掃描 `Resources/` 資料夾內所有的 `.ico` 檔案。
     - 以 48x48 高畫質縮圖卡片陳列，顯示檔案名稱與「✓ 已選取」視覺狀態。
     - 支援滑鼠點選後按下【儲存】套用，亦支援雙擊圖示卡片快速確認套用；點擊【取消】則放棄變更。
  3. **即時熱抽換與持久化**：
     - 儲存後立即生效：熱抽換至 Windows 視窗標題列、工作列以及系統匣常駐圖示（NotifyIcon）。
     - 設定持久化儲存於 `settings.json`（`selectedAppIcon`），重啟應用程式自動恢復使用者的圖示偏好。
  4. **建置與發布支援**：
     - 專案檔設定為 `<Content Include="Resources\*.ico">`，編譯與發布時自動複製所有放在 Resources 的圖示檔案。
