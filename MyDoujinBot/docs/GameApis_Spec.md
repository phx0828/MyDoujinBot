# 遊戲延伸 API 規格文件 (Game APIs Spec)

> 狀態：**已確認端點與遊戲頁面欄位對齊**。
> 資料來源：取自 `MyDoujinBot/LOG/` 內真實擷取之 API Response JSON，並與遊戲介面之各項名詞完成 100% 精準對照。
> 通訊原則：所有 API 均為 `GET` 請求，Header 配置沿用既有 `HttpService`（含 Bearer Token、User-Agent、Referer、Origin）。

---

## 1. 玩家自身基本資訊 (`users/me`)

- **端點 URL**：`GET https://mydoujin-backend.onrender.com/api/users/me`
- **資料檔案**：`MyDoujinBot/LOG/me.txt`
- **用途**：取得目前登入使用者的帳號資訊、當前出戰角色 ID 與目前所在最高樓層。

### 欄位結構
| 欄位名稱 | 型別 | 遊戲介面對應名稱 | 說明 / 範例值 |
| :--- | :--- | :--- | :--- |
| `id` | `string` | 使用者 ID | 唯一識別碼 (UserId，與 Token 一致)，例：`"6aace3d256161900a8a0f4bf"` |
| `discordId` | `string` | Discord ID | 綁定之 Discord 使用者 ID，例：`"460854284087721984"` |
| `username` | `string` | 帳號名稱 | 登入帳號，例：`"phx0828"` |
| `nickname` | `string` | **暱稱** | 玩家個人公開暱稱，例：`"阿爾吉利亞"` |
| `personalStatus` | `string` | **個人狀態** | 個人個性簽名，例：`"Shringara Love：啊好痛苦但這才是愛，你也感受吧"` |
| `activeCharacterId`| `string` | 出戰角色 ID | 當前出戰角色之實例 ObjectId (對應 `active.txt` 的 `_id`)，例：`"6acf0df8734f55ff241d9943"` |
| `currentFloor` | `int` | 當前最高樓層 | 目前通關/所在最高樓層，例：`41` |

---

## 2. 行動與即時狀態 (`action/status`)

- **端點 URL**：`GET https://mydoujin-backend.onrender.com/api/action/status`
- **資料檔案**：`MyDoujinBot/LOG/status.txt`
- **用途**：取得即時冷卻剩餘時間、未決策的突發事件、樓層獎勵冷卻與滿等狀態。

### 欄位結構
| 欄位名稱 | 型別 | 說明 / 作用 |
| :--- | :--- | :--- |
| `cooldownRemainingMs` | `int` | **重要：距離下次可行動的冷卻剩餘毫秒數**（如 `1797`，可用於啟動時精確同步冷卻） |
| `cooldownMs` | `int` | 基礎冷卻時間長度 (毫秒，例：`10000` = 10 秒) |
| `pendingEvent` | `PendingEvent?` | **重要：目前是否有卡住未處理的事件**（無為 `null`，有則為事件詳情與選項清單） |
| `isMaxLevel` | `bool` | 當前出戰角色是否已達等級上限 |
| `lastActionAt` | `DateTime` | 上次執行行動之 UTC 時間 (例：`"2026-10-16T09:12:10.276Z"`) |
| `serverTime` | `DateTime` | 伺服器當前 UTC 時間 (例：`"2026-10-16T09:12:18.479Z"`) |
| `floorReward` | `FloorRewardInfo` | **樓層獎勵物件** (詳見下方子結構) |
| `trainingSpec` | `TrainingSpecInfo` | 委託/長時間訓練設定規格 (`minMinutes: 30`, `maxMinutes: 480`, `stepMinutes: 30`) |

#### `floorReward` 樓層獎勵子結構
| 欄位名稱 | 型別 | 說明 |
| :--- | :--- | :--- |
| `floor` | `int` | 當前可領取獎勵之樓層 (例：`41`) |
| `exp` | `int` | **樓層獎勵給的經驗值** (例：`4100`) |
| `cooldownRemainingMs` | `int` | **樓層獎勵領取冷卻時間**（為 `0` 時代表已可領取！） |
| `cooldownMs` | `long` | 獎勵結算週期 (毫秒，例：`14400000` = 4小時) |
| `isDead` | `bool` | 角色當前是否死亡 |
| `isMaxLevel` | `bool` | 角色當前是否滿等 |

---

## 3. 當前出戰角色詳情 (`characters/active`)

- **端點 URL**：`GET https://mydoujin-backend.onrender.com/api/characters/active`
- **資料檔案**：`MyDoujinBot/LOG/active.txt`
- **用途**：取得當前出戰角色的各項九圍能力、扔屎與沾屎戰績統計、技能配備與轉生狀態。
- **頂層包裹**：`{ "character": { ... } }`

### 基本資訊
| 欄位名稱 | 遊戲介面對應名稱 | 說明 / 數值範例 |
| :--- | :--- | :--- |
| `_id` | 角色實例 ID | ObjectId，例：`"6acf0df8734f55ff241d9943"` |
| `name` | **角色** | 角色名稱，例：`"同人"` |
| `title` | **稱號** | 角色頭銜，例：`"我的同人"` |
| `level` | **等級** | 目前等級，例：`67` |
| `exp` / `totalExp` | 經驗值 / 總經驗值 | 當前等級 EXP (`1710`) / 累計總 EXP (`79095`) |
| `reincarnationCount` | **轉生次數** | 轉生次數，例：`22` |
| `isDead` | 死亡標記 | 是否處於死亡狀態 |
| `isSoiled` | 沾屎標記 | 是否處於髒污狀態 |
| `normalAttackId` | 普攻技能 | 代號，例：`"standard_offensive_magic"` |
| `skillIds` | 配備技能 | 主動技能 ID 清單 (`["minor_erdtree", "first_aid", "simmer_staff_meal"]`) |

### 九大戰鬥屬性 (`character.stats`)
| 欄位名稱 | 遊戲名稱 | 數值範例 | 遊戲內機制說明 |
| :--- | :--- | :--- | :--- |
| `hp` | **HP** | `8740` | 生命值，歸零即倒下。 |
| `atk` | **攻擊** | `167` | 影響普通攻擊與技能傷害。 |
| `def` | **防禦** | `336` | 減少受到的傷害。 |
| `sta` | **體力** | `178` | 影響持續作戰能力與部分技能發動。 |
| `agi` | **敏捷** | `175` | 影響迴避與攻擊命中。 |
| `spd` | **反應速度** | `158` | 決定先攻、連擊次數，以及額外回合。 |
| `tec` | **技巧** | `121` | 影響命中、爆擊傷害，以及反擊機率。 |
| `int` | **智力** | `549` | 影響法術傷害與治療量。 |
| `luk` | **幸運** | `103` | 最神奇的屬性，能在戰鬥中造成意想不到的效果。 |

### 戰績數據對應表 (`combatStats` vs `totalCombatStats`)
遊戲介面個人資訊完全對應以下兩組欄位（本輪 vs 生涯累計）：

| 遊戲介面標籤 | 對應 JSON 欄位 | 數值範例 (active.txt) | 說明 |
| :--- | :--- | :--- | :--- |
| **主動扔屎** | `combatStats.activeThrows` | `0` | 本輪主動對敵人丟小零食次數 |
| **防衛扔屎** | `combatStats.defenseThrows` | `0` | 本輪防守反擊丟小零食次數 |
| **總主動扔屎** | `totalCombatStats.activeThrows` | `0` | 歷史生涯主動丟小零食累計 |
| **總防衛扔屎** | `totalCombatStats.defenseThrows` | `11` | 歷史生涯防守反擊丟小零食累計 |
| **遭襲沾屎** | `totalCombatStats.assaultSoiled`| `7` | 被敵方主動襲擊丟中沾屎累計 |
| **遭反殺沾屎** | `totalCombatStats.counterSoiled`| `0` | 攻擊敵方時被敵方反殺丟中沾屎累計 |
| **勝場** | `combatStats.wins` | `10` | 當前輪次勝場 |
| **敗場** | `combatStats.losses` | `0` | 當前輪次敗場 |
| **總勝場** | `totalCombatStats.wins` | `417` | 歷史生涯總勝場 |
| **總敗場** | `totalCombatStats.losses` | `130` | 歷史生涯總敗場 |

---

## 4. 全角色清單與轉生配點 (`characters`)

- **端點 URL**：`GET https://mydoujin-backend.onrender.com/api/characters`
- **資料檔案**：`MyDoujinBot/LOG/characters.txt`
- **用途**：取得玩家擁有的全部角色清單、事件偏好名單、最高跳樓層數以及分配轉生點配置。

### 核心頂層欄位
| 欄位名稱 | 型別 | 遊戲介面對應名稱 | 說明 |
| :--- | :--- | :--- | :--- |
| `deprioritizedCharacterIds` | `List<string>` | **事件偏好** | 可以減少所選角色的相關事件；共用事件仍可能正常出現（例：`["agil"]`）。 |
| `jumpMaxFloor` | `int` | **跳樓最高層數** | 當前可直接跳躍挑戰之最高層數（例：`41`）。 |
| `characters` | `List<Character>` | **角色列表** | 玩家持有的所有角色（含傳承等級 `legacyLevel` 與轉生加成數值 `reincarnationStats`）。 |
| `reincarnation` | `ReincarnationInfo`| **分配轉生點** | 轉生點數儲存、加成配點分配與層數解鎖開關。 |

### 轉生點數機制 (`reincarnation`)
- **可用點數** (`available`): `0`
- **已累積點數** (`total`): `155`
- **本次分配** (`spent`): `155`
- **點數上限** (`maxPoints`): `155`
- **規則記錄**：
  1. 每次轉生都會退回全部舊配點，從零重新分配；未分配的點數會保留。
  2. 角色每升到 16 等以上的每一級可獲得 1 點，上限 `maxPoints` 點。
  3. 歷史最高樓層從 31 層起，每層提高上限 5 點；跳樓後保留，上限提高不直接贈送點數。

### 轉生配點屬性與層數解鎖清單 (`reincarnation.allocated`)
| 屬性代號 | 遊戲介面名稱 | 配點範例 | 解鎖條件 | 遊戲內機制說明 |
| :--- | :--- | :--- | :--- | :--- |
| `hp` | **HP** | `30` | 基礎預設 | 生命值，歸零即倒下。 |
| `atk` | **攻擊** | `0` | 基礎預設 | 影響普通攻擊與技能傷害。 |
| `def` | **防禦** | `0` | 基礎預設 | 減少受到的傷害。 |
| `sta` | **體力** | `0` | 基礎預設 | 影響持續作戰能力與部分技能發動。 |
| `agi` | **敏捷** | `0` | 基礎預設 | 影響迴避與攻擊命中。 |
| `spd` | **反應速度** | `0` | 基礎預設 | 決定先攻、連擊次數，以及額外回合。 |
| `tec` | **技巧** | `0` | 基礎預設 | 影響命中、爆擊傷害，以及反擊機率。 |
| `int` | **智力** | `95` | 基礎預設 | 影響法術傷害與治療量。 |
| `luk` | **幸運** | `0` | 基礎預設 | 最神奇的屬性，能在戰鬥中造成意想不到的效果。 |
| `expBonus` | **經驗值加成** | `0` | **20 層通關解鎖** (`utilityUnlocked`) | 所有經驗來源依每點倍率增加。 |
| `eventBonus` | **事件觸發加速** | `30` | **20 層通關解鎖** (`utilityUnlocked`) | 遭遇事件機率依每點倍率增加。 |
| `effortBonus` | **增加行動努力值** | `0` | **30 層通關解鎖** (`advancedUnlocked`) | 提高行動取得的努力值，讓累積的努力值支撐更多次連續升級；不增加每級成長總量。 |
| `aggroIncrease` | **增加仇恨值** | `0` | **30 層通關解鎖** (`advancedUnlocked`) | 提高被依仇恨選敵的攻擊選中的相對權重。 |
| `aggroDecrease` | **減少仇恨值** | `0` | **30 層通關解鎖** (`advancedUnlocked`) | 降低被依仇恨選敵的攻擊選中的相對權重。（增加與減少仇恨同時配點會互相抵銷，仍各自消耗點數） |
| `legacyUnlocked` | **傳承系統** | - | **60 層通關解鎖** (`legacyUnlockFloor: 61`) | 通關第 60 層後解鎖傳承：轉生時保留一半的總經驗值與對應的養成。 |
