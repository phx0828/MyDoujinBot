# MyDoujin Bot - API 端點與服務對照手冊 (API Catalog)

> **基礎伺服器網址**：`https://mydoujin-backend.onrender.com/api`  
> **目標遊戲主機**：`https://mydoujin.online`  
> **通用標頭 (Headers)**：由 [HttpService.cs](file:///d:/Side/MyDoujinBot/MyDoujinBot/Services/HttpService.cs) 統一封裝，所有呼叫自動帶入：
> - `Authorization: Bearer <Token>`
> - `Content-Type: application/json`
> - `User-Agent: Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 ...`
> - `Origin: https://mydoujin.online`
> - `Referer: https://mydoujin.online/`

---

## 1. 目前程式已實作整合之 API (Active APIs)

以下為專案內部已具備服務封裝與商業邏輯之 API 清單：

| 服務類別 (Service) | HTTP 方法 | 端點路徑 (Endpoint) | 傳入參數 (Payload / Param) | 回傳模型 (Model) | 作用說明 |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **TrainingService** | **POST** | `/action/explore` | `{"actionId": "hunt"}` | `TrainingResponse` | 執行指定訓練探險行動 |
| **EventService** | **POST** | `/action/resolve` | `{"optionId": "選項ID"}` | `ApiResponse<EventResult>` | 提交遭遇事件之選擇結果 |
| **BattleService** | **GET** | `/players/{id}` | URL 路徑帶入玩家 ID | `PlayerProfileResponse` | 查詢並驗證對手玩家名稱、角色與等級 |
| **BattleService** | **POST** | `/players/{id}/challenge` | 空物件 Payload | `BattleActionResponse` | 對指定玩家發起友好切磋 |
| **BattleService** | **POST** | `/players/{id}/chado` | 空物件 Payload | `BattleActionResponse` | 對指定玩家發起「我要茶渡你」挑戰 |
| **BattleService** | **GET** | `/battle-reports/{id}` | URL 路徑帶入戰報編號 | `BattleReportResponse` | 取得單場戰鬥完整戰報與逐行 LOG |

---

## 2. 已驗證之延伸 GET API (Verified Extension APIs)

以下為由 `MyDoujinBot/LOG/` 擷取、已驗證正確 URL 與回傳結構之唯讀 API。  
詳細 JSON 欄位與遊戲頁面名詞對照請參閱專屬文件：[GameApis_Spec.md](file:///d:/Side/MyDoujinBot/MyDoujinBot/docs/GameApis_Spec.md)。

| HTTP 方法 | 端點路徑 (Endpoint) | 測試資料檔 | 對應功用 | 預期回傳核心資訊 |
| :--- | :--- | :--- | :--- | :--- |
| **GET** | `/users/me` | `LOG/me.txt` | 帳號基本資料 | 暱稱、個人狀態簽名、當前出戰角色 ID、最高樓層 |
| **GET** | `/action/status` | `LOG/status.txt` | 行動與冷卻狀態 | 剩餘冷卻毫秒數、未決策事件 (`pendingEvent`)、樓層掛機獎勵冷卻 |
| **GET** | `/characters/active` | `LOG/active.txt` | 出戰角色詳情 | 角色名稱、稱號、九圍數值、扔屎與沾屎戰績統計、轉生次數 |
| **GET** | `/characters` | `LOG/characters.txt` | 全角色與轉生庫 | 擁有的全角色清單、事件偏好名單、轉生配點分配狀態 |

---

## 3. 錯誤處理與授權安全機制 (Security & Errors)

1. **Token 脫敏與修整**：
   - 使用者輸入之 Token 若不慎包含 `Bearer ` 前綴，`HttpService` 會自動修剪只保留真實 JWT 字符串，避免重複前綴導致請求無效。
   - Token **嚴格禁止寫入本機日誌或在 UI 上無遮蔽顯示**。
2. **HTTP 401 攔截與中斷**：
   - 當 Token 過期或失效時，`HttpService` 攔截 `HttpStatusCode.Unauthorized` 並標記 `IsUnauthorized = true`。
   - 所有執行迴圈（TrainingLoop、BattleLoop、PipelineLoop）均會立即中斷並在日誌輸出紅字警告，避免無效持續請求。
3. **逾時防護**：
   - 預設逾時為 30 秒，發生逾時或網路中斷時不會導致應用程式崩潰，並會自動記錄錯誤訊息。
