# 產學合作計劃表：FIPS 203/204 演算法驗證網頁工具

**版本** v0.1 · **建立日** 2026-06-16 · **期程** 約 3 個月（一學期，12 週）· **人力** 2–3 人小組

---

## 1. 計畫目標

為菲律賓合作公司建立一個**自裝式（self-hosted）網頁工具**，讓**非密碼專業**的使用者，
不需任何 CLI 指令，即可驗證其密碼模組（IUT）的 **FIPS 203（ML-KEM）** 與 **FIPS 204（ML-DSA）**
實作是否正確。底層直接複用 NIST ACVP-Server 的 Gen/Val 引擎。

### 範圍（本學期凍結）
- **FIPS 203 / ML-KEM**：`keyGen`、`encapDecap`
- **FIPS 204 / ML-DSA**：`keyGen`、`sigGen`、`sigVer`
- 參數集：ML-KEM-512/768/1024；ML-DSA-44/65/87
- **不做**：其他演算法、雲端多租戶、帳號權限系統（除非提早完成）

### 關鍵前提（已驗證）
- repo 已支援上述全部模式，且提供程式化介面 `IGenValInvoker`
  （`CheckParameters` / `GenerateAsync` / `ValidateAsync`）→ **後端直接呼叫，免 spawn CLI**。
- 公司的 IUT（機密密碼模組）**永遠在公司端執行**；網頁只負責出題、收答、批改，
  **不接觸其原始碼或金鑰** → 自裝部署天然符合資料不出門的需求。

---

## 2. 系統架構（自裝包）

公司端執行 `docker compose up` 即啟動全套，資料完全留在公司內網：

```
┌──────────────────────────────────────────────────────┐
│  Docker Compose 套件                                    │
│                                                        │
│  ┌────────────────┐      ┌──────────────────────────┐ │
│  │ Container: Web  │ ───▶ │ Container: Orleans Silo   │ │
│  │  ASP.NET Core 8 │      │  (ServerHost, 既有專案)   │ │
│  │  - REST API     │ gw   │  - 執行全部 crypto         │ │
│  │  - 參考 Gen 函式庫│:30000│    (ML-KEM / ML-DSA)      │ │
│  │  - IGenValInvoker│      └──────────────────────────┘ │
│  │  - 前端 SPA      │                                    │
│  │  - 工作佇列      │      (選用) SQLite：保存歷史紀錄    │
│  └────────────────┘                                    │
└──────────────────────────────────────────────────────┘
        ▲ 瀏覽器（公司非技術人員）
```

### 技術選型
| 層 | 選擇 | 理由 |
|----|------|------|
| 後端 | **ASP.NET Core 8** Web API | 與 repo 同為 .NET 8，可直接參考 Generation/Common 函式庫 |
| 前端 | **React + Vite**（或 Blazor） | 簡單 SPA；若團隊偏 .NET 可選 Blazor 減少切換 |
| 運算 | 既有 **Orleans Silo** | 不改動，當作 crypto 後端 |
| 封裝 | **Docker Compose** | 一鍵啟動、跨平台、資料不出門 |
| 持久化 | 檔案系統 +（選用）**SQLite** | 保存 prompt/validation 產物與歷史 |

### 使用者流程（非專家視角）
1. 選演算法（ML-KEM / ML-DSA）與模式（卡片式 UI）
2. 勾選參數集，進階選項給安全預設值
3. 按「產生測試向量」→ 後端 `GenerateAsync` → 下載 `prompt.json` + 操作說明
4. 公司用自己的模組跑 `prompt.json`（離線）→ 產生 `responses.json`
5. 上傳 `responses.json` → 後端 `ValidateAsync` → 顯示**可讀的成績報告**（逐題 pass/fail、摘要、可下載 `validation.json`）

---

## 3. 公司端整合支援（最關鍵的去風險）

整個案子最不確定的一段，不在我們的網頁，而在**公司端如何產生 `responses.json`**——
因為那段程式在他們的機器、呼叫他們的模組，我們看不到。這節定義我們要交付的支援物。

### 觀念：產生 `responses.json` 與交付方式無關
- Docker / 托管交付的是**我們的工具**（Gen/Val 引擎 + 網頁），**不包含也碰不到公司的 IUT**。
- `responses.json` 永遠在工具**之外**、由公司用自家模組產生。**任何交付方式都躲不掉這一步。**

### 公司端兩種角色
| 角色 | 工作 | 需技術 |
|------|------|--------|
| 操作者/主管（外行） | 網頁上選演算法、產生題目、上傳回應、看 pass/fail 報告 | ❌ |
| 他們的工程師（內行） | 把 `prompt.json` 餵給自家模組、產生 `responses.json` | ✅ 不可避免 |

> 能寫出 FIPS 203/204 實作的公司，必有會寫程式的工程師；「答題」這步交給他們合理且無法替代。

### 完整流程（以 Docker 交付為例）
```
1. docker compose up                      ← 一次性啟動我們的工具
2. 瀏覽器 → 選 ML-KEM keyGen → 下載 prompt.json
3. 【他們工程師】harness：prompt.json → responses.json   ← 唯一需寫程式的一步
4. 瀏覽器 → 上傳 responses.json → 看可讀報告
```
第 1、2、4 步外行可獨立完成；只有第 3 步需要他們的工程師。

### 我們要交付的四項支援（把第 3 步降到「填一行程式」）
1. **範例 harness**：ML-KEM / ML-DSA 各一份 Python 範本，把「讀題 → 呼叫模組 → 寫答」骨架寫好，
   **只留「呼叫自家模組」那一行讓他們填**（參考 demo 的 `iut_sha256.py` 形式）。
2. **欄位對照文件**：每個演算法逐一列出 prompt 的輸入欄位、responses 該填的輸出欄位，
   並標明編碼與長度（哪個是 hex、哪個是 base64、位元組長度）。
3. **網頁附範例檔**：下載 `prompt.json` 時，一併附「對應的範例 `responses.json`」與操作說明，
   讓他們有現成格式可直接比對。
4. **強健的錯誤訊息**：上傳格式錯誤時明確指出問題（例如「tcId 3 缺少 `ek` 欄位」），
   而非拋出原始堆疊；常見錯誤給修正提示。

### 交付時程對接
- 上述 1–3 在 **S2（後端全模式覆蓋）完成時**即可定版，讓公司工程師能提早並行開發 harness。
- 第一個里程碑就用 **sample 向量與公司對接一次**，及早發現 JSON 格式落差（呼應風險 R6）。

---

## 4. 團隊角色（2–3 人）

| 角色 | 主責 |
|------|------|
| **Dev A — 後端/.NET** | API、`IGenValInvoker` 整合、Orleans client 接線、Docker 封裝 |
| **Dev B — 前端/UX** | UI 流程、成績報告畫面、使用者文件（英文） |
| **Dev C（若有）/ 共享** | 測試、整合、UAT、黃金測試（與 CLI 比對）；教授擔任架構與驗收顧問 |

---

## 5. 期程：6 個 Sprint × 2 週 = 12 週

| Sprint | 週次 | 主題 | 里程碑 |
|--------|------|------|--------|
| **S0** | W1–2 | 地基 + 關鍵驗證 spike | **M1** 程式化呼叫打通 |
| **S1** | W3–4 | 後端 API 核心 | API 可對 ML-KEM keyGen 出題/批改 |
| **S2** | W5–6 | 涵蓋全部 203/204 模式 + 非同步工作 | **M2** 後端全模式覆蓋 |
| **S3** | W7–8 | 前端 MVP | **M3** 瀏覽器端到端跑通 |
| **S4** | W9–10 | 非專家 UX + 成績報告 | **M4** 非技術人員可獨立操作 |
| **S5** | W11–12 | 封裝、文件、驗收 | **M5** 自裝包交付 + 簽收 |

### Sprint 細部

**S0（W1–2）地基與 spike — 最重要的去風險**
- repo 可建置、Orleans 可跑（demo 已大致完成）
- **Spike：寫一個最小 .NET host，程式化呼叫 `IGenValInvoker` 完成 ML-KEM keyGen 的 Generate + Validate（不經 CLI）** ← 全案成敗關鍵，先證明可行
- 移植 `AutofacConfig` 與 Orleans client 接線到獨立 host
- 決定前端框架、建立 repo 骨架與 CI
- 交付物：證明「程式化 ML-KEM 出題+批改」可行的測試程式

**S1（W3–4）後端 API 核心**
- 建立 ASP.NET Core API，參考 Generation 函式庫 + Orleans client
- 端點：`POST /check`、`POST /generate`、`POST /validate`（小資料同步）
- 交付物：以 curl/Postman 對 ML-KEM keyGen 完成出題/批改

**S2（W5–6）全模式覆蓋 + 非同步工作 + 公司整合包定版**
- 全部：ML-KEM keyGen/encapDecap、ML-DSA keyGen/sigGen/sigVer
- 長時間 generate/validate 改為**背景工作 + 輪詢狀態端點**（避免 HTTP 逾時）
- registration 預設集 + 伺服器端輸入驗證
- **定版公司整合包（第 3 章支援項 1–3）**：ML-KEM / ML-DSA 範例 harness、欄位對照文件、範例 `responses.json`
- **與公司用 sample 向量試對接一次**，及早抓 JSON 格式落差（呼應 R6）
- 交付物：5 種 演算法×模式 組合皆可 API 出題+批改；公司整合包初版交付

**S3（W7–8）前端 MVP**
- UI 流程：選演算法/模式 → 參數 → 產生 → 下載
- 上傳 responses → 批改 → 結果頁
- 交付物：單一演算法在瀏覽器端到端跑通

**S4（W9–10）非專家 UX + 報告**
- 友善預設、提示/術語小辭典
- **強健的錯誤訊息（第 3 章支援項 4）**：上傳格式錯誤精確定位到 tcId/欄位級別，附修正提示
- 可讀的驗證報告（摘要 + 逐題、醒目標示失敗項）
- （選用）歷史清單（SQLite）
- 交付物：非技術人員無需 CLI 即可操作

**S5（W11–12）封裝、文件、驗收**
- Dockerfile + docker-compose（silo + web），一鍵啟動，可調 `MaxConcurrentWork`
- 英文使用手冊（給公司）+ 交接文件
- 與公司做 UAT（sample + 真實向量），修 bug
- 交付物：自裝包 + 文件 + 驗收簽核

---

## 6. 驗收標準

- [ ] 非專家僅透過瀏覽器即可對**全部 FIPS 203/204 模式**出題並驗證回應
- [ ] 公司端以單一 `docker compose up` 完成自裝
- [ ] 網頁驗證結果與 CLI 結果一致（黃金測試比對）
- [ ] 文件足以讓公司獨立操作（含 prompt/responses 格式說明）

---

## 7. 風險清單

| # | 風險 | 衝擊 | 緩解 |
|---|------|------|------|
| R1 | 程式化 DI/Orleans 接線比預期難 | 高 | **S0 spike 先驗證**；退路：暫時 shell-out 呼叫 GenValAppRunner CLI |
| R2 | 長時間 MCT/大向量塞住 HTTP | 中 | S2 改非同步背景工作 + 輪詢 |
| R3 | 公司 OS 未知，跨平台 | 中 | Docker 抽象；於 Linux + Windows 各測一次 |
| R4 | ML-DSA 進階選項（hashAlgs/deterministic/externalMu/preHash…）讓使用者卻步 | 中 | 提供預設、隱藏進階選項 |
| R5 | 大檔上傳/下載 | 中 | 串流、限制大小、必要時分塊 |
| R6 | 公司 IUT 回應格式與 ACVP 不符 | 中 | 提供清楚 prompt + 範例 responses + 格式文件；強化錯誤訊息 |
| R7 | 範圍蔓延（要求加其他演算法） | 中 | 本學期凍結於 203/204，其餘列為後續 |
| R8 | repo 更新造成破壞性變更 | 低 | 釘選特定 commit / 版本 |

---

## 8. 後續可擴充（本學期外）

- 擴充至其他 PQC（SLH-DSA / LMS）或傳統演算法
- 帳號/多專案管理、驗證歷史儀表板
- 自動產生符合 CMVP 提交格式的報告
- 實驗室托管版（SaaS）作為自裝版之外的選項
