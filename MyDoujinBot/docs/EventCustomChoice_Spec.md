# 事件自選功能規格書 (EventCustomChoice_Spec.md)

## 1. 功能概述
在遊戲執行遭遇事件時，依據使用者當前選定方案之設定，自動選擇指定選項。
若當前方案未設定該事件選項，則進入第二優先級，依既有之自動/手動流程處理。

---

## 2. 決策優先級流程

```
遊戲觸發 pendingEvent (包含事件 ID EventId 與選項清單 Options)
  │
  ▼
[優先級 1] 查詢當前方案之決策字典 Dictionary<string, string> (O(1))
  ├─ 依據 pe.EventId 查詢命中設定，且 pendingEvent.Options 包含該指定 targetOptionId
  │    └─ 直接送出該 targetOptionId，略過手動 UI
  │
  └─ 未設定該事件，或指定 targetOptionId 不存在於本次選項清單
       │
       ▼
[優先級 2] 進入既有流程
       ├─ 若為自動模式 (AutoStrategy.HasValue) ──> 依既有策略 (最高成功率 / 第一選項) 自動選擇
       └─ 若為手動模式 ──> 彈出 EventSelectionForm 或顯示 pnlEventOverlay 由使用者點選
```

---

## 3. 資料來源與欄位對應規則

### 3.1 基礎資料檔
- 檔案路徑：`data/events/EventRoute.json`（備援來源：`data/defaults/DefaultEventRoute.json`）。
- 結構依據：`ExternalRouteFile` -> `chains` (角色線) -> `events` (事件，含 `key` 作為 `eventId`) -> `options` (選項，含 `key` 作為 `optionId`)。

### 3.2 推薦標記規則 (依據 options[i].flags.guide)
標記判定僅依據 `options[i].flags.guide.kind`，不標註完美結局與迴避。標籤顯示於選項列最右側，不含括號。對照表如下：

| guide.kind 原始值 | 介面標記文字 | 說明 |
| :--- | :--- | :--- |
| `route` | 推薦 | 攻略推薦路線 |
| `always` | 必選 | 關鍵目標必須選擇 |
| `never` | 別選 | 負面或斷線選項避免選擇 |
| `null` 或未定義 | (不標記) | 一般選項 |

### 3.3 成功獎勵對應規則 (依據 options[i].onSuccess)
欄位鍵名與介面顯示文字之固定對照如下：

| 原始鍵名 | 介面顯示名稱 |
| :--- | :--- |
| `hp` | HP |
| `攻` | 攻擊 |
| `防` | 防禦 |
| `體` | 體力 |
| `敏` | 敏捷 |
| `反` | 反應速度 |
| `技` | 技巧 |
| `智` | 智力 |
| `幸` | 幸運 |

- 顯示格式範例：`體力+2 敏捷+1`（緊接在選項文字之後顯示）。
- 若 `onSuccess` 為空字典，則不顯示屬性字串。

### 3.4 遭遇戰標記規則 (依據 options[i].encounter)
- 當原始 JSON 的 `encounter` 欄位值不為 `false`（即為 `true` 或 `"success"`）時：
  - 介面標記文字：`有遭遇戰`（於最右側以紅色文字顯示，不含括號）。
- 當為 `false` 時：不標記。

---

## 4. 字典建立時機與時間複雜度

### 4.1 事件靜態索引字典 (EventRegistry)
- 型別：`Dictionary<string, EventRegistryItem>`
  - Key：事件 ID (`eventId`，對應 JSON 之 `event.key` 欄位)
  - Value：事件標題、所屬角色、事件順序、選項清單（含選項 `optionId`、推薦標記、成功獎勵、遭遇戰）
- 建立時機：
  1. 應用程式啟動完成，初始化事件庫服務時 (`EventArchiveService.EnsureInitialized`)。
  2. 使用者點擊「更新事件庫」完成並重新載入檔案後。
- 效能：資料量為 424 個事件，在記憶體建立索引耗時約 2ms。

### 4.2 當前方案決策字典 (ActivePresetChoices)
**原則：僅載入並維護「當前作用中方案 (Active Preset)」的單一字典，不預先將所有方案全部載入記憶體。**
- 型別：`Dictionary<string, string>`
  - Key：事件 ID (`eventId`，對應 JSON 之 `event.key` 與遊戲 `pendingEvent.EventId`)
  - Value：使用者選定之選項 ID (`optionId`，對應 JSON 之 `option.key` 與遊戲選項 `opt.Id`)
- 建立與更新時機：
  1. 應用程式啟動時：僅載入各方案之中繼資料 (Id 與 Name) 填入下拉選單；僅針對「當前選定方案」反序列化載入其決策字典。
  2. 使用者切換方案下拉選單 (`cmbEventPresets`) 時：動態載入新選取方案的決策字典，替換記憶體指標。
  3. 使用者在清單中點擊選項行設定或取消時：直接更新當前方案之字典項目，並寫入該方案檔案。
  4. 匯入方案檔案完成並選定該方案時：載入該方案之決策字典。
- 遊戲輪詢查詢：
  - 於 `TrainingLoop` 與 `BattleLoop` 處理 `pendingEvent` 時，呼叫 `_activePresetChoices.TryGetValue(pe.EventId, out string? targetOptionId)`，時間複雜度為 O(1)。
  - 若命中且 `pendingEvent.Options` 存在該 `targetOptionId`，直接送交此 ID，全流程不進行文字比對。

---

## 5. 多方案管理與匯入匯出

### 5.1 架構重用
重用現有 `PresetManager<T>` 與 `IPresetProfile` 架構，分類名稱為 `"event"`。

### 5.2 方案資料模型 (EventChoicePreset)
- 實作介面：`IPresetProfile`
- 欄位定義：
  - `Id` (string)：方案唯一識別碼 (GUID)。
  - `Name` (string)：方案顯示名稱 (如："預設方案"、"敏捷優先")。
  - `CreatedAt` (DateTime)：建立時間。
  - `UpdatedAt` (DateTime)：更新時間。
  - `Choices` (`Dictionary<string, string>`)：事件 ID (`eventId`) 對應選定選項 ID (`optionId`)。

### 5.3 儲存路徑
- 本機路徑：`data/presets/event/{presetId}.json`
- 匯出檔案格式：`PresetPackage<EventChoicePreset>`（JSON 封裝包，含 `schemaVersion`、`category: "event"`、`exportedAt` 與 `preset` 本體）。

### 5.4 操作功能
- 方案切換：下拉選單選取。
- 編輯方案：彈出專用編輯視窗，支援第一行關鍵字搜尋、第二行屬性篩選與遭遇戰篩選（全部／有遭遇戰／無遭遇戰）、指定狀態篩選、角色分組、清空所有選擇與未存檔關閉確認。
- 開新方案：建立全新空白方案並開啟編輯視窗，確認儲存後寫入。
- 複製方案：直接複製當前選定方案並標註「(複製)」，自動設為當前方案。
- 刪除方案：刪除當前方案（保留至少一個預設方案）。
- 匯出方案：將當前方案輸出為 `.json` 檔案。
- 匯入方案：讀取外部 `.json` 檔案，驗證 `category == "event"` 後加入方案清單。

---

## 6. 介面架構與控制項設計 (WinForms)

### 6.1 控制項清單與排版架構
獨立編輯視窗 (`EventChoiceEditorForm`) 控制項配置如下：

1. `pnlMainTable` (TableLayoutPanel)：頂部篩選卡片、中央雙欄列表、底部儲存列。
2. `pnlTopCard` (TableLayoutPanel)：
   - **第一行 (Row 0 / pnlLine1)**：
     - `lblPresetName` / `_txtName` (TextBox)：方案名稱輸入框。
     - `lblSearch` / `_txtSearch` (TextBox)：關鍵字搜尋輸入框（300ms 防抖即時過濾標題與選項）。
   - **第二行 (Row 1 / pnlLine2)**：
     - `pnlFilters` (FlowLayoutPanel)：
       - `lblStat` / `_cmbStatFilter` (ComboBox)：屬性篩選下拉選單 (全部、HP、攻擊、防禦、體力、敏捷、反應速度、技巧、智力、幸運)。
       - `lblEncounter` / `_cmbEncounterFilter` (ComboBox)：遭遇戰篩選下拉選單 (全部、有遭遇戰、無遭遇戰)。
       - `lblStatus` / `_cmbStatusFilter` (ComboBox)：指定狀態下拉選單 (全部、已指定、未指定)。
     - `_btnClearAll` (IconButton)：重置所有選項按鈕（靠右對齊）。
3. `lstCharacters` (ListBox)：左側角色清單。第一項固定為「全部」，後續為各角色名稱。
4. `lstEvents` (DoubleBufferedListBox)：右側事件清單，設定 `DrawMode = DrawMode.OwnerDrawVariable`。
5. `_searchDebounceTimer` (Timer)：300ms 搜尋防抖計時器。
6. `_btnSave`、`_btnCancel` (IconButton)：底部儲存與取消按鈕。

### 6.2 右側 ListBox 自繪項目定義
清單資料來源攤平成一維項目列表 `List<EventListItem>`，包含兩種類型：

1. **標題行 (EventHeaderItem)**
   - 繪製內容：角色名稱、事件順序序號、事件標題、目前設定狀態 (未設定 / 已指定)。
   - 行高：固定 32px (經 DPI 縮放)。
   - 互動：點擊無動作。

2. **選項行 (EventOptionItem)**
   - 繪製內容：單選標記 (已選/未選)、選項名稱、成功屬性變化、標籤 (推薦 / 必選 / 別選，不含括號)、遭遇戰標籤 (`有遭遇戰`)。
   - 行高：固定 28px (經 DPI 縮放)。
   - 互動：滑鼠點擊時設定為該事件之選定項目；若再次點擊已選中項目則取消設定。

---

## 7. 驗證與錯誤處理規則

1. **選項 ID 異動防呆**：
   - 若事件庫更新後，原設定之選項 ID (`optionId`) 不存在於該事件之選項清單中，系統判定為未設定，並於該標題行標註狀態為已失效，不阻斷程式執行。
2. **無指定事件之退回機制**：
   - 嚴格依照優先級執行。若當前方案字典未命中，直接交由後續既有流程接手，不改變既有之自動/手動行為。
