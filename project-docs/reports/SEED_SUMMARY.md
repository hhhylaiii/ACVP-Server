# ACVP Seed 報告摘要（ML-DSA / ML-KEM KeyGen）

---

## 1. Seed 的完整資料流

```
Generate 階段（我們的 ACVP server 做）
  ① server 亂數產生 seed ───────────► Random800_90 → System.Random（種子取自 CSPRNG 的 32 bits）
  ② seed 寫進 prompt.json 下發給 IUT
  ③ server 自己也用同一 seed 算出標準金鑰，存進 expectedResults.json

IUT 做的事
  ④ 讀 prompt 裡「server 給的 seed」→ 用自家模組從 seed 推導金鑰
  ⑤ 把金鑰交回 responses.json

Validate 階段（我們的 server 做）
  ⑥ 比對「IUT 用該 seed 算出的金鑰」 == 「server 用同一 seed 算出的金鑰」
```

> 重點：KeyGen 本是隨機的，每次金鑰都不同。ACVP 的做法是**由 server 指定 seed**，把隨機演算法
> 釘成「確定性、可批改的考題」。所以 seed 是驗證機（我們）的職責，不是 IUT 產生的。

---

## 2. Seed 從哪裡來：逐層呼叫鏈 + 檔案路徑

由上往下呼叫：

```
Orleans Grain（server 端，Generate 模式）
        │  _entropyProvider.GetEntropy(256)
        ▼
EntropyProvider.GetEntropy(n)            ← 轉發
        │  _random.GetRandomBitString(n)
        ▼
Random800_90.GetRandomBitString(n)       ← 實際產生隨機 bytes（底層 System.Random）
        │  seed (256-bit)
        ▼
Dilithium.GenerateKey(seed) / MLKEM.GenerateKey(z, d)   ← 用 SHAKE/SHA3 展開成金鑰
```

| 呼叫層 | 檔案路徑 | 行號 |
|--------|----------|------|
| 1. ML-DSA seed 來源 | `gen-val/src/orleans/src/NIST.CVP.ACVTS.Libraries.Orleans.Grains/Pqc/OracleObserverMLDSAKeyCaseGrain.cs` | L42 `GetEntropy(256).Bits` |
| 2. ML-KEM seed 來源 | `gen-val/src/orleans/src/NIST.CVP.ACVTS.Libraries.Orleans.Grains/Pqc/OracleObserverMLKEMEncapKeyCheckCaseGrain.cs` | L45–46 `GetEntropy(256).Bits` |
| 3. Entropy 轉發層 | `gen-val/src/common/src/NIST.CVP.ACVTS.Libraries.Math/Entropy/EntropyProvider.cs` | L14–16 `GetEntropy` → `GetRandomBitString` |
| 4. 實際亂數產生器 | `gen-val/src/common/src/NIST.CVP.ACVTS.Libraries.Math/Random800_90.cs` | L12, L30–31, L47 |
| 5. ML-DSA 金鑰展開 | `gen-val/src/crypto/src/NIST.CVP.ACVTS.Libraries.Crypto/Dilithium/Dilithium.cs` | L74 `GenerateKey(BitArray seed)` |
| 6. ML-KEM 金鑰展開 | `gen-val/src/crypto/src/NIST.CVP.ACVTS.Libraries.Crypto/MLKEM/MLKEM.cs` | L65 `GenerateKey(byte[] z, byte[] d)` |

---

## 3. 兩層的解釋與「需符合的標準」

「合規」要分兩層看——真正該符合標準的是第 2 層（演算法數學），不是第 1 層（產測試 seed 的亂數）。

| | 第 1 層：seed 的「亂數來源」 | 第 2 層：seed → 金鑰的「展開數學」 |
|---|---|---|
| 對應程式 | `Random800_90.cs` / `EntropyProvider.cs` | `Dilithium.cs` / `MLKEM.cs` |
| 該套哪個標準 | SP 800-90A/B/C（DRBG／熵源） | **FIPS 203 / 204** |
| 本 repo 合不合規 | ❌ **不是** 800-90A DRBG | ✅ **符合** FIPS 203/204 |
| 它需要合規嗎 | **不需要**（只產測試輸入） | **需要，而且是重點** |
| 可不可驗證 | 可量測特性，無「通過/不通過」 | ✅ 可用 known-answer test 驗證 |

---

## 4. 為什麼那 32-bit 亂數不需要符合 SP 800-90A DRBG

`Random800_90.cs` 名字雖是取名800-90，實際只用 CSPRNG 取 **32-bit 種子**餵給非密碼學的 `System.Random`：

```csharp
Global.GetBytes(buffer);                              // L29  從 CSPRNG 只取 4 bytes = 32 bits
_local = new Random(BitConverter.ToInt32(buffer, 0)); // L30  拿去 seed System.Random
Randy.NextBytes(randomBytes);                         // L47  真正的 bytes 來自 System.Random
```

它**不需要**是合規 DRBG，原因有三：

1. **角色不同**：本 repo 是「出題的測試系統」，不是「被驗證的密碼模組（IUT）」。SP 800-90A 約束的是 IUT，不是測試工具。
2. **產出是公開測試資料**：這些 seed 會以明文寫進 `prompt.json` 下發，不保護任何祕密。DRBG 存在的目的（保護祕密的不可預測性）在這裡不存在。
3. **正確性由數學保證**：標準答案由 server 用正確演算法算出，跟 seed 的亂數品質無關；廠商沒有正確實作就答不出來。

> 一句話：合規 DRBG 的價值在「保護祕密的不可預測性」。這裡**既沒祕密、輸出又公開、正確性又由數學保證**，
> 所以強加合規 DRBG 沒有意義。**但此機制僅供測試向量產生，不可當真實金鑰產生器。**

---

## 5. 直接跑測試給老師看（驗第 2 層：seed → 金鑰展開數學）

### 5-0. 先啟動 Orleans Silo（Demo 時的 server 端）

第 1～2 點的 Generate／Validate 流程是由 Orleans Grain 執行，Demo 前先把 Silo 起起來。
另開一個終端機，背景常駐執行（看到 `Silo started` 即代表就緒）：

```bash
cd ~/Project/ACVP-Server

# 啟動 Orleans Silo Host（承載所有 Grain，含 seed 產生與金鑰展開）
dotnet run --project gen-val/samples/NIST.CVP.ACVTS.Orleans.ServerHost
```

> 註：下面第 5-1 的 crypto known-answer test 是純單元測試，**不需要** Orleans 也能跑；
> 啟動 Silo 是為了 Demo 第 1～2 點完整的 Generate／Validate 出題與批改流程。

### 5-1. 跑 known-answer test

第 2 層才有明確的「通過 / 不通過」。以下 known-answer test 通過，即代表金鑰展開逐位元符合 FIPS 203/204 官方向量：

```bash
cd ~/Project/ACVP-Server

# ML-DSA（FIPS 204）— 預期：200 通過、3 略過（略過的是壓力測試）
dotnet test gen-val/src/crypto/test/NIST.CVP.ACVTS.Libraries.Crypto.Dilithium.Tests

# ML-KEM（FIPS 203）— 預期：112 通過、0 略過
dotnet test gen-val/src/crypto/test/NIST.CVP.ACVTS.Libraries.Crypto.MLKEM.Tests
```

這些測試的「期望值」是**寫死的 NIST 官方 known-answer 向量**——通過即證明本 repo 的 seed→金鑰展開符合標準。
