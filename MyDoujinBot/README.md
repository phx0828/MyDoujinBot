# MyDoujin Bot - 開發與 AI 傳承文件 (Agent Context)

這份文件旨在記錄 `MyDoujin Bot` 專案的核心架構、歷史需求以及運作邏輯，方便未來接手的 AI (Agent) 或開發者能夠在最短時間內理解專案脈絡，避免重複造輪子或破壞現有架構。

## 專案概述 (Project Overview)
**MyDoujin Bot** 是一個為特定網頁遊戲（mydoujin.online）量身打造的**純 Windows 桌面端自動化輔助工具**。
- **技術棧**：C# / .NET 8.0 / Windows Forms (WinForms)。
- **無依賴發布**：專案支援透過 `dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true` 打包為單一免安裝 `.exe`。
- **核心功能**：自動發送訓練 API 請求、動態倒數冷卻時間、自動或手動處理隨機遭遇事件。

---

## 系統架構與設計原則 (Architecture & Guidelines)
本專案**嚴格禁止**將所有邏輯塞入單一的 `Form.cs`，採用了關注點分離（SoC）設計：

1. **網路層 (`Services/HttpService.cs`)**
   - 所有的網路請求 (HttpClient) 統一由此負責。
   - 負責寫死標頭 (`User-Agent`, `Origin`, `Referer`, `Content-Type`)。
   - 自動處理 Token 的 `Bearer ` 前綴清除。
   - 攔截 HTTP 401 狀態碼並中止操作。

2. **業務邏輯層 (`Services/TrainingService.cs` & `EventService.cs`)**
   - 將 API 端點封裝為非同步的方法 (Async/Await)。
   - 使用可空型別 (`int?`) 處理 API 回傳的不確定欄位（如 `successChance`）。

3. **控制層 (`Services/TrainingLoop.cs`)**
   - 負責管理訓練的無窮迴圈、倒數計時與停止信號 (`CancellationToken`)。
   - 內部所有等待皆使用 `await Task.Delay`，**絕對不使用 `Thread.Sleep`** 以免凍結 UI。

4. **UI 層 (`Forms/MainForm.cs` 等)**
   - 使用 `TableLayoutPanel` 分成「左（設定）、中（狀態）、右（日誌）」三欄，確保縮放時排版不跑位。
   - RadioButton 控制項必須使用獨立 `Panel` 隔離，避免預設的群組干擾。
   - 跨執行緒更新 UI 統一透過 `Control.Invoke` 處理。

---

## 歷史需求回顧 (Historical Requirements)

### 🔴 最初始需求指令 (Initial Prompts)
1. **框架限制**：「不要使用 ASP.NET。不要做成 Web App。這是一個純 Windows Desktop App。」
2. **結構要求**：「我希望這個專案的程式碼結構清楚，不要把所有邏輯全部塞在 Form1.cs。」
3. **介面規劃**：
   - 視窗畫面分三區顯示。
   - 左側：設定區（選擇訓練項目、執行模式、冷卻延遲、事件應對方式）。
   - 中間：即時狀態區（觸發事件次數、成功/失敗次數、能力值變化），並固定於中央不隨拉扯跑版。
   - 右側：Log 輸出區，最多保存 2000 筆。
4. **安全設定**：「Token 的部分不可寫死、不可出現在 LOG，需實作設定按鈕，點擊後最上層出現修改視窗。」
5. **事件處理**：「若觸發事件，需支援自動選擇（最高成功率）或手動選擇（跳出視窗等待玩家點擊）。」

### 🟡 後續調整與除錯記錄 (Subsequent Adjustments)
1. **事件 API 釐清**：
   - 遭遇事件後，需呼叫 `POST /api/action/resolve`，酬載格式為 `{"optionId": "事件ID"}`。
2. **屬性名稱對齊**：
   - 遊戲能力值必須對應中文：`HP`, `攻擊`, `防禦`, `體力`, `敏捷`, `反應速度`, `技巧`, `智力`, `幸運`。
3. **網路表頭增強 (HTTP Headers)**：
   - 為了模擬真實瀏覽器行為，新增了 `content-type`, `origin`, `referer`, `user-agent`。這是催生出 `HttpService` 的關鍵點。
4. **UX/UI 微調**：
   - **文字裁切問題**：加寬了狀態欄值的 X 軸偏移量（設為 +95），避免「事件成功：」等長文字遮擋數值。
   - **RadioButton 衝突**：將「執行設定」與「事件應對」用透明 `Panel` 隔離，解決 Windows Forms 預設全部歸為同一群組的 Bug。
   - **長文字視窗**：事件選擇視窗 (`EventSelectionForm`) 實作了動態高度計算，避免超長說明文字被截斷。
   - **視窗置中修復**：將 `EventSelectionForm` 的 `StartPosition` 設為 `CenterScreen`，避免主程式縮小時彈出視窗位置偏移。
   - **多重字體按鈕實作**：為了在同一按鈕中顯示大小不同的字體 (主要選項名稱與較小的屬性參照/成功率說明)，在 `EventSelectionForm` 放棄了原生的 `btn.Text`，改為攔截 `Paint` 事件並使用 `TextRenderer.DrawText` 進行自定義渲染 (Custom Paint)。

5. **專案圖示 (Icon)**：
   - 將 `.ico` 放入 `Resources/app.ico`。
   - 於 `MainForm.cs` 與 `.csproj` 皆進行綁定，使編譯後的 EXE 擁有原生應用程式縮圖。

---

## 未來待辦與擴充規劃 (Future Roadmap)
- **無干擾事件通知系統 (UX 優化)**：
  - **需求背景**：避免全螢幕遊戲（如 FPS 槍戰）被獨立彈出視窗（`EventSelectionForm`）強制中斷並奪取焦點。
  - **預定實作方向**：
    1. 在設定區擴充「遭遇事件應對」選項：保留【獨立彈出視窗】，新增【內嵌於主畫面】選項。
    2. 若選擇【內嵌】，事件發生時將在 `MainForm` 內部展開覆蓋層（Overlay Panel）來顯示選項，取代新視窗，實現零干擾。
    3. 新增可選的 Windows 原生通知（`NotifyIcon`），當處於內嵌模式且事件觸發時，僅透過右下角系統提示與微弱音效提醒玩家，等待玩家空閒時再切回視窗操作。

---

> **給未來接手 AI 的提示 (Prompt for Future Agent)**
> 當使用者要求修改此專案時，請優先閱讀本文件。修改 UI 請查閱 `Forms/`，修改連線邏輯請查閱 `Services/HttpService.cs`，若要修改迴圈邏輯請至 `Services/TrainingLoop.cs`。保持非同步 (Async) 與關注點分離，絕不可破壞已建立的防呆機制 (Token Trim, 401 中斷)。
