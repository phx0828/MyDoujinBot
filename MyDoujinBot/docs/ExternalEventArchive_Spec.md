# 第三方事件檔案庫規格文件 (External Event Archive Spec)

> **定位與原則**：
> 1. **非遊戲內 API**：本規格所定義之兩份資料檔（[DefaultEventBrief.json](file:///d:/MyDoujinBot/MyDoujinBot/docs/DefaultEventBrief.json) 與 [DefaultEventRoute.json](file:///d:/MyDoujinBot/MyDoujinBot/docs/DefaultEventRoute.json)）皆取自第三方攻略網站（我的同人攻略圖鑑網／社群作者紀錄整理），**嚴禁與既有遊戲即時 API**（如 [PendingEvent.cs](file:///d:/MyDoujinBot/MyDoujinBot/Models/PendingEvent.cs)、[EventOption.cs](file:///d:/MyDoujinBot/MyDoujinBot/Models/EventOption.cs)、[GameApis_Spec.md](file:///d:/MyDoujinBot/MyDoujinBot/docs/GameApis_Spec.md)）**混為一談**。
> 2. **用途**：作為本地「事件檔案庫」之基礎規格。後續系統將建立遠端擷取與本地資料庫匯入更新功能，供事件自動應對、數值決策與路線前置條件分析使用。
> 3. **無推測原則**：所有型別、結構與欄位均 100% 依據實際檔案內容萃取，無任何未經證實之假設欄位。

---

## 1. 兩份規格檔案總覽與關聯性

| 項目 | 簡要事件檔 ([DefaultEventBrief.json](file:///d:/MyDoujinBot/MyDoujinBot/docs/DefaultEventBrief.json)) | 詳細事件路線檔 ([DefaultEventRoute.json](file:///d:/MyDoujinBot/MyDoujinBot/docs/DefaultEventRoute.json)) |
| :--- | :--- | :--- |
| **檔案大小** | ~351 KB (12,469 行) | ~1.26 MB (30,123 行) |
| **主要用途** | 事件基本目錄、選項成果對照（純淨的選項結果與屬性增減） | 深度攻略路線、DC 檢定門檻、選取次數統計、期望值推薦、前置條件圖譜 |
| **收錄規模** | 512 個事件、1,446 個選項 | 66 條角色線、516 個事件、1,528 個選項、102 條前置條件、2 套總覽評級 |
| **關聯對齊方式** | 根層鍵值為「**事件名稱**」，子層鍵值為「**選項文字**」 | 透過 `title`（事件名稱）與 `label`（選項文字）與簡要檔完全互通對齊；同時額外具備唯一代碼 `key` |
| **對應 C# 命名空間** | [MyDoujinBot.Models.ExternalEvents](file:///d:/MyDoujinBot/MyDoujinBot/Models/ExternalEvents/ExternalEventBrief.cs) | [MyDoujinBot.Models.ExternalEvents](file:///d:/MyDoujinBot/MyDoujinBot/Models/ExternalEvents/ExternalEventRoute.cs) |

---

## 2. 簡要事件檔規格 (`DefaultEventBrief.json`)

- **資料實體檔案**：[DefaultEventBrief.json](file:///d:/MyDoujinBot/MyDoujinBot/docs/DefaultEventBrief.json)
- **對應 C# Model**：[ExternalEventBrief.cs](file:///d:/MyDoujinBot/MyDoujinBot/Models/ExternalEvents/ExternalEventBrief.cs) 中的 [ExternalEventBriefFile](file:///d:/MyDoujinBot/MyDoujinBot/Models/ExternalEvents/ExternalEventBrief.cs#L9-L12)

### 2.1 結構模型層級
```text
Root: Dictionary<string, Dictionary<string, ExternalEventBriefOption>>
 └── [事件名稱 (Event Title)]
      └── [選項文字 (Option Label)]
           ├── success: ExternalEventBriefOutcome
           └── failed:  ExternalEventBriefOutcome? (可選)
```

### 2.2 欄位詳細定義

#### `ExternalEventBriefOption` (選項結果對象)
| 欄位名稱 | 型別 | 必填 | 說明 |
| :--- | :--- | :---: | :--- |
| `success` | [ExternalEventBriefOutcome](file:///d:/MyDoujinBot/MyDoujinBot/Models/ExternalEvents/ExternalEventBrief.cs#L32-L60) | 是 | 檢定成功（或無檢定時選擇）所產生的能力變化與結果。 |
| `failed` | [ExternalEventBriefOutcome](file:///d:/MyDoujinBot/MyDoujinBot/Models/ExternalEvents/ExternalEventBrief.cs#L32-L60) | 否 | 檢定失敗時所產生的能力變化與結果。若該選項無判定或不會失敗，則此欄位不存在。 |

#### `ExternalEventBriefOutcome` (分支結果細節)
| 欄位名稱 | 型別 | 必填 | 說明與規範 |
| :--- | :--- | :---: | :--- |
| `stats` | `Dictionary<string, int>` | 是 | 該結果賦予的能力值變更。鍵值簡寫包含：`hp`、`攻`、`防`、`體`、`敏`、`反`、`技`、`智`、`幸`。無能力變化時為空物件 `{}`。 |
| `encounter` | `bool` | 是 | 是否進入遭遇戰。布林值（`true` 或 `false`）。 |
| `unlock` | `string?` (`bool` \| `string`) | 是 | 是否解鎖新角色。<br>• 原文無解鎖時為 `false`。<br>• 解鎖角色時為角色名稱字串（如 `"二階堂希羅"`）。<br>• C# 透過 [ExternalEventUnlockConverter](file:///d:/MyDoujinBot/MyDoujinBot/Models/ExternalEvents/ExternalEventConverters.cs#L14-L38) 統一轉為 `string?`（`false` 轉為 `null`）。 |
| `requires` | `string?` | 否 | 選項出現之前置條件文字描述（全檔有 137 處，例：`"要先在「尋人啟事」選「詢問頭髮五顏六色的少女」才會出現"`）。若無特殊限制則不出現。 |

### 2.3 JSON 範例片段
```json
{
  "地下室": {
    "調查保險箱": {
      "success": {
        "stats": { "智": 1 },
        "encounter": false,
        "unlock": false
      },
      "failed": {
        "stats": {},
        "encounter": false,
        "unlock": false
      }
    },
    "回到樓上": {
      "success": {
        "stats": {},
        "encounter": false,
        "unlock": false
      }
    }
  },
  "正確與不正確": {
    "說明來意：雪莉想把大家找回來": {
      "success": {
        "stats": { "智": 1 },
        "encounter": false,
        "unlock": "二階堂希羅"
      }
    }
  }
}
```

---

## 3. 詳細事件路線檔規格 (`DefaultEventRoute.json`)

- **資料實體檔案**：[DefaultEventRoute.json](file:///d:/MyDoujinBot/MyDoujinBot/docs/DefaultEventRoute.json)
- **對應 C# Model**：[ExternalEventRoute.cs](file:///d:/MyDoujinBot/MyDoujinBot/Models/ExternalEvents/ExternalEventRoute.cs) 中的 [ExternalRouteFile](file:///d:/MyDoujinBot/MyDoujinBot/Models/ExternalEvents/ExternalEventRoute.cs#L9-L32)

### 3.1 頂層結構 (Top-Level Structure)
```text
ExternalRouteFile
 ├── about:         ExternalRouteAbout
 ├── fields:        Dictionary<string, string>
 ├── weights:       Dictionary<string, Dictionary<string, double>>
 ├── chains:        Dictionary<string, ExternalRouteChain>
 ├── overview:      Dictionary<string, ExternalRouteOverviewProfile>
 ├── encounter:     ExternalRouteEncounterAdvice
 └── prerequisites: List<ExternalRoutePrerequisite>
```

| 區塊名稱 | 型別 | 說明 |
| :--- | :--- | :--- |
| `about` | [ExternalRouteAbout](file:///d:/MyDoujinBot/MyDoujinBot/Models/ExternalEvents/ExternalEventRoute.cs#L37-L55) | 來源中繼資訊（網站、作者、資料日期、格式識別與免責註記） |
| `fields` | `Dictionary<string, string>` | 原檔內建之欄位說明字典，記載各路徑鍵名涵義 |
| `weights` | `Dictionary<string, Dictionary<string, double>>` | 各流派（`atk` 攻擊型、`int` 智力型）評估選項價值之能力加權倍率 |
| `chains` | `Dictionary<string, ExternalRouteChain>` | 核心資料：以角色線代號（如 `hiro`、`quan`）為鍵的 56 條角色線完整事件鏈 |
| `overview` | `Dictionary<string, ExternalRouteOverviewProfile>` | 總體 1～100 級路線期望值排行榜、建議勾選（`recommend`）與取消勾選（`untick`）角色清單 |
| `encounter` | [ExternalRouteEncounterAdvice](file:///d:/MyDoujinBot/MyDoujinBot/Models/ExternalEvents/ExternalEventRoute.cs#L318-L330) | 遭遇戰應對方針（攻擊型與智力型等級避開策略、經驗門檻數值） |
| `prerequisites` | `List<ExternalRoutePrerequisite>` | 全事件與選項之前置條件跨線依賴總表（共 77 條圖譜關聯） |

---

### 3.2 中繼與加權區塊 (`about`, `weights`, `encounter`)

#### `about` 結構
| 欄位名稱 | 型別 | 範例值 | 說明 |
| :--- | :--- | :--- | :--- |
| `site` | `string` | `"我的同人攻略圖鑑網"` | 來源站點名稱 |
| `author` | `string` | `"赤羽和一（Discord）"` | 攻略作者 |
| `dataDate` | `string` | `"2026-10-18"` | 資料快照日期 |
| `format` | `string` | `"mydoujin-route"` | 規格格式代碼 |
| `version` | `int` | `2` | 規格版本號 |
| `note` | `string` | `"數字是作者的紀錄與戰報分析推估，僅供參考..."` | 備註與跨檔對齊指引 |

#### `weights` 結構
以流派代號為鍵（`atk`、`int`），子層包含屬性代號對應的加權權重（`double`）：
- 屬性鍵名：`atk`、`luk`、`tec`、`int`、`sta`、`agi`、`spd`、`hp`、`def`。
- 計算原則：計算加權期望值時，`hp` 會先除以 10 後再乘以加權係數。

#### `encounter` 結構
| 欄位名稱 | 型別 | 說明 |
| :--- | :--- | :--- |
| `atk` | `string` | 攻擊型角色之遭遇戰策略（例：`"Lv10 以下避開，之後照常打"`） |
| `int` | `string` | 智力型角色之遭遇戰策略（例：`"全等級避開（完美結局、解鎖角色需要的遭遇戰例外）"`） |
| `minExp` | `int` | 遭遇戰建議設定的經驗值下限（例：`100`） |
| `note` | `string` | 作者戰報分析之等級經驗值比例公式說明 |

---

### 3.3 角色線結構 (`chains.<代號>`)

- **對應 C# Model**：[ExternalRouteChain](file:///d:/MyDoujinBot/MyDoujinBot/Models/ExternalEvents/ExternalEventRoute.cs#L60-L75)

| 欄位名稱 | 型別 | 說明 |
| :--- | :--- | :--- |
| `name` | `string` | 角色名稱（例：`"二階堂希羅"`） |
| `sub` | `string` | 角色副標題（例：`"正義的執行者"`） |
| `forced` | `bool` | 該角色線是否每輪強制出現（例：`true`） |
| `perfect` | `List<ExternalRoutePerfectCondition>` | 達成該角色完美結局之選項清單 |
| `events` | `List<ExternalRouteEvent>` | 該角色線所包含之事件清單（依每輪出現順序排序） |

#### `ExternalRoutePerfectCondition` (完美結局條件項)
| 欄位名稱 | 型別 | 說明 |
| :--- | :--- | :--- |
| `eventKey` | `string` | 事件代號（例：`"hachi_x8"`） |
| `event` | `string` | 事件標題（例：`"又是三份"`） |
| `key` | `string` | 選項代號（例：`"x_34"`） |
| `option` | `string` | 選項文字內容（例：`"把一直留下的第三份交給他"`） |
| `confirmed` | `bool` | 是否已由其他玩家或戰報 100% 驗證（例：`false`） |

---

### 3.4 事件節點結構 (`events[]`)

- **對應 C# Model**：[ExternalRouteEvent](file:///d:/MyDoujinBot/MyDoujinBot/Models/ExternalEvents/ExternalEventRoute.cs#L100-L127)

| 欄位名稱 | 型別 | 說明 |
| :--- | :--- | :--- |
| `order` | `int` | 本輪順序編號（1-based 序數，例：`1`） |
| `key` | `string` | 事件唯一代號（例：`"hiro_e001_the_list"`） |
| `title` | `string` | 事件名稱（對齊簡要檔之事件鍵名，例：`"筆記上的名單"`） |
| `requires` | `List<ExternalRouteEventRequirement>` | 觸發此事件所需之前置條件清單 |
| `options` | `List<ExternalRouteOption>` | 事件包含的選項清單（平均 2～4 個選項） |
| `atk` | `ExternalRouteEventRecommendation?` | 攻擊型流派之最佳期望值推薦選項 |
| `int` | `ExternalRouteEventRecommendation?` | 智力型流派之最佳期望值推薦選項 |
| `route` | `ExternalRouteGuidance?` | 劇情路線推薦選項（完美結局 > 解鎖角色 > 打開後續 > 玩家攻略；無則為 `null`） |
| `avoid` | `List<ExternalRouteGuidance>` | 必須避開的選項（如會斷線或攻略嚴禁選擇之選項） |

#### `ExternalRouteEventRequirement` (前置需求)
| 欄位名稱 | 型別 | 說明 |
| :--- | :--- | :--- |
| `eventKey` | `string` | 前置事件代號（例：`"hiro_e004_missing"`） |
| `event` | `string` | 前置事件名稱（例：`"尋人啟事"`） |
| `source` | `string` | 資料來源標記（例：`"作者事件圖"`、`"紀錄推算"`） |
| `option` | `string?` | 必須選擇的選項文字（若為事件前置或排除選項則為 `null`） |
| `notOption` | `string?` | 不能選擇的選項文字（選了此選項反而不會出現） |

#### `ExternalRouteEventRecommendation` 與 `ExternalRouteGuidance`
- **屬性推薦 (`atk` / `int`)**：包含 `key` (選項代號), `option` (選項文字), `value` (加權期望值分數, `double`)。
- **路線指引 (`route` / `avoid`)**：包含 `key` (選項代號), `option` (選項文字), `why` (推薦或迴避理由說明文字)。

---

### 3.5 選項詳細數值與標記 (`options[]`)

- **對應 C# Model**：[ExternalRouteOption](file:///d:/MyDoujinBot/MyDoujinBot/Models/ExternalEvents/ExternalEventRoute.cs#L150-L188)

| 欄位名稱 | 型別 | 說明與邊界條件 |
| :--- | :--- | :--- |
| `key` | `string` | 選項唯一代號（例：`"hiro_opt_e001_red"`） |
| `label` | `string` | 選項文字內容（對齊簡要檔之選項鍵名，例：`"指著穿紅衣、神情端正的少女"`） |
| `check` | `string?` | 判定能力項目（例：`"智"`、`"敏"`；無判定時為 `null`） |
| `dc` | `int?` | 檢定門檻數值（例：`15`、`20`；無判定時為 `null`） |
| `tries` | `int` | 實測紀錄選取次數（例：`27`） |
| `success` | `int` | 實測紀錄成功次數（例：`27`） |
| `rate` | `double` | 成功率（0.0 ~ 1.0；tries < 3 時作者依 DC 推算） |
| `onSuccess` | `Dictionary<string, int>` | 成功時屬性增減平均值（hp、攻、防、體、敏、反、技、智、幸） |
| `onFail` | `Dictionary<string, int>?` | 失敗時屬性增減平均值（無檢定時為 `null`） |
| `encounter` | [ExternalRouteEncounter](file:///d:/MyDoujinBot/MyDoujinBot/Models/ExternalEvents/ExternalEventRoute.cs#L193-L228) | **重要型別**：<br>• `false`: 不遭遇<br>• `true`: 無條件進入遭遇戰<br>• `"success"`: 僅在檢定成功時進入遭遇戰<br>透過 [ExternalRouteEncounterConverter](file:///d:/MyDoujinBot/MyDoujinBot/Models/ExternalEvents/ExternalEventConverters.cs#L45-L88) 解析。 |
| `unlock` | `string?` | 解鎖之角色名（例：`"二階堂希羅"`；無解鎖為 `null`） |
| `flags` | [ExternalRouteOptionFlags?](file:///d:/MyDoujinBot/MyDoujinBot/Models/ExternalEvents/ExternalEventRoute.cs#L233-L250) | 選項特殊標籤物件（無特殊屬性時為 `null` 或不存在） |

#### `ExternalRouteOptionFlags` 標籤細項
| 標籤欄位 | 型別 | 說明 |
| :--- | :--- | :--- |
| `perfect` | `bool?` | 是否為完美結局之關鍵選項（例：`true`） |
| `opens` | `List<string>?` | 選了此選項後，本輪才會出現的後續事件名稱清單（例：`["火種"]`） |
| `gate` | `List<ExternalRouteGateItem>?` | 選了此選項後才會開啟的特定後續選項（含 `event`, `option`, `source`） |
| `breaks` | `string?` | 選了此選項會導致斷線的警告說明（例：`"這輪之後的梅琳娜事件..."`） |
| `once` | `bool?` | 該選項每輪是否僅能選擇一次（例：`true`） |
| `guide` | `ExternalRouteGuide?` | 玩家攻略評語（含 `kind`: `"route"` \| `"always"` \| `"never"`, `why`: 說明） |

---

### 3.6 總覽與排行榜 (`overview.<atk|int>`)

- **對應 C# Model**：[ExternalRouteOverviewProfile](file:///d:/MyDoujinBot/MyDoujinBot/Models/ExternalEvents/ExternalEventRoute.cs#L290-L313)

| 欄位名稱 | 型別 | 說明 |
| :--- | :--- | :--- |
| `label` | `string` | 流派標籤（例：`"攻擊型"`、`"智力型"`） |
| `order` | `string` | 能力權重排序字串（例：`"智力＞幸運＞技巧＞體力＝敏捷＝反應速度＞HP＞防禦＞攻擊"`） |
| `median` | `double` | 全部線路的事件期望值中位數（分／事件，例：`1.87`、`2.9`） |
| `lines` | `List<ExternalRouteOverviewLine>` | 53 條角色線評比排行資料清單 |
| `recommend` | `List<string>` | `top` 階級之推薦勾選角色名清單（例：`["蒜頭", "石頭", "芋頭", ...]`） |
| `untick` | `List<string>` | `low` 階級之建議取消勾選角色名清單（例：`["木之本櫻", "薇蝮", ...]`） |

#### `ExternalRouteOverviewLine` 結構
- `rank`: 排名名次 (`int`)
- `key`: 角色線代碼 (`string`, 例: `"garlic"`)
- `name`: 角色名稱 (`string`, 例: `"蒜頭"`)
- `sub`: 副標題 (`string`, 例: `"會殺人的蒜頭"`)
- `value`: 每事件期望分數 (`double`, 例: `9.28`)
- `events`: 包含事件總數 (`int`, 例: `3`)
- `seen`: 樣本觀察次數 (`int`, 例: `284`)
- `tier`: 建議分級 (`string`, `"top"` \| `"mid"` \| `"low"`)
- `forced`: 是否包含強制遭遇戰 (`bool`)

---

### 3.7 前置條件總表 (`prerequisites[]`)

- **對應 C# Model**：[ExternalRoutePrerequisite](file:///d:/MyDoujinBot/MyDoujinBot/Models/ExternalEvents/ExternalEventRoute.cs#L335-L364)
- **總計數量**：77 筆關聯規則。

| 欄位名稱 | 型別 | 說明 |
| :--- | :--- | :--- |
| `kind` | `string` | 條件種類：<br>• `"option"`：需先在某事件選擇某選項。<br>• `"event"`：需先遇過某事件（此時 `option` 為 `null`）。<br>• `"notOption"`：選了該選項反而不出現。<br>• `"perfect"`：完美結局前置條件。 |
| `chain` | `string` | 角色線代號（例：`"hiro"`、`"gb"`） |
| `name` | `string` | 角色名稱（例：`"二階堂希羅"`） |
| `event` | `string` | 受影響的事件標題 |
| `option` | `string?` | 受影響的選項文字（若整場事件受影響則為 `null`） |
| `needEvent` | `string?` | 必須先觸發的事件標題 |
| `needOption` | `string?` | 必須先選取的選項文字 |
| `alsoNeed` | `List<ExternalRoutePrerequisiteRequirement>?` | 需同時滿足的其他事件與選項要求（陣列項含 `event` 與 `option`） |
| `source` | `string` | 資料來源（作者圖表、實測樣本統計比例說明） |

---

## 4. 程式碼整合架構與規範

### 4.1 命名空間與目錄配置
所有第三方規格專用 Model 均收錄於獨立命名空間：
```text
MyDoujinBot/
 └── Models/
      └── ExternalEvents/
           ├── ExternalEventConverters.cs    // 特殊型別轉換器 (unlock, encounter)
           ├── ExternalEventBrief.cs         // 簡要事件檔資料結構
           └── ExternalEventRoute.cs         // 詳細路線檔資料結構
```

### 4.2 反序列化驗證測試
本規格定義之 C# 模型已於 [Program.cs](file:///C:/Users/phx/.gemini/antigravity-ide/brain/8ae1ef8a-d5e8-4e64-b666-a96d230769de/scratch/test_runner/Program.cs) 進行全量 100% 資料反序列化驗證：
- **`DefaultEventBrief.json`**：512 個事件全部通過無損解析。
- **`DefaultEventRoute.json`**：66 條角色線、516 個事件、1,528 個選項、102 條前置條件、2 個總覽設定檔全部通過無損解析。
- **專案建置**：`dotnet build /t:Compile` 達成 0 錯誤、0 警告。

### 4.3 本機儲存與 Jint 線上動態更新機制
- **本機資料庫路徑**：
  - `data/events/EventBrief.json`：運作中最新簡要事件檔。
  - `data/events/EventRoute.json`：運作中最新詳細路線檔。
  - `data/events/metadata.json`：紀錄版本、來源站點、作者、更新時間戳記與收錄數量。
- **出廠備份目錄**：
  - `data/defaults/DefaultEventBrief.json` 與 `data/defaults/DefaultEventRoute.json`。初次使用或本機檔案缺失時自動還原複製（亦內嵌於組件中）。
- **更新服務類別**：[EventArchiveService.cs](file:///d:/MyDoujinBot/MyDoujinBot/Services/EventArchiveService.cs)
- **更新運作方式**：
  1. 若傳入 Claude Artifact 主網址，自動解析其 `uuid` 與 `manifest` 並查詢後端 API 動態合成當前最新沙盒 Frame URL（若遇 Cloudflare WAF 阻擋則自動啟用 `curl.exe` 備援），再下載沙盒 HTML。
  2. 提取 `<script id="seed">` 原始資料庫與計算腳本。
  3. 動態識別「匯出事件檔」與「匯出路線檔」按鈕所呼叫之函式（`buildExport` 與 `buildRouteExport`）。
  4. 透過內嵌輕量 JS 引擎 **Jint (4.1.0)** 執行計算並輸出 JSON 字串。
  5. 驗證格式無誤後，採暫存檔換名（Atomic Save）安全覆寫本地檔案庫。
  6. 於覆寫前讀取現有資料庫狀態，更新後精確計算事件與路線增減差異（`BriefEventDiff`、`RouteChainDiff`、`RouteEventDiff`）並擷取新增角色路線名稱（`NewChainNames`），於完成彈出對話框、彩色日誌與面板狀態列即時回報（例如：`事件 +2 / 路線 +1` 或 `無增減`）。
