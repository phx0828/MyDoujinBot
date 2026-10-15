# 戰鬥數據功能 需求規格(Battle Stats Spec)

> 狀態:**僅記錄需求,尚未實作**。等待使用者提供其餘 LOG 檔案,用以驗證 API response 格式與統計規則。
> 原則:沿用現有專案風格與分層(Models / Services / Forms / Utilities),不另起新結構。

## 1. 任務

玩家輸入戰報編號 → 呼叫 API 取得戰報 → 統計每位玩家的 輸出 / 承傷 / 治療 / 出手 / 爆擊 → 以長條圖顯示。

## 2. 輸入:API

- `GET https://mydoujin-backend.onrender.com/api/battle-reports/{戰報編號}`(例:`.../118253`)
- 錯誤處理(需明確提示玩家):編號不存在、網路失敗、逾時、回傳格式異常。

### 回傳資料結構

**基本資訊**:`reportId`、`battleType`(例 BOSS)、`boss`、`result`(WIN/LOSE)、`createdAt`

**participants.players**
- `userId`(**唯一識別**,顯示與讀 LOG 一律以此辨識)、`name`、`characterName`
- `characterId`、`title`、`level`、`stats`(hp, atk, def, sta, agi, spd, tec, int, luk)、`imagePath`(本功能不使用)

**participants.enemies**
- `entityId`(**唯一識別**,例 `mob_0_three_brothers_liu_bei`)
- `monsterId`(不可作唯一識別)、`name`、`title`、`floor`、`level`、`stats`、`imagePath`
- 召喚物也可能在此清單,帶 `summonerId`(召喚者 userId),`stats` 可能為 null。

**outcomes**
- `userId`、`name`(此處為**角色名稱**)、`expGained`、`newFloor`、`isDead`
- 可能缺少某位玩家,缺少時 `isDead` 視為 false,不可報錯。

### 資料處理規則
- 玩家清單以 `participants.players` 為準;頂層 `userIds` 不可作為玩家清單。
- 完整保存 players 與 enemies,以 `userId` / `entityId` 建立對照表。
- LOG 中的 ID 一律透過對照表轉換為名稱,不比對名稱字串。

## 3. 輸入:戰鬥 LOG

### 基本規則
- LOG 在頂層 `logs` 陣列,依序逐筆處理,以 `type` 區分事件。
- 一律使用結構化欄位,**不解析 `message`**。
- 傷害用 `actualDamage`(已扣除溢出),不用 `value`;只有 COUNTER、LUCK_EVENT、BUFF_EFFECT 沒有 `actualDamage`,才用 `value`。
- 玩家與敵方都累計,畫面只顯示玩家,敵方保留供日後擴充。
- 目前只有玩家↔敵方傷害,沒有友軍傷害。
- 遇到不在 participants 內的 ID(如 `summon_1`),建立臨時單位,不中斷。

### 召喚物歸屬
- 召喚者對照表:處理 SUMMON 時記錄 `targetId`(召喚物)→ `actorId`(召喚者),並以 enemies 的 `summonerId` 補充。
- 召喚物「主動造成」的傷害(DAMAGE、BLOCK、COUNTER 中 actor 為召喚物):輸出、出手、爆擊都算召喚者。
- 僅限上述攻擊;召喚物承傷、召喚物治療**不**算給召喚者。
- 玩家攻擊召喚物時,輸出仍算攻擊的玩家。

### 單位統計欄位
輸出、承傷、治療、出手次數、命中次數、爆擊次數;命中 = 出手 − MISS;命中率 = 命中 ÷ 出手;爆擊率 = 爆擊 ÷ 命中(分母為 0 時為 0)。

### PVP 戰報
- `battleType == "PVP"`;對手同樣放在 `participants.enemies`,但**沒有 `entityId`**,改以 `userId` 識別,並帶有 `characterName`。
- 敵方單位一律以 `entityId ?? userId` 作為唯一識別(`BattleReportEnemy.Id`)。
- 統計規則與 PVE 相同;圖表下方區塊標題改為【對手統計】,標題行格式同玩家:`玩家名稱 (角色名稱) 【倒下】`。
- `CHADO`(茶渡結尾嘲諷)為已辨識、不影響統計的事件。

### 事件處理規則

| type | 規則 |
|---|---|
| DAMAGE | 略過 `isCounter == true`(與前一筆 COUNTER 重複)。actor 輸出 +actualDamage;target 承傷 +actualDamage。**有 `isCrit` 欄位**才算一次出手(actor 出手 +1,`isCrit==true` 爆擊 +1)。**無 `isCrit`** 只累傷害不算出手:source=REFLECTION(傷害算 actor)、source=DIRECT、skillId=`three_brothers_guard`(替身承受;actor 是原攻擊者,target 是替身者) |
| BLOCK | 同 DAMAGE:actor 輸出、target 承傷,用 actualDamage。每筆都算一次出手(isCrit 則爆擊 +1)。`actualDamage==0` 是被替身擋下,後接一筆 three_brothers_guard DAMAGE,傷害由該筆累計,BLOCK 只算出手。方向不可相反(actor 攻擊方、target 被攻擊方) |
| MISS | actor 為攻擊方、target 為閃避方。actor 出手 +1,不算命中、不爆擊、不累計傷害 |
| COUNTER | actor 輸出 +value;target 承傷 +value。不算出手、不算爆擊 |
| LUCK_EVENT | actor 承傷 +value;`opponentId` 輸出 +value。不算出手(目前 actorId==targetId,opponentId 為對立陣營) |
| HEAL | actor 治療 +value(算施放者);actor 與 target 可相同 |
| BUFF_EFFECT | `buffType=HOT`:actorId 治療 +value。`buffType=DOT`:actorId 承傷 +value,不歸屬任何人輸出。無 buffType:略過 |
| DEATH | 用來判斷玩家「倒下」狀態 |

**已辨識、不影響統計(略過,不計入「無法辨識」)**:
BATTLE_START、BOSS_DIALOGUE、SKILL_TEXT、TEXT、BUFF_APPLY、SP_RECOVER(回復 SP,不算治療)、SUMMON(只建召喚者對照表)、HP_REMAINING、EXP_GAIN、SANITY_CHANGE、SANITY_STATE_CHANGE、FATIGUE、BRICK_CRACK、PLAYER_DEATH、PLAYER_DEFEAT(死亡狀態改由 `outcomes.isDead` 判斷)

### 解析要求
type 不在上述清單、或缺少必要欄位的資料:略過並計數,結束時回報筆數。

## 4. 統計規則
- 以單位(userId/entityId)累加 輸出、承傷、治療、出手、爆擊。
- 畫面只顯示 `participants.players`。
- 出手、爆擊、爆擊率需計算並顯示。

## 5. 輸出格式

每位玩家:標題行 → 輸出 / 承傷 / 治療 三行長條 → 一行出手與爆擊資訊。

### 標題行
格式:`玩家名稱 (角色名稱) 【倒下】 【死亡】`
- 玩家名稱 = `players.name`;角色名稱 = `players.characterName`
- 【倒下】:logs 中存在 `type==DEATH` 且 `actorId==該玩家 userId`
- 【死亡】:`outcomes` 中該玩家 `isDead==true`(缺資料視為 false)
- 兩標記獨立判斷、可同時出現;都不符合則不顯示任何標記,**不留多餘空格**
- 【死亡】= 紅色粗體;【倒下】= 橘色粗體;其餘文字預設樣式

### 範例
```
老ㄐ訶德 (羅伊·馬斯坦) 【倒下】 【死亡】
輸出 ███████████ 125,000
承傷 ███████ 82,000
治療 ████ 45,000
出手 163 次｜命中 160 次｜命中率 98.2%｜爆擊 70 次｜爆擊率 43.8%

阿爾吉利亞 (塞雷婭) 【倒下】
輸出 ████ 45,000
承傷 ███████████ 82,000
治療 ███████████ 125,000
出手 8 次｜命中 8 次｜命中率 100.0%｜爆擊 2 次｜爆擊率 25.0%
```

### 圖表規格
- 顏色:輸出=紅、承傷=藍、治療=綠(長條與對應顏色一致)
- 長條長度:每個項目**各自**以該項目最大值為 100% 等比例縮放
- 數值千分位逗號;非零數值至少 1 格

### 出手與爆擊資訊
- 三條長條之後另起一行,純文字、不畫長條、預設顏色。
- 格式:`出手 N 次｜命中 N 次｜命中率 N.N%｜爆擊 N 次｜爆擊率 N.N%`
- 數值直接使用統計累計欄位,不另外重算。
- 出手含 MISS;召喚物攻擊算召喚者;反擊不計入。
- 命中 = 出手 − MISS(被完全擋下的 BLOCK 仍算命中)。
- 命中率 = 命中 ÷ 出手 × 100;爆擊率 = 爆擊 ÷ 命中 × 100;皆四捨五入到小數 1 位。
- 分母為 0 → 顯示 `0.0%`,不可除以零或出現 NaN。
- 數字使用千分位逗號。

## 6. 其他要求
- 取得資料、解析、統計、繪圖分成獨立函式/類別。
- 每種事件類型的處理獨立成各自函式或區塊,方便新增事件類型。
- 附簡短使用說明與測試資料,需涵蓋:有人倒下、有人死亡、無人倒下、outcomes 缺玩家、含召喚物,且涵蓋所有上述事件類型。
- 完成後以下列驗收數值自行核對;對不上時**說明原因,不可自行改規則湊數字**。
- 盡量結構化、解耦,降低維護成本。

## 7. 驗收數值

> 註:下表「爆擊率」為舊公式(爆擊 ÷ 出手)的數值,改為 爆擊 ÷ 命中 後需依實際 MISS 數重新核對。

**LOG6**(WIN,outcomes 缺芙莉蓮)

| 玩家 | 輸出 | 承傷 | 治療 | 出手 | 爆擊 | 爆擊率 | 標記 |
|---|---|---|---|---|---|---|---|
| 源氏 | 101,225 | 16,547 | 11,697 | 163 | 70 | 42.9% | 無 |
| 壺頭哥 | 18,642 | 6,427 | 0 | 42 | 19 | 45.2% | 【倒下】 |
| 芙莉蓮 | 46,673 | 6,004 | 790 | 25(含 3 次落空) | 11 | 44.0% | 無 |

**LOG1**(LOSE,含召喚物;格林輸出包含召喚物造成的 349)

| 玩家 | 輸出 | 承傷 | 治療 | 出手 | 爆擊 | 爆擊率 | 標記 |
|---|---|---|---|---|---|---|---|
| 愛麗絲 | 38,394 | 7,801 | 2,693 | 46 | 16 | 34.8% | 【倒下】【死亡】 |
| 塞雷婭 | 7,071 | 12,905 | 2,982 | 8 | 2 | 25.0% | 【倒下】 |
| 格林 | 37,509 | 16,651 | 4,308 | 72 | 22 | 30.6% | 【倒下】【死亡】 |

**可選對帳**:被擊殺敵人的「承傷總和」應等於其 `stats.hp` + 它受到的治療。

## 8. 現有專案參考(實作時沿用)

- 網路層:[HttpService.cs](../Services/HttpService.cs) 的 `GetAsync<T>` / `ApiResponse<T>`(已含 timeout 30s、HTTP 錯誤、JsonException 處理)。
  - ⚠ 目前 `GetAsync` **強制要求 Token**;需確認 battle-reports 端點是否需要驗證(見待確認)。
- 業務層:[BattleService.cs](../Services/BattleService.cs)(`BaseUrl` 已為 `https://mydoujin-backend.onrender.com/api`)。
- Models:[Models/](../Models/)(已有 `BattleLog`、`BattleResult`,供「戰鬥報告」視窗使用)。
- UI:[BattleReportForm.cs](../Forms/BattleReportForm.cs) 為深色主題(背景 `28,28,35`、字型 Microsoft JhengHei UI、RichTextBox LOG 區),新視窗應與之風格一致。

## 9. 預計分層(草案,待看過 LOG 後確認)

| 層 | 職責 |
|---|---|
| Models | 戰報 DTO(`BattleReportResponse`、players/enemies/outcomes/logs)、`UnitStats`、`BattleStatsResult` |
| Services(取得) | `BattleService.GetBattleReportAsync(id)` |
| Services(解析/索引) | 建立 userId/entityId 對照表、召喚者對照表、臨時單位 |
| Services(統計) | `BattleStatsCalculator`:事件分派表 `type → handler`,每種事件獨立 handler;回報略過筆數 |
| Utilities(繪圖/格式) | 長條字串、千分位、爆擊率格式化、標題標記判斷 |
| Forms | 戰鬥數據視窗(輸入編號、顯示 RichTextBox 結果),與主畫面入口 |
| 測試資料 | 涵蓋所有事件與情境的範例 JSON + 使用說明 |

## 10. 已確認決策
1. **Token**:battle-reports 端點統一需要 Bearer Token,直接沿用 `HttpService.GetAsync`(Token 未設定時回傳既有的提示訊息,401 沿用既有處理),不需新增免 Token 的 GET。
2. **入口位置**:MainForm 左側設定區,放在「自動訓練 / 自動戰鬥」下方,新增一顆「戰鬥數據」按鈕,點擊開啟獨立的戰鬥數據視窗(實作時需先確認該區現有的版面與控制項命名)。
3. **其餘 LOG 檔案**:使用者稍後提供,用於驗證 API response 格式與統計規則。

## 11. 待確認事項
1. 測試資料/驗證方式(專案目前無測試專案):新增 xUnit 專案,或僅附範例 JSON + 使用說明?

