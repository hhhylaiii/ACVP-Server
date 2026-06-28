# ACVP Demo — Seed 生成報告（ML-DSA / ML-KEM KeyGen）

> 本報告說明 demo 用的 ACVP server（Orleans）在 **Generate 階段**如何產生 KeyGen 的隨機 seed，
> 包含程式碼位置、呼叫鏈、以及 seed 如何透過數學式展開成金鑰。
>
> 注意：`fips-203-204-demo/iut_*.py` 這些 IUT 腳本**不產生 seed**，它們只是把 `expectedResults.json`
> 的答案抄進 `responses.json`。真正的 seed 一律由 server 端產生。

---

## 1. 總覽：Seed 從哪裡來

```
Orleans Grain (server 端，Generate 模式)
        │  _entropyProvider.GetEntropy(256)
        ▼
EntropyProvider.GetEntropy(n)                     ← 轉發
        │  _random.GetRandomBitString(n)
        ▼
Random800_90.GetRandomBitString(n)                ← 實際產生隨機 bytes
        │  seed (256-bit)
        ▼
Dilithium.GenerateKey(seed)  /  MLKEM.GenerateKey(z, d)
        │  以 SHAKE / SHA3 展開
        ▼
(pk, sk)  /  (ek, dk)
```

---

## 2. 程式碼位置（檔案與行號）

| 角色 | 檔案 | 關鍵行 |
|------|------|--------|
| ML-DSA KeyGen seed 來源 | `gen-val/src/orleans/src/NIST.CVP.ACVTS.Libraries.Orleans.Grains/Pqc/OracleObserverMLDSAKeyCaseGrain.cs` | L42 `var seed = _entropyProvider.GetEntropy(256).Bits;` |
| ML-KEM KeyGen seed 來源 | `gen-val/src/orleans/src/NIST.CVP.ACVTS.Libraries.Orleans.Grains/Pqc/OracleObserverMLKEMEncapKeyCheckCaseGrain.cs` | L45–46 `seedZ` / `seedD = GetEntropy(256)` |
| Entropy 轉發層 | `gen-val/src/common/src/NIST.CVP.ACVTS.Libraries.Math/Entropy/EntropyProvider.cs` | `GetEntropy()` → `_random.GetRandomBitString()` |
| 實際亂數產生器 | `gen-val/src/common/src/NIST.CVP.ACVTS.Libraries.Math/Random800_90.cs` | L12, L30–31, L47 |
| ML-DSA 金鑰展開數學 | `gen-val/src/crypto/src/NIST.CVP.ACVTS.Libraries.Crypto/Dilithium/Dilithium.cs` | `GenerateKey(BitArray seed)` L74 |
| ML-KEM 金鑰展開數學 | `gen-val/src/crypto/src/NIST.CVP.ACVTS.Libraries.Crypto/MLKEM/MLKEM.cs` | `GenerateKey(z, d)` L65、`K_Pke_KeyGen(d)` L603 |

---

## 3. 亂數產生器內部（重要）

`Random800_90.cs`（雖名為 800-90，實作細節如下）：

```csharp
private static readonly RNGCryptoServiceProvider Global = new();   // L12  密碼學級 CSPRNG
...
var buffer = new byte[4];
Global.GetBytes(buffer);                                           // 取 4 bytes = 32 bits
_local = new Random(BitConverter.ToInt32(buffer, 0));             // L30  用 32-bit 當 System.Random 種子
...
Randy.NextBytes(randomBytes);                                      // L47  實際隨機 bytes 來自 System.Random
```

### ⚠️ 重點結論
- 最終的隨機 bytes 是由 **`System.Random`（非密碼學等級 PRNG）** 產生。
- `RNGCryptoServiceProvider`（CSPRNG）**只負責替 `System.Random` 挑一個 32-bit 初始種子**，
  不是直接產生 seed。
- 因此整體熵上限約為 **32 bits**（每執行緒一個 `System.Random` 實例，`[ThreadStatic]`）。

**這對 demo / ACVP 測試向量產生沒問題**：目的只是產生可分散、可重現的測試資料來驗證受測實作（IUT）。
**但不可拿此機制當真實金鑰產生器**——真實場景必須使用 SP 800-90A 合規的 DRBG 或 OS CSPRNG 直接輸出。

---

## 4. ML-DSA (FIPS 204) — seed 如何展開成金鑰

`Dilithium.GenerateKey(seed)`，輸入為 256-bit 隨機 seed `ξ`（程式內變數 `seed`）。

### 步驟 4.1：用 SHAKE-256 展開 seed
```
H = SHAKE256
(ρ ‖ ρ′ ‖ K) = H( ξ ‖ IntegerToBytes(k,1) ‖ IntegerToBytes(ℓ,1) )   ，輸出 1024 bits = 128 bytes
```
切分（對應 code L85–87）：

$$
\rho = \text{seedMaterial}[0:32] \quad(256\text{ bits})
$$
$$
\rho' = \text{seedMaterial}[32:96] \quad(512\text{ bits})
$$
$$
K = \text{seedMaterial}[96:128] \quad(256\text{ bits})
$$

- `ρ`：用來展開公開矩陣 **Â**
- `ρ′`：用來抽樣秘密向量 **s₁, s₂**
- `K`：私鑰中的簽章用隨機種子

### 步驟 4.2：展開矩陣與秘密向量
$$
\hat{A} = \text{ExpandA}(\rho) \in R_q^{k \times \ell}
$$
$$
(s_1, s_2) = \text{ExpandS}(\rho') ,\quad s_1 \in R_q^{\ell},\ s_2 \in R_q^{k}
$$
（`s₁, s₂` 的係數落在 `[-η, η]`）

### 步驟 4.3：計算 t（核心 MLWE 關係式）
程式 L106：`t = NTTInverse(aHat * NTT(s1)) + s2`

$$
t = A \cdot s_1 + s_2 = \text{NTT}^{-1}\!\big(\hat{A} \circ \text{NTT}(s_1)\big) + s_2
$$

其中 `∘` 為 NTT 域逐元素乘法。所有運算在環

$$
R_q = \mathbb{Z}_q[x]/(x^{256}+1),\quad q = 8380417 = 2^{23} - 2^{13} + 1
$$

### 步驟 4.4：Power2Round 拆解 t
對 `t` 每個係數做（`d = 13`）：
$$
(t_1, t_0) = \text{Power2Round}(t, d):\quad
t_1 = \Big\lfloor \tfrac{t - t_0}{2^d} \Big\rceil,\quad
t_0 = t \bmod^{\pm} 2^{d}
$$

### 步驟 4.5：編碼輸出
$$
pk = \text{pkEncode}(\rho,\, t_1)
$$
$$
tr = \text{SHAKE256}(pk,\ 512\text{ bits}) \quad(\text{64 bytes})
$$
$$
sk = \text{skEncode}(\rho,\, K,\, tr,\, s_1,\, s_2,\, t_0)
$$

### 參數集（k, ℓ）
| 參數集 | k | ℓ | η |
|--------|---|---|---|
| ML-DSA-44 | 4 | 4 | 2 |
| ML-DSA-65 | 6 | 5 | 4 |
| ML-DSA-87 | 8 | 7 | 2 |

---

## 5. ML-KEM (FIPS 203) — 兩個 seed 如何展開成金鑰

`MLKEM.GenerateKey(z, d)`，輸入兩個 256-bit 隨機 seed：
- `d`：餵給 K-PKE.KeyGen 的種子
- `z`：私鑰用的隱式拒絕（implicit rejection）種子

### 步驟 5.1：K-PKE.KeyGen — 從 d 展開
程式 L605：`(rho, sigma) = G(d ‖ IntegerToBytes(k,1))`

$$
(\rho, \sigma) = G\big(d \,\|\, \text{IntegerToBytes}(k,1)\big),\quad G = \text{SHA3-512}
$$
（512-bit 輸出切成兩半，各 256 bits）

### 步驟 5.2：展開矩陣 Â（用 ρ）
$$
\hat{A}[i][j] = \text{SampleNTT}(\rho, j, i),\quad i,j \in \{0,\dots,k-1\}
$$

### 步驟 5.3：抽樣秘密向量 s、誤差向量 e（用 σ + PRF）
程式 L629 / L639，`n` 為遞增的 nonce：
$$
s[i] = \text{SamplePolyCBD}_{\eta_1}\big(\text{PRF}_{\eta_1}(\sigma, n)\big)
$$
$$
e[i] = \text{SamplePolyCBD}_{\eta_1}\big(\text{PRF}_{\eta_1}(\sigma, n)\big)
$$
其中 `PRF = SHAKE256`，CBD = Centered Binomial Distribution。

### 步驟 5.4：計算 t̂（核心 MLWE 關係式）
程式：`tHat = aHat * sHat + eHat`

$$
\hat{t} = \hat{A} \circ \hat{s} + \hat{e},\quad
\hat{s} = \text{NTT}(s),\ \hat{e} = \text{NTT}(e)
$$

環為

$$
R_q = \mathbb{Z}_q[x]/(x^{256}+1),\quad q = 3329
$$

### 步驟 5.5：編碼公鑰 / 私鑰
$$
ek_{pke} = \text{ByteEncode}_{12}(\hat{t}) \,\|\, \rho
$$
$$
dk_{pke} = \text{ByteEncode}_{12}(\hat{s})
$$

### 步驟 5.6：組裝最終 ML-KEM 金鑰（程式 L73）
$$
ek = ek_{pke}
$$
$$
dk = dk_{pke} \,\|\, ek_{pke} \,\|\, H(ek_{pke}) \,\|\, z,\quad H = \text{SHA3-256}
$$

`z` 在解封裝失敗時用於計算隱式拒絕的共享金鑰，因此被併入私鑰。

### 參數集
| 參數集 | k | η₁ | q |
|--------|---|----|----|
| ML-KEM-512 | 2 | 3 | 3329 |
| ML-KEM-768 | 3 | 2 | 3329 |
| ML-KEM-1024 | 4 | 2 | 3329 |

---

## 6. 一句話總結（demo 被問時可直接回答）

- **Seed 由 server 端 Orleans grain 產生**，呼叫 `EntropyProvider.GetEntropy(256)`，
  底層是 `Random800_90` → `System.Random`（種子取自 CSPRNG 的 32 bits）。
- **ML-DSA**：單一 256-bit seed `ξ` → 經 SHAKE256 展開為 `(ρ, ρ′, K)`，
  再由 MLWE 關係式 `t = A·s₁ + s₂` 算出金鑰。
- **ML-KEM**：兩個 256-bit seed `d, z` → `d` 經 SHA3-512 展開為 `(ρ, σ)`，
  由 MLWE 關係式 `t̂ = Â∘ŝ + ê` 算出金鑰，`z` 併入私鑰供隱式拒絕。
- 此 seed 機制**僅適用於測試向量產生，不可用於真實金鑰**。
