# FIPS 203 / 204 演算法導讀(Code Review 用）

對象檔案:

| 演算法 | 標準 | 核心檔案 | 行數 |
|--------|------|----------|------|
| **ML-KEM** | FIPS 203 | `gen-val/src/crypto/.../Crypto/MLKEM/MLKEM.cs` | 913 |
| **ML-DSA** | FIPS 204 | `gen-val/src/crypto/.../Crypto/Dilithium/Dilithium.cs` | 1457 |

> ML-DSA 的核心類別沿用標準化前的舊名 **Dilithium**,對外行為即 FIPS 204 的 ML-DSA。

兩支檔案的共通設計:**一個多項式 = `int[256]`**(係數陣列),向量 = `int[][]`,矩陣 = `int[][][]`;所有模運算走 `PosMod`(回非負餘數)/ `PlusMinusMod`(回對稱餘數);雜湊一律透過注入的 `IShaFactory`(SHA3 / SHAKE 包裝),核心不自己實作雜湊。原始碼裡每個方法的 XML 註解都已標好對應的 **FIPS Algorithm 編號**,review 時可直接對照標準文件。

---

## 一、ML-KEM(FIPS 203)— `MLKEM.cs`

### 1. 物件結構

- 建構子注入 4 個雜湊實例:`SHAKE128`、`SHAKE256`、`SHA3-256`、`SHA3-512`(行 50–57)。
- `Param`(`MLKEMParameters`)依參數集帶入常數:`N=256`、`Q=3329`,以及隨等級變動的 `K`(2/3/4)、`Eta1`、`Du`、`Dv` 與三種長度(見 `MLKEMParameters.cs`)。
- `_zeta[]`(行 20–48)是 **預先算好的 NTT 旋轉因子表**(256 個),搭配 bit-reversal 索引使用。

### 2. 對外入口(`IMLKEM`)— 對應 FIPS 「ML-KEM.*」外層

| 介面方法 | FIPS 對應 | 程式位置 | 重點 |
|----------|-----------|----------|------|
| `GenerateKey(z, d)` | ML-KEM.KeyGen | 行 65–79 | 呼叫內層 `K_Pke_KeyGen(d)`,再把 `dk = dk_pke ‖ ek_pke ‖ H(ek_pke) ‖ z` 串接 |
| `Encapsulate(ek, m)` | ML-KEM.Encaps | 行 130–147 | `(K, r) = G(m ‖ H(ek))`,再 `c = K-PKE.Encrypt(ek, m, r)` |
| `Decapsulate(dk, c)` | ML-KEM.Decaps | 行 155–206 | 解出 `m'`→重算 `(K', r')`→**重新加密 `c'`**,`c≠c'` 時回退 `K̄ = J(z‖c)`(隱式拒絕) |

**Review 重點 ①(隱式拒絕,FIPS 203 的安全關鍵)**:`Decapsulate` 行 193–199 用 `c.SequenceEqual(cPrime)` 比對密文,失敗則把回傳值換成 `KBar` 並回傳 `implicitRejection=true`。這是 Fujisaki–Okamoto 轉換的核心 —— 解封裝**永遠回傳一把金鑰**,不對外洩漏成功/失敗,以抵抗選擇密文攻擊。

### 3. 鍵的合法性檢查(ACVP keyCheck 用)

- `EncapsulationKeyCheck`(行 81–99):長度檢查 + **模數約化檢查**——對 `t̂` 做 `ByteEncode(12, ByteDecode(12, …))` 來回一趟,確認每個係數都 `< q`。
- `DecapsulationKeyCheck`(行 101–112):確認 `dk` 內嵌的 `H(ek_pke)` 與重算值相符。

### 4. 內層 K-PKE(真正的格密碼運算)

| 方法 | FIPS Algorithm | 位置 | 說明 |
|------|----------------|------|------|
| `K_Pke_KeyGen` | Alg 12 | 行 603–672 | `(ρ,σ)=G(d‖K)`→展開矩陣 `Â`→抽 `s,e`→`t̂ = Â∘ŝ + ê` |
| `K_Pke_Encrypt` | Alg 13 | 行 674–753 | 抽 `y,e1,e2`→`u = NTT⁻¹(Âᵀ∘ŷ)+e1`、`v = NTT⁻¹(t̂ᵀ∘ŷ)+e2+μ`→壓縮編碼成 `c` |
| `K_Pke_Decrypt` | Alg 14 | 行 755–787 | `w = v − NTT⁻¹(ŝᵀ∘NTT(u))`→`Compress(1,·)` 還原訊息 |

### 5. 取樣 / 編碼 / NTT 基礎元件

| 方法 | FIPS Algorithm | 位置 |
|------|----------------|------|
| `BitsToBytes` / `BytesToBits` | Alg 2 / 3 | 行 280 / 350 |
| `ByteEncode` / `ByteDecode` | Alg 4 / 5 | 行 383 / 406 |
| `SampleNTT`(均勻拒絕取樣 `Â`) | Alg 6 | 行 431–474 |
| `SamplePolyCBD`(中心二項分布抽 `s,e`) | Alg 7 | 行 482–502 |
| `NTT` / `NTTInverse` | Alg 8 / 9 | 行 509 / 538 |
| `MultiplyNTTs` / `BaseCaseMultiply` | Alg 10 / 11 | 行 569 / 590 |
| `Compress` / `Decompress` | §4.2.1 公式 | 行 367 / 372 |

**Review 重點 ②(整數溢位防護)**:`BaseCaseMultiply`(行 590–596)刻意用 `long` 接收參數,註解說明 `3300³` 會超出 32-bit int。這類「在哪裡升位 `long`」是格密碼最常見的正確性陷阱,值得逐一確認(ML-DSA 的 NTT 也有同樣處理)。

**Review 重點 ③(SampleNTT 的 squeeze 補抽)**:行 444–471,當 168-byte 緩衝用盡時 `squeezeFactor++` 重新擠出更多 SHAKE128 輸出,確保拒絕取樣不會中斷。

---

## 二、ML-DSA(FIPS 204)— `Dilithium.cs`

### 1. 物件結構

- 繼承 `ExternalSignatureBase`(提供 context / pre-hash / 外部簽章包裝,見下節),並實作 `IMLDSA`。
- 注入 `SHAKE256`(`_h`)、`SHAKE128`(`_h128`)與選用的 `IEntropyProvider`(僅非確定性簽章需要)。
- `_param`(`DilithiumParameters`)帶 `Q=8380417`、`D`、`Tau`、`Gamma1`、`Gamma2`、`Eta`、`Beta`、`Omega`、`Lambda`、`K`、`L` 等。
- `_zeta[]`(行 24–48)同樣是預算旋轉因子(已套 bit-reversal,含負值)。

### 2. 對外入口(`IMLDSA` / 內部介面)

| 方法 | FIPS 對應 | 位置 | 重點 |
|------|-----------|------|------|
| `GenerateKey(seed)` | ML-DSA.KeyGen | 行 74–146 | `H(seed‖K‖L)` 切出 `ρ,ρ',K`→`Â=ExpandA`、`(s1,s2)=ExpandS`→`t=NTT⁻¹(Â∘ŝ1)+s2`→`Power2Round` 拆 `t1,t0`→編碼 `pk,sk` |
| `Sign(sk, m, rnd)` | ML-DSA.Sign(internal) | 行 156–300 | **拒絕取樣迴圈**(見重點④) |
| `SignExternalMu(sk, mu, rnd)` | externalMu 變體 | 行 310–395 | 同上,但 `μ` 由外部直接提供(略過 `tr‖m` 雜湊) |
| `Verify(pk, m, sig)` | ML-DSA.Verify(internal) | 行 404–514 | 重算 `w1'`→比對 `c̃' == c̃` |

### 3. 外部包裝層 — `ExternalSignatureBase`

FIPS 204 的「對外」簽章(帶 context、可選 pre-hash)在基底類別 `ExternalSignatureBase.cs`,把訊息預處理成 `M'` 後再呼叫 `Sign`/`Verify`:

- `ExternalSign`:`M' = 0 ‖ len(ctx) ‖ ctx ‖ M`
- `ExternalPreHashSign`:`M' = 1 ‖ len(ctx) ‖ ctx ‖ OID ‖ PH(M)`(HashML-DSA)
- context 長度 > 255 直接拋例外 / 驗證回 false。

> 對應到 web-tool 的 5 個模式:keyGen=`GenerateKey`、sigGen=`Sign`/`ExternalSign`、sigVer=`Verify`/`ExternalVerify`。

### 4. 簽章拒絕取樣迴圈(FIPS 204 的核心)

**Review 重點 ④**:`Sign`(行 200–293)是一個 `do…while` 迴圈,每輪:

1. `y = ExpandMask(ρ', κ)`→`w = NTT⁻¹(Â∘ŷ)`→取高位 `w1`→`c̃ = H(μ ‖ W1Encode(w1))`。
2. `c = SampleInBall(c̃)`→算 `z = y + c·s1`、`r0 = LowBits(w − c·s2)`。
3. **三道拒絕門檻**(任何一條不過就丟掉重來,`κ += L`):
   - `‖z‖∞ ≥ γ1 − β`(行 241)
   - `‖r0‖∞ ≥ γ2 − β`(行 241)
   - `‖c·t0‖∞ ≥ γ2` 或 hint 數 `> ω`(行 282)
4. 通過才用 `MakeHint` 建提示向量 `h`,`SigEncode(c̃, z, h)` 輸出簽章。

這個迴圈確保簽章不洩漏私鑰且大小有界,是 review 時最該逐行對照標準的地方。`Verify` 端對應做 `‖z‖∞`、hint 數量上限檢查,再用 `UseHint` 重建 `w1'`、比對挑戰雜湊。

### 5. 編碼 / 取樣 / 數學元件(註解皆標 Algorithm 編號)

| 群組 | 方法 (FIPS Alg) | 位置 |
|------|------|------|
| 位元轉換 | `IntegerToBits` (9) / `BitsToInteger` (10) / `IntegerToBytes` (11) / `BitsToBytes` (12) / `BytesToBits` (13) | 行 522–606 |
| 係數取樣 | `CoeffFromThreeBytes` (14) / `CoeffFromHalfByte` (15) | 行 615 / 632 |
| 打包 | `SimpleBitPack` (16) / `BitPack` (17) / `SimpleBitUnpack` (18) / `BitUnpack` (19) | 行 653–721 |
| Hint 打包 | `HintBitPack` (20) / `HintBitUnpack` (21) | 行 728 / 754 |
| 鍵/簽章編碼 | `PkEncode`(22)/`PkDecode`(23)/`SkEncode`(24)/`SkDecode`(25)/`SigEncode`(26)/`SigDecode`(27)/`W1Encode`(28) | 行 800–978 |
| 取樣器 | `SampleInBall`(29)/`RejNTTPoly`(30)/`RejBoundedPoly`(31)/`ExpandA`(32)/`ExpandS`(33)/`ExpandMask`(34) | 行 985–1174 |
| 捨入/提示 | `Power2Round`(35)/`Decompose`(36)/`HighBits`(37)/`LowBits`(38)/`MakeHint`(39)/`UseHint`(40) | 行 1181–1272 |
| NTT | `NTT`(41)/`NTTInverse`(42) | 行 1279 / 1314 |

**Review 重點 ⑤(`HintBitUnpack` 的健全性檢查)**:行 754–792 在解碼提示時做了嚴格驗證——索引必須遞增、超出 `ω` 或尾端非零一律回 `null`(導致 `Verify` 直接判否)。這是抵抗惡意簽章(malformed signature)的防線,對應 web-tool 的「上傳惡意檔案」場景,值得展示。

**Review 重點 ⑥(NTT 溢位處理)**:`NTT`/`NTTInverse`/`MatrixMultiply`(行 1295、1333、1391)在乘法時升位 `(long)`——`Q≈2²³`,兩數相乘逼近 2⁴⁶,必須用 64-bit 暫存再 `PosMod` 回 int。

---

## 三、Code Review 共通觀察點(可當作展示提綱)

1. **與標準逐行對齊**:每個方法都標了 FIPS Algorithm 編號,review 時把程式碼與 FIPS 203/204 偽碼並排即可逐條核對。
2. **正確性已被 KAT 背書**:`MlkemTests.cs`(112 passed)、`DilithiumTests.cs`(200 passed)內嵌官方已知答案向量,演算法輸出逐位元比對通過。
3. **安全關鍵點**:ML-KEM 的隱式拒絕(重點①)、ML-DSA 的拒絕取樣三門檻(重點④)與惡意簽章檢查(重點⑤)是兩個演算法最該被審視的安全邏輯。
4. **數值陷阱**:格密碼最常見 bug 是整數溢位,本實作在所有逼近 `Q²` 的乘法都顯式升位 `long`(重點②⑥)。
5. **可改善處(非正確性)**:大量被註解掉的 `Console.WriteLine` 中間值輸出(debug 殘留)可清掉;`ByteDecode`/`BytesToBits` 等會就地破壞輸入陣列(`z[i] /= 2`),XML 註解已標 "NOTE: wipes out",但仍是潛在踩雷點,review 時可建議改為不可變寫法。

---

## 四、建議的展示順序

1. 開 `MLKEMParameters.cs` / `DilithiumParameters.cs` —— 先講參數集差異(等級 vs 安全強度)。
2. `MLKEM.cs` 三個入口 `GenerateKey/Encapsulate/Decapsulate` —— 點出隱式拒絕。
3. `Dilithium.cs` 的 `Sign` 拒絕取樣迴圈 —— ML-DSA 最有戲的部分。
4. 跑兩支 KAT 測試現場展示「全綠」:
   ```bash
   dotnet test gen-val/src/crypto/test/NIST.CVP.ACVTS.Libraries.Crypto.MLKEM.Tests
   dotnet test gen-val/src/crypto/test/NIST.CVP.ACVTS.Libraries.Crypto.Dilithium.Tests
   ```
