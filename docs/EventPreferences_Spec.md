# 事件偏好（Event Preferences）功能需求

專案：`d:\Side\MyDoujinBot\MyDoujinBot`（WinForms, .NET）
位置：事件設定頁面（`MainForm.cs` 分頁 4「事件應對」，現有 `EventCustomChoicePanel` 所在處）

## 1. 功能目的
「事件偏好」可降低所選角色的相關事件出現機率。使用者勾選要降低機率的角色，按「更新」後才送出伺服器。
可將勾選結果存成本機配置方案，外層選方案即套用到待更新清單（**不會自動送出**，仍需按「更新」）。

## 2. API
### 讀取：`GET https://api.mydoujin.online/api/characters`
- 既有實作：`UserService.GetCharactersAsync`、模型 `Models/CharactersResponse.cs`，快取於 `UserContext.Current.CharactersData`。
- `characters`：可被選擇的角色（`CharacterEntry`：`CharacterId`、`Name`、`Title` …）。
- `deprioritizedCharacterIds`：目前儲存在伺服器的清單。

### 儲存：`PUT https://api.mydoujin.online/api/characters/event-preferences`
```json
{"deprioritizedCharacterIds":["agil","ruijerd","roland", "..."]}
```
- 需帶 Bearer Token，沿用 `HttpService`（參考 `EventService.cs` 的寫法）。專案目前 **沒有** `PutAsync`／`PutRawAsync`，需確認 `HttpService` 是否支援，沒有則新增。
- 成功後同步更新 `UserContext.Current.CharactersData.DeprioritizedCharacterIds`。
- PUT 回應格式未知，實作前需向使用者確認，不可臆測。

## 3. UI 設計
### 外層（事件設定頁，新面板，風格比照 `EventCustomChoicePanel`）
- 方案下拉選單（本機方案）：選擇即套用到「待更新清單」（不自動送出）。
- 摘要文字：已選 N 位角色、與伺服器是否一致。
- 「編輯」按鈕：開啟彈出視窗。
- 「更新」按鈕：PUT 送出目前待更新清單。
- 方案管理（新增／覆寫／複製／刪除／匯入匯出）可沿用 `PresetManager<T>`（新增一個實作 `IPresetProfile`、存 `deprioritizedCharacterIds` 的類別，分類名如 `"eventpref"`），UI 可參考 `EventCustomChoicePanel`。

### 彈出視窗（參考 `EventChoiceEditorForm`）
- 表格每列一個角色（來源：API `characters`），欄位：
  - 勾選框（勾選 = 降低該角色事件機率）
  - 角色名稱
  - 事件總數
  - 各屬性（HP、攻、防、體、敏、反、技、智、幸）的「給點事件數 / 佔比」（可切換顯示）
  - 攻略組分級（智力型分級、攻擊型分級：`top` / `mid` / `low`，來自 `EventRoute.json` 的 `overview`）
- 欄位標題可點擊排序。
- 儲存方案、另存方案、套用方案。

### 一鍵勾選與檢視篩選
1. **檢視篩選（全部 / 已排除 / 未排除）**：
   - 切換時依目前勾選狀態篩選表格顯示的角色列（手動變更勾選狀態時暫不立即隱藏列，待重新切換篩選時刷新）。
   - 所有一鍵勾選一律採**覆蓋模式**，僅覆蓋當下畫面上篩選後顯示的所有角色列，隱藏中的列維持原勾選狀態。
2. **攻略組推薦（智力角 / 攻擊角）**：
   - 依 `EventRoute.json` 的 `overview.int.untick` 與 `overview.atk.untick` 清單，一鍵勾選排除該流派建議取消的角色（覆蓋目前顯示列，使用者可再手動微調）。
3. **複合條件一鍵勾選**：
   - 支援多條條件組合，例如：「會給智力獎勵的事件 < 2 **&&** 會給技巧獎勵的事件 < 2」才勾選排除。
   - 每條條件包含：屬性（HP、攻、防、體、敏、反、技、智、幸）、指標類型（事件數 / 佔比 %）、比較方式（`<` / `<=`）、門檻數值。
   - 多條條件之間支援「全部符合（AND）」或「任一符合（OR）」組合（可動態增減條件列）。
   - 無事件庫對應資料的角色不納入自動勾選（維持未勾選），仍可手動勾選。
   - 確認前可於表格內檢視並手動微調。
- 不使用期望值／權重評分（已討論後決定不做）。

## 4. 計數邏輯（核心）
目的：「這個角色有多少事件會給某屬性」，太少使用者就會選擇降低機率。

- 資料來源：
  - `EventRoute.json`（透過 `EventArchiveService.LoadRouteFile()`）：包含各角色線 `chains` 底下事件的選項 `onSuccess` 與 `onFail`（`Dictionary<string,int>`，鍵：hp 攻 防 體 敏 反 技 智 幸），以及 `overview.int` / `overview.atk` 的攻略組推薦清單（`untick`、`lines[].tier`）。
- 「某事件給屬性 X」：該事件至少有一個選項，成功或失敗時 X 的值 ≠ 0。
  - 成功、失敗都算；顯示時需標註來源（僅成功時給 / 失敗時也有）。
  - 值為負數（扣點）也會計入並於 UI 標記提示。預設事件庫中負值極少（7 筆，6 個選項的失敗有屬性變動），不需特別設計。
- 顯示兩個數字：「給點事件數」與「佔比 = 給點事件數 / 該線事件總數」。使用者自行選擇看哪個。
- 非角色線 `#daily`（每日事件）、`#misc`（其他事件）不顯示在勾選列表。

## 5. 角色對應（尚未驗證，實作前必須先處理）
- API `CharacterId`（例：`zhuang_fangyi`、`gebura`）與事件庫 `ChainId`（例：`zfy`、`geb`）命名不同。
- 初步想法是以中文名稱比對（`CharacterEntry.Name` 對 `chain.Name`），但**未驗證**。事件庫名稱有合併情形，例如「大衛·馬丁內斯／露西娜·櫛名田」「格林／愛麗絲」，且「孤鳥」「孤鳥（其他型態）」分為兩條線；payload 範例有 `lone_bird_greatsword_lbg`、`lone_bird_longsword_hh` 兩個不同 ID。
- 作法：先用使用者登入後的真實 API 資料比對，列出對不上的角色，與使用者確認對照規則（可能需要一份手動對照表 JSON）。對不上的角色在表格顯示「無事件資料」，仍可勾選。

## 6. 建議實作階段
1. **第一階段（可獨立編譯）**：偏好 API 服務（GET 已有；PUT 新增）、方案類別與管理、計數邏輯（含失敗給點與註記）、角色對應。
2. **第二階段**：彈出視窗（表格、排序、檢視篩選、攻略組推薦、複合條件一鍵勾選、方案儲存）、外層面板、接入 `MainForm.cs` 事件頁。

## 7. 注意事項
- 專案無測試，以編譯通過為驗證；UI 需使用者實測。
- 遵守使用者規則：不臆測、不多做未討論的功能、說明客觀。有疑問先問。
- 待確認：PUT 回應格式；角色對應規則。
