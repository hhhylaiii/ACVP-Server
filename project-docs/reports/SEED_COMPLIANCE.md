# ACVP Seed 生成是否符合 NIST 標準 — 合規性分析報告

> 本報告回答一個常被誤解的問題：「這個 ACVP-Server（NIST 官方參考實作）產生 seed 的方式，符不符合 NIST 標準？」
>
> 核心結論：**「合規」其實是兩個完全不同的問題，必須分層回答。**
> 真正該符合標準的「演算法層」**符合 FIPS 203/204**；產測試 seed 的「亂數層」**不是合規 DRBG，但也不需要是**。
>
> 相關文件：[[SEED_REPORT.md]]（seed 從哪來、如何展開成金鑰的詳細呼叫鏈）。

---

## 0. 一頁式結論（給老師看的摘要）

| | 第 1 層：seed 的「亂數來源」 | 第 2 層：seed → 金鑰的「展開數學」 |
|---|---|---|
| 對應程式 | `Random800_90.cs` / `EntropyProvider.cs` | `Dilithium.cs` / `MLKEM.cs` |
| 該套哪個標準 | SP 800-90A/B/C（DRBG／熵源） | **FIPS 203 / 204** |
| 本 repo 合不合規 | ❌ **不是** 800-90A DRBG | ✅ **符合** FIPS 203/204 |
| 它需要合規嗎 | **不需要**（只產測試輸入，理由見 §3） | **需要，而且是重點** |
| 可不可驗證 | 可量測特性，無「通過/不通過」 | ✅ 可用 known-answer test 驗證 |

三句話總結：

1. **演算法層（FIPS 203/204）：符合。** 這是真正該合規的部分；本 repo 就是 NIST 官方參考實作（`github.com/usnistgov/ACVP-Server`），並以 NIST 官方 known-answer 向量自我驗證。
2. **seed 亂數層（SP 800-90A）：不是合規 DRBG，也不需要。** 它只負責產生「可重現的測試輸入」；對方 production 的真實 RBG 才受 800-90 規範，且由 ACVP 另一條獨立軌道（DRBG/Entropy validation）驗證。
3. **界線聲明：** 此 seed 機制僅用於產生測試向量，**不可作真實金鑰產生器**。

---

## 1. 為什麼要分兩層談（破除常見誤解）

常見誤解是：「seed 怎麼產生取決於對方機器，我們只是驗證機，所以討論 seed 沒意義。」

這在 ACVP 的 **KeyGen 測試**裡是反過來的：**seed 是「我們的 server（這個 repo）產生並下發給 IUT 的」**，不是對方產生的。

### 證據：seed 寫在 prompt 裡，由 server 下發

`PromptProjectionContractResolver` 決定 server 出題時把哪些欄位放進 `prompt.json`：

| 演算法 | 檔案 | 下發給 IUT 的測試案例欄位 |
|---|---|---|
| ML-DSA keyGen | `gen-val/src/generation/src/NIST.CVP.ACVTS.Libraries.Generation/ML-DSA/FIPS204/KeyGen/ContractResolvers/PromptProjectionContractResolver.cs` (L28–42) | `TestCaseId`, **`Seed`** |
| ML-KEM keyGen | `gen-val/src/generation/src/NIST.CVP.ACVTS.Libraries.Generation/ML-KEM/FIPS203/KeyGen/ContractResolvers/PromptProjectionContractResolver.cs` | `TestCaseId`, **`SeedZ`**, **`SeedD`** |

### 完整資料流

```
Generate 階段（我們的 server 做）：
  1. server 自己亂數產生 seed         ← Random800_90 那條鏈（見 SEED_REPORT.md）
  2. seed 寫進 prompt.json 下發給 IUT
  3. server 自己也用同一個 seed 算出標準金鑰，存進 expectedResults.json

對方 IUT 做的事：
  4. 讀 prompt 裡「我們給的 seed」→ 用自家模組從這個 seed 推導金鑰
  5. 把金鑰交回 responses.json

Validate 階段（我們的 server 做）：
  6. 比對「IUT 用我們的 seed 算出的金鑰」 == 「我們用同一 seed 算出的金鑰」？
```

### seed 的意義：把隨機演算法變成可驗證的考題

KeyGen 本質是**隨機**的——每次產的金鑰都不同。若讓對方用自己的隨機 seed，我們**永遠無法批改**（不知道正解、每次都不同）。ACVP 的解法是「**把隨機性釘死**」：由 server 指定 seed，逼雙方從**同一個 seed** 出發，金鑰因此變成**確定性、可比對**。

> **seed 是把「隨機演算法」變成「可驗證考題」的那把鑰匙。** 沒有 server 產生並下發 seed，整個 KeyGen 驗證就不成立。這也是為什麼 seed 是驗證機（我們）的職責，而非對方。

---

## 2. 兩種 seed 不能混為一談

| | 真實 production 的 seed | ACVP 測試的 seed |
|---|---|---|
| 誰產生 | 對方自家的 RBG/DRBG（他們的機器） | **我們的 server（這個 repo）** |
| 目的 | 產生真正要用的金鑰 | 產生可被批改的考題輸入 |
| 該套標準 | SP 800-90A/B（合規 DRBG） | 無（只需可重現、被記錄） |
| ACVP 有沒有測它 | ❌ keyGen 不直接測（RBG 另由 DRBG/800-90B 軌道驗證） | ✅ 就是測「給定 seed，金鑰展開對不對」 |

對方「真實怎麼產 seed」確實是他們的事——但 ML-KEM/ML-DSA 的 **keyGen 故意不測那個**，它測的是「給定 seed，你的金鑰展開數學對不對」。RBG 品質是另一條獨立的驗證軌（800-90A/B），不在 203/204 keyGen 範圍。

---

## 3. 什麼是「合規 DRBG」

**DRBG = Deterministic Random Bit Generator（確定性隨機位元產生器）**，是現代密碼系統實際拿來產生金鑰、nonce、seed 的標準亂數元件。「合規 DRBG」指**符合 NIST SP 800-90 系列**的那種。

### 設計核心：少量真熵 + 確定性展開

```
   真實熵源 (entropy source)          DRBG 演算法 (確定性)
   ┌──────────────┐                 ┌──────────────────┐
   │ 硬體雜訊/OS   │ ── 高熵種子 ──▶ │ 用 AES/SHA 把種子 │ ──▶ 大量隨機 bytes
   │ (不可預測)    │   (entropy)     │ 展開成隨機序列     │
   └──────────────┘                 └──────────────────┘
```

- **熵（entropy）** 只需少量（例如 256 bits），來自真正不可預測的物理來源。
- **DRBG** 是確定性演算法（用 AES 或 SHA 等核可原語），把高熵種子「安全地拉長」成任意長度隨機位元。
- 同種子 → 同輸出（故稱確定性）；安全性來自「沒人能從輸出反推種子或預測下一個輸出」。

### NIST SP 800-90 系列

| 標準 | 管什麼 |
|------|--------|
| **SP 800-90A** | DRBG 演算法本身：核可 **Hash_DRBG、HMAC_DRBG、CTR_DRBG** 三種機制 |
| **SP 800-90B** | 熵源：種子隨機性夠不夠、有沒有健康測試 |
| **SP 800-90C** | 如何把熵源 + DRBG 正確組裝 |

### 一個合規 DRBG 至少要具備

1. **核可的密碼原語**：AES-CTR 或 SHA-2/HMAC，**不是**一般 PRNG。
2. **明確生命週期函數**：`Instantiate`（足量熵播種）、`Reseed`（補新熵）、`Generate`、`Uninstantiate`。
3. **足量熵播種**：要 256-bit 安全強度，種子就得有 256 bits 真實熵——不是 32 bits。
4. **可預測性保護**：Backtracking resistance（狀態洩漏也推不出過去輸出）、Prediction resistance（推不出未來）。
5. **健康測試（800-90B）**：對熵源做開機/連續測試。
6. **可被 CAVP 驗證**：送 NIST CAVP 跑 known-answer test，取得驗證證書才算「合規」。

---

## 4. 為什麼 `Random800_90` 不是合規 DRBG（但沒問題）

雖名為 `Random800_90`，實際實作於
`gen-val/src/common/src/NIST.CVP.ACVTS.Libraries.Math/Random800_90.cs`：

```csharp
private static readonly RNGCryptoServiceProvider Global = new();   // L12  CSPRNG
[ThreadStatic] private static Random _local;                       // L14
...
Global.GetBytes(buffer);                       // L29  從 CSPRNG 只取 4 bytes = 32 bits
_local = new Random(BitConverter.ToInt32(buffer, 0));  // L30  拿 32 bits 當 System.Random 種子
...
Randy.NextBytes(randomBytes);                  // L47  真正的 bytes 來自 System.Random
```

### 逐項對照

| 合規要求 | `Random800_90` 實際 |
|---------|--------------------|
| 用 AES/SHA 等核可原語 | ❌ 用 `System.Random`（線性同餘類 PRNG） |
| 256-bit 熵播種 | ❌ 只有 **32-bit** 熵上限 |
| Instantiate/Reseed/Generate 生命週期 | ❌ 沒有 |
| Backtracking / Prediction resistance | ❌ `System.Random` 可反推、可預測 |
| 800-90B 熵源健康測試 | ❌ 沒有 |
| CAVP 驗證 | ❌ 沒有（也不需要，因為只產測試向量）|

### ⚠️ 重點結論

- 最終隨機 bytes 由 **`System.Random`（非密碼學等級 PRNG）** 產生；`RNGCryptoServiceProvider` 只負責替它挑一個 32-bit 初始種子。整體熵上限約 **32 bits**。
- 它「結構上就不是 DRBG」，只是名字取得有誤導性的測試用亂數器。
- **這對 ACVP 測試向量產生沒問題**：目的只是產生可分散、可重現的測試輸入來驗證 IUT。
- **但不可拿此機制當真實金鑰產生器**——真實場景必須使用 SP 800-90A 合規 DRBG 或 OS CSPRNG。

---

## 5. 哪一層「符合」、用什麼證明

真正該符合 NIST 標準的是**第 2 層：seed → 金鑰的展開數學**（FIPS 203/204）。本 repo 用 **known-answer test（KAT）** 證明它符合官方向量。

### 證據檔案

| 測試 | 檔案 / 函式 | 驗什麼 |
|---|---|---|
| ML-DSA KeyGen KAT | `gen-val/src/crypto/test/NIST.CVP.ACVTS.Libraries.Crypto.Dilithium.Tests/DilithiumTests.cs` → `ShouldGenerateKeyCorrectly` (L104) | 餵固定 `seedHex`，斷言算出的 `pk`/`sk` == 寫死的 NIST 官方答案 |
| ML-DSA SigGen KAT | 同檔 `ShouldExerciseAlgorithm` (L33) | 固定 seed+message → 斷言 signature 相符 |
| ML-DSA SigVer KAT | 同檔 `ShouldVerifySignaturesCorrectly` (L202) | 固定 (pk,msg,sig) → 斷言 verify 結果相符 |
| ML-KEM KAT | `gen-val/src/crypto/test/NIST.CVP.ACVTS.Libraries.Crypto.MLKEM.Tests/MlkemTests.cs` | 同理驗 ek/dk/封裝解封裝 |

這些測試裡的「期望值」是**寫死的 NIST 官方 known-answer 向量**——只要測試通過，即代表本 repo 的金鑰展開數學逐位元符合 FIPS 203/204。

---

## 6. 如何驗證（具體可執行）

### 6.1 驗第 2 層（該合規的，有 pass/fail）

```bash
cd ~/Project/ACVP-Server
# 跑 crypto 層 known-answer test —— 通過＝seed→金鑰數學符合 NIST 官方向量
dotnet test gen-val/src/crypto/test/NIST.CVP.ACVTS.Libraries.Crypto.Dilithium.Tests
dotnet test gen-val/src/crypto/test/NIST.CVP.ACVTS.Libraries.Crypto.MLKEM.Tests
```

加碼：把測試裡寫死的某一組 `(seed → pk/sk)` 拿去跟 **NIST 官方公布的 FIPS 203/204 KAT 檔**比對，證明那些寫死答案真的來自 NIST，而非 repo 自說自話。

### 6.2 量測第 1 層（沒有 pass/fail，但能客觀描述特性）

- **seed 碰撞測試**：用 server 連續產生大量 keyGen 測試向量，蒐集所有 seed，檢查是否重複、bit 分佈是否均勻 → 客觀呈現 `System.Random` ~32-bit 熵上限在生成大量向量時的碰撞風險。
- **程式碼佐證**：用 `Random800_90.cs` 指出它缺少 800-90A 的 Instantiate/Reseed/Generate 與 800-90B 健康測試 → 客觀說明「結構上就不是 DRBG」。

---

## 7. 一句話回老師

> 「ML-KEM/ML-DSA 的 KeyGen 測試中，seed 由我們的 ACVP server 產生並寫進 prompt 下發給 IUT，目的是把隨機的金鑰生成釘成確定性、可批改的考題。真正該符合 NIST 標準的是『seed → 金鑰的展開數學』，它符合 FIPS 203/204，並以 NIST 官方 known-answer 向量驗證通過；而產生 seed 的亂數器（`Random800_90`，底層是 `System.Random`、熵上限約 32 bits）**不是**合規的 SP 800-90A DRBG，但這沒問題——它只產測試輸入，不是真金鑰；真實金鑰的 DRBG 是對方 production 模組的責任，由 ACVP 另一條獨立軌道驗證。」
</content>
</invoke>
