# FIPS 203 (ML-KEM) 與 FIPS 204 (ML-DSA) 演算法完整整理

> 來源:NIST FIPS 203 / FIPS 204(2024-08-13 正式版)。
> 本檔逐一列出兩個標準的**所有演算法**,附虛擬碼與計算說明。
> 記號慣例:`←` 賦值、`mod` 取餘、`⌊x⌋` 下取整、`⌈x⌋` 上取整、`⌈x⌋` 在 FIPS 中亦表「四捨五入到最近整數」(`.5` 進位),`‖` 串接、`∈` 屬於。
> NTT 表示(NTT domain)的物件加帽號,例:`Â`、`ŝ`、`t̂`。

---

## 0. 共同數學基礎

兩個標準都建立在多項式環與 NTT(數論變換)之上,但**參數不同**,務必分清。

### 0.1 環結構對照

| 項目 | ML-KEM (FIPS 203) | ML-DSA (FIPS 204) |
|---|---|---|
| n | 256 | 256 |
| q(質數) | 3329 = 2⁸·13 + 1 | 8380417 = 2²³ − 2¹³ + 1 |
| 環 R_q | ℤ_q[X]/(X²⁵⁶ + 1) | ℤ_q[X]/(X²⁵⁶ + 1) |
| 單位根 ζ | 17(256 次原根) | 1753(512 次原根) |
| NTT 像 T_q | 128 個二次擴張之直和(每點為一次多項式) | 256 個 ℤ_q 之直積(每點為純量) |
| NTT 點乘 | 兩兩配對做「二次模」乘法(BaseCaseMultiply) | 逐座標純量乘法 |

> 關鍵差異:ML-KEM 的 q ≡ 1 (mod 256) 但**非** ≡ 1 (mod 512),故 X²⁵⁶+1 只能分解到「二次因式」,NTT 後每點是一次多項式;ML-DSA 的 q ≡ 1 (mod 512),X²⁵⁶+1 可完全分解成 256 個一次因式,NTT 後每點是純量。這也是為何兩者的 NTT 乘法寫法不同。

### 0.2 共用的計算原則
- **禁用浮點數**:所有除法用整數除法 `⌊x/y⌋`,且 `⌈x/y⌉ = ⌊(x+y−1)/y⌋`。
- **小端序(little-endian)**:位元/位元組由低位排到高位。
- **雜湊/XOF**:皆出自 FIPS 202(SHA-3 家族)。

---

# Part I — FIPS 203:ML-KEM(金鑰封裝機制)

## I-1. 整體結構

ML-KEM = 「K-PKE 公鑰加密」 + 「Fujisaki–Okamoto(FO)轉換」,達到 IND-CCA2 安全。三個對外演算法:

```
ML-KEM.KeyGen()        → (ek, dk)              產生封裝金鑰 / 解封裝金鑰
ML-KEM.Encaps(ek)      → (K, c)                封裝:產生共享金鑰 K 與密文 c
ML-KEM.Decaps(dk, c)   → K'                     解封裝:由密文還原共享金鑰
```

底層 K-PKE **不可單獨使用**,只當作子程式。安全基礎:Module-LWE。

## I-2. 參數集

| 參數集 | n | q | k | η₁ | η₂ | dᵤ | dᵥ | 安全類別 | RBG 強度 |
|---|---|---|---|---|---|---|---|---|---|
| ML-KEM-512 | 256 | 3329 | 2 | 3 | 2 | 10 | 4 | 1 | 128 |
| ML-KEM-768 | 256 | 3329 | 3 | 2 | 2 | 10 | 4 | 3 | 192 |
| ML-KEM-1024| 256 | 3329 | 4 | 2 | 2 | 11 | 5 | 5 | 256 |

**大小(位元組)**

| 參數集 | ek | dk | 密文 c | 共享金鑰 K |
|---|---|---|---|---|
| ML-KEM-512 | 800 | 1632 | 768 | 32 |
| ML-KEM-768 | 1184| 2400 | 1088| 32 |
| ML-KEM-1024| 1568| 3168 | 1568| 32 |

> NIST 預設建議 **ML-KEM-768**。

## I-3. 密碼學函數(wrapper)

```
PRF_η(s, b)  := SHAKE256(s ‖ b, 8·64·η)            s∈𝔹³², b∈𝔹, 輸出 64η 位元組
H(s)         := SHA3-256(s)                         → 32 位元組
J(s)         := SHAKE256(s, 8·32)                   → 32 位元組(隱性拒絕用)
G(c)         := SHA3-512(c)                          → 兩段各 32 位元組 (a, b)
XOF          := SHAKE128 的 Init/Absorb/Squeeze 增量介面
```

## I-4. 輔助演算法

### Algorithm 3 — BitsToBytes(b)
位元陣列 → 位元組陣列(每 8 位元一個位元組,小端)。
```
輸入: b ∈ {0,1}^(8ℓ)         輸出: B ∈ 𝔹^ℓ
1: B ← (0,…,0)
2: for i ← 0 … 8ℓ−1:
3:     B[⌊i/8⌋] ← B[⌊i/8⌋] + b[i]·2^(i mod 8)
4: return B
```

### Algorithm 4 — BytesToBits(B)
位元組 → 位元(Algorithm 3 之逆)。
```
輸入: B ∈ 𝔹^ℓ            輸出: b ∈ {0,1}^(8ℓ)
1: C ← B
2: for i ← 0 … ℓ−1:
3:     for j ← 0 … 7:
4:         b[8i+j] ← C[i] mod 2
5:         C[i] ← ⌊C[i]/2⌋
6: return b
```

### 壓縮 / 解壓縮(Compress / Decompress)
對 d < 12:
```
Compress_d(x)   = ⌈(2^d / q)·x⌋ mod 2^d          ℤ_q → ℤ_{2^d}
Decompress_d(y) = ⌈(q / 2^d)·y⌋                  ℤ_{2^d} → ℤ_q
```
> 性質:`Compress_d(Decompress_d(y)) = y`。d 越接近 12,先壓再解越接近原值。除法與四捨五入在有理數上做,**禁浮點**。

### Algorithm 5 — ByteEncode_d(F)
把 256 個 d 位元整數編碼成位元組(1 ≤ d ≤ 12)。
```
輸入: F ∈ ℤ_m^256   (d<12 時 m=2^d;d=12 時 m=q)    輸出: B ∈ 𝔹^(32d)
1: for i ← 0 … 255:
2:     a ← F[i]
3:     for j ← 0 … d−1:
4:         b[i·d + j] ← a mod 2
5:         a ← (a − b[i·d+j]) / 2
6: B ← BitsToBytes(b)
7: return B
```

### Algorithm 6 — ByteDecode_d(B)
位元組 → 256 個 d 位元整數(Algorithm 5 之逆)。
```
輸入: B ∈ 𝔹^(32d)      輸出: F ∈ ℤ_m^256
1: b ← BytesToBits(B)
2: for i ← 0 … 255:
3:     F[i] ← Σ_{j=0}^{d−1} b[i·d + j]·2^j   mod m
4: return F
```
> d=12 時對 4096 取出後再 mod q;某些 12 位元段可能 ≥ q(這正是封裝金鑰「模數檢查」的依據)。

### Algorithm 7 — SampleNTT(B)
由 32 位元組種子 + 2 索引位元組 → T_q 的均勻元素(拒絕取樣)。
```
輸入: B ∈ 𝔹³⁴        輸出: â ∈ ℤ_q^256(某多項式的 NTT 表示係數)
1: ctx ← XOF.Init();  ctx ← XOF.Absorb(ctx, B)
2: j ← 0
3: while j < 256:
4:     (ctx, C) ← XOF.Squeeze(ctx, 3)        取 3 位元組
5:     d₁ ← C[0] + 256·(C[1] mod 16)          0 ≤ d₁ < 2¹²
6:     d₂ ← ⌊C[1]/16⌋ + 16·C[2]               0 ≤ d₂ < 2¹²
7:     if d₁ < q:  â[j] ← d₁;  j ← j+1
8:     if d₂ < q and j < 256:  â[j] ← d₂;  j ← j+1
9: return â
```

### Algorithm 8 — SamplePolyCBD_η(B)
由位元組流取「中心二項分布」CBD 的多項式(雜訊/誤差)。
```
輸入: B ∈ 𝔹^(64η)       輸出: f ∈ ℤ_q^256
1: b ← BytesToBits(B)
2: for i ← 0 … 255:
3:     x ← Σ_{j=0}^{η−1} b[2iη + j]
4:     y ← Σ_{j=0}^{η−1} b[2iη + η + j]
5:     f[i] ← (x − y) mod q          結果落在 {0..η} 或 {q−η..q−1}
6: return f
```

### Algorithm 9 — NTT(f)
多項式 → NTT 表示(就地、蝶形運算)。`ζ = 17`。
```
輸入: f ∈ ℤ_q^256      輸出: f̂ ∈ ℤ_q^256
1: f̂ ← f;  i ← 1
2: for len ← 128, 64, …, 2  (len ← len/2):
3:     for start ← 0; start < 256; start ← start + 2·len:
4:         zeta ← ζ^BitRev7(i) mod q;  i ← i+1
5:         for j ← start … start+len−1:
6:             t ← zeta · f̂[j+len]          (以下皆 mod q)
7:             f̂[j+len] ← f̂[j] − t
8:             f̂[j]     ← f̂[j] + t
9: return f̂
```

### Algorithm 10 — NTT⁻¹(f̂)
NTT 表示 → 多項式。最後乘 `3303 ≡ 128⁻¹ (mod q)`。
```
輸入: f̂ ∈ ℤ_q^256      輸出: f ∈ ℤ_q^256
1: f ← f̂;  i ← 127
2: for len ← 2, 4, …, 128  (len ← 2·len):
3:     for start ← 0; start < 256; start ← start + 2·len:
4:         zeta ← ζ^BitRev7(i) mod q;  i ← i−1
5:         for j ← start … start+len−1:
6:             t ← f[j]
7:             f[j]     ← t + f[j+len]        (mod q)
8:             f[j+len] ← zeta · (f[j+len] − t)
9: f ← f · 3303 mod q
10: return f
```

### Algorithm 11 — MultiplyNTTs(f̂, ĝ)
T_q 中的乘法(逐 128 點做二次模乘法)。
```
輸入: f̂, ĝ ∈ ℤ_q^256        輸出: ĥ ∈ ℤ_q^256
1: for i ← 0 … 127:
2:     (ĥ[2i], ĥ[2i+1]) ← BaseCaseMultiply(f̂[2i], f̂[2i+1], ĝ[2i], ĝ[2i+1], ζ^(2·BitRev7(i)+1))
3: return ĥ
```

### Algorithm 12 — BaseCaseMultiply(a₀, a₁, b₀, b₁, γ)
兩個一次多項式在模 `X² − γ` 下相乘。
```
輸入: a₀,a₁,b₀,b₁ ∈ ℤ_q;  γ ∈ ℤ_q      輸出: (c₀, c₁)
1: c₀ ← a₀·b₀ + a₁·b₁·γ        (mod q)
2: c₁ ← a₀·b₁ + a₁·b₀          (mod q)
3: return (c₀, c₁)
```

## I-5. K-PKE 元件(僅作子程式)

### Algorithm 13 — K-PKE.KeyGen(d)
產生加密金鑰 ek_PKE 與解密金鑰 dk_PKE。
```
輸入: d ∈ 𝔹³²
輸出: ek_PKE ∈ 𝔹^(384k+32),  dk_PKE ∈ 𝔹^(384k)
1: (ρ, σ) ← G(d ‖ k)                  ← 注意串接 k(域分離,k∈{2,3,4})
2: N ← 0
3: for i ← 0 … k−1:                     生成矩陣 Â (k×k)
4:     for j ← 0 … k−1:
5:         Â[i,j] ← SampleNTT(ρ ‖ j ‖ i)
6: for i ← 0 … k−1:                     生成 s ∈ R_q^k(CBD)
7:     s[i] ← SamplePolyCBD_η₁(PRF_η₁(σ, N));  N ← N+1
8: for i ← 0 … k−1:                     生成 e ∈ R_q^k(CBD)
9:     e[i] ← SamplePolyCBD_η₁(PRF_η₁(σ, N));  N ← N+1
10: ŝ ← NTT(s);   ê ← NTT(e)
11: t̂ ← Â ∘ ŝ + ê                       NTT 域中的含雜訊線性系統 t = As + e
12: ek_PKE ← ByteEncode₁₂(t̂) ‖ ρ        附上 Â 的種子 ρ
13: dk_PKE ← ByteEncode₁₂(ŝ)
14: return (ek_PKE, dk_PKE)
```
> 直覺:dk = 秘密向量 s;ek = 一組含雜訊的線性方程 (A, t = As + e)。

### Algorithm 14 — K-PKE.Encrypt(ek_PKE, m, r)
用加密金鑰 + 隨機 r 加密 32 位元組訊息 m。
```
輸入: ek_PKE ∈ 𝔹^(384k+32),  m ∈ 𝔹³²,  r ∈ 𝔹³²
輸出: c ∈ 𝔹^(32(dᵤk + dᵥ))
1:  N ← 0
2:  t̂ ← ByteDecode₁₂(ek_PKE[0 : 384k])
3:  ρ ← ek_PKE[384k : 384k+32]
4:  for i,j: Â[i,j] ← SampleNTT(ρ ‖ j ‖ i)       重建矩陣 Â
5:  for i ← 0 … k−1:  y[i] ← SamplePolyCBD_η₁(PRF_η₁(r, N));  N ← N+1
6:  for i ← 0 … k−1:  e₁[i] ← SamplePolyCBD_η₂(PRF_η₂(r, N)); N ← N+1
7:  e₂ ← SamplePolyCBD_η₂(PRF_η₂(r, N))
8:  ŷ ← NTT(y)
9:  u ← NTT⁻¹(Âᵀ ∘ ŷ) + e₁                       u = Aᵀy + e₁
10: μ ← Decompress₁(ByteDecode₁(m))               訊息編碼成多項式
11: v ← NTT⁻¹(t̂ᵀ ∘ ŷ) + e₂ + μ                    v = tᵀy + e₂ + μ
12: c₁ ← ByteEncode_dᵤ(Compress_dᵤ(u))
13: c₂ ← ByteEncode_dᵥ(Compress_dᵥ(v))
14: return c ← (c₁ ‖ c₂)
```

### Algorithm 15 — K-PKE.Decrypt(dk_PKE, c)
用解密金鑰還原訊息 m。
```
輸入: dk_PKE ∈ 𝔹^(384k),  c ∈ 𝔹^(32(dᵤk + dᵥ))
輸出: m ∈ 𝔹³²
1: c₁ ← c[0 : 32dᵤk]
2: c₂ ← c[32dᵤk : 32(dᵤk + dᵥ)]
3: u' ← Decompress_dᵤ(ByteDecode_dᵤ(c₁))
4: v' ← Decompress_dᵥ(ByteDecode_dᵥ(c₂))
5: ŝ ← ByteDecode₁₂(dk_PKE)
6: w ← v' − NTT⁻¹(ŝᵀ ∘ NTT(u'))                  w = v' − sᵀu'
7: m ← ByteEncode₁(Compress₁(w))
8: return m
```

## I-6. 主內部演算法(去隨機化、確定性)

### Algorithm 16 — ML-KEM.KeyGen_internal(d, z)
```
輸入: d, z ∈ 𝔹³²
輸出: ek ∈ 𝔹^(384k+32),  dk ∈ 𝔹^(768k+96)
1: (ek_PKE, dk_PKE) ← K-PKE.KeyGen(d)
2: ek ← ek_PKE
3: dk ← (dk_PKE ‖ ek ‖ H(ek) ‖ z)                 z 供「隱性拒絕」使用
4: return (ek, dk)
```

### Algorithm 17 — ML-KEM.Encaps_internal(ek, m)
```
輸入: ek ∈ 𝔹^(384k+32),  m ∈ 𝔹³²
輸出: K ∈ 𝔹³²,  c ∈ 𝔹^(32(dᵤk+dᵥ))
1: (K, r) ← G(m ‖ H(ek))                           由 m 與 ek 導出共享金鑰 K 與隨機 r
2: c ← K-PKE.Encrypt(ek, m, r)
3: return (K, c)
```

### Algorithm 18 — ML-KEM.Decaps_internal(dk, c)
含 FO 轉換的「重加密比對 + 隱性拒絕」。
```
輸入: dk ∈ 𝔹^(768k+96),  c ∈ 𝔹^(32(dᵤk+dᵥ))
輸出: K ∈ 𝔹³²
1: dk_PKE ← dk[0 : 384k]
2: ek_PKE ← dk[384k : 768k+32]
3: h ← dk[768k+32 : 768k+64]                       = H(ek)
4: z ← dk[768k+64 : 768k+96]                        隱性拒絕值
5: m' ← K-PKE.Decrypt(dk_PKE, c)
6: (K', r') ← G(m' ‖ h)
7: K̄ ← J(z ‖ c)
8: c' ← K-PKE.Encrypt(ek_PKE, m', r')               用導出的 r' 重加密
9: if c ≠ c':  K' ← K̄                               密文不符 → 隱性拒絕
10: return K'
```
> 「是否拒絕」這個旗標是秘密,**不可外洩**,演算法結束前須銷毀。

## I-7. ML-KEM 對外演算法

### Algorithm 19 — ML-KEM.KeyGen()
```
輸出: ek ∈ 𝔹^(384k+32),  dk ∈ 𝔹^(768k+96)
1: d ←$ 𝔹³²                                        以核可 RBG 取 32 隨機位元組
2: z ←$ 𝔹³²
3: if d == NULL or z == NULL:  return ⊥
4: (ek, dk) ← ML-KEM.KeyGen_internal(d, z)
5: return (ek, dk)
```

### Algorithm 20 — ML-KEM.Encaps(ek)
**先做封裝金鑰檢查**,再呼叫內部封裝。
```
[封裝金鑰檢查] 輸入 ek:
 1) 型別檢查:長度須為 384k+32 位元組。
 2) 模數檢查:test ← ByteEncode₁₂(ByteDecode₁₂(ek[0:384k]));
              若 test ≠ ek[0:384k] 則檢查失敗。(確保編碼整數都在 [0, q−1])

輸出: K ∈ 𝔹³²,  c ∈ 𝔹^(32(dᵤk+dᵥ))
1: m ←$ 𝔹³²
2: if m == NULL:  return ⊥
3: (K, c) ← ML-KEM.Encaps_internal(ek, m)
4: return (K, c)
```

### Algorithm 21 — ML-KEM.Decaps(dk, c)
**先做輸入檢查**,再呼叫內部解封裝。
```
[解封裝輸入檢查]:
 1) 密文型別:c 長度須為 32(dᵤk+dᵥ)。
 2) 金鑰型別:dk 長度須為 768k+96。
 3) 雜湊檢查:test ← H(dk[384k : 768k+32]);
              若 test ≠ dk[768k+32 : 768k+64] 則失敗。

輸出: K' ∈ 𝔹³²
1: K' ← ML-KEM.Decaps_internal(dk, c)
2: return K'
```
> 密文檢查**每次解封裝都要做**;金鑰檢查可由其他途徑事先保證。


---

# Part II — FIPS 204:ML-DSA(數位簽章)

## II-1. 整體結構

ML-DSA 採 **Fiat-Shamir with Aborts**(帶拒絕的 Schnorr 類簽章),安全目標 **SUF-CMA**(強不可偽造)。三個對外演算法 + 一個預雜湊變體 HashML-DSA。

```
ML-DSA.KeyGen()               → (pk, sk)
ML-DSA.Sign(sk, M, ctx)       → σ
ML-DSA.Verify(pk, M, σ, ctx)  → Boolean
```

安全基礎:MLWE + SelfTargetMSIS。簽章核心是一個**拒絕取樣迴圈**:反覆嘗試直到 z 與 hint 都通過界限檢查。

**hedged vs deterministic**:預設為 hedged(每次簽章注入 32 位元組新隨機 `rnd`);確定性變體把 `rnd` 設為全零 `{0}³²`。兩者只差在 `rnd` 來源,驗證流程相同。

## II-2. 參數集

| 參數 | ML-DSA-44 | ML-DSA-65 | ML-DSA-87 |
|---|---|---|---|
| q | 8380417 | 8380417 | 8380417 |
| ζ | 1753 | 1753 | 1753 |
| d(丟棄低位元數) | 13 | 13 | 13 |
| τ(c 中 ±1 個數) | 39 | 49 | 60 |
| λ(c̃ 抗碰撞強度) | 128 | 192 | 256 |
| γ₁(y 係數範圍) | 2¹⁷ | 2¹⁹ | 2¹⁹ |
| γ₂(低位元捨入範圍) | (q−1)/88 | (q−1)/32 | (q−1)/32 |
| (k, ℓ)(矩陣 A 維度) | (4, 4) | (6, 5) | (8, 7) |
| η(私鑰係數範圍) | 2 | 4 | 2 |
| β = τ·η | 78 | 196 | 120 |
| ω(hint 中 1 的上限) | 80 | 55 | 75 |
| 安全類別 | 2 | 3 | 5 |

**大小(位元組)**

| 參數集 | 私鑰 sk | 公鑰 pk | 簽章 σ |
|---|---|---|---|
| ML-DSA-44 | 2560 | 1312 | 2420 |
| ML-DSA-65 | 4032 | 1952 | 3309 |
| ML-DSA-87 | 4896 | 2592 | 4627 |

## II-3. 對稱密碼函數

```
H(str, ℓ) := SHAKE256(str, 8ℓ)            (主要 XOF / 雜湊)
G(str, ℓ) := SHAKE128(str, 8ℓ)            (RejNTTPoly / ExpandA 用,較快)
```
兩者皆有 Init / Absorb / Squeeze 增量介面。預雜湊版另可用 SHA2/SHAKE 等核可函數。

## II-4. 對外函數(External)

### Algorithm 1 — ML-DSA.KeyGen()
```
輸出: pk, sk
1: ξ ←$ 𝔹³²                                以核可 RBG 取 32 位元組種子
2: if ξ == NULL:  return ⊥
3: return ML-DSA.KeyGen_internal(ξ)
```

### Algorithm 2 — ML-DSA.Sign(sk, M, ctx)
```
輸入: sk,  訊息 M ∈ {0,1}*,  context 字串 ctx(≤ 255 位元組)
輸出: σ
1: if |ctx| > 255:  return ⊥
2: rnd ←$ 𝔹³²            (確定性變體改用 rnd ← {0}³²)
3: if rnd == NULL:  return ⊥
4: M' ← BytesToBits( IntegerToBytes(0,1) ‖ IntegerToBytes(|ctx|,1) ‖ ctx ) ‖ M
5: σ ← ML-DSA.Sign_internal(sk, M', rnd)
6: return σ
```
> 第 4 行的 `0` 是域分離旗標(純 ML-DSA = 0;預雜湊 = 1)。

### Algorithm 3 — ML-DSA.Verify(pk, M, σ, ctx)
```
輸出: Boolean
1: if |ctx| > 255:  return ⊥
2: M' ← BytesToBits( IntegerToBytes(0,1) ‖ IntegerToBytes(|ctx|,1) ‖ ctx ) ‖ M
3: return ML-DSA.Verify_internal(pk, M', σ)
```
> pk 與 σ 長度若不符規格,**必須回傳 false**(關乎強不可偽造性)。

### Algorithm 4 — HashML-DSA.Sign(sk, M, ctx, PH)
先把訊息預雜湊,再簽章(域分離旗標 = 1)。
```
1: if |ctx| > 255:  return ⊥
2: rnd ←$ 𝔹³²            (確定性:rnd ← {0}³²)
3: if rnd == NULL:  return ⊥
4: switch PH:
     case SHA-256:   OID ← (…2.16.840.1.101.3.4.2.1…);   PH_M ← SHA256(M)
     case SHA-512:   OID ← (…2.16.840.1.101.3.4.2.3…);   PH_M ← SHA512(M)
     case SHAKE128:  OID ← (…2.16.840.1.101.3.4.2.11…);  PH_M ← SHAKE128(M, 256)
     …
5: M' ← BytesToBits( IntegerToBytes(1,1) ‖ IntegerToBytes(|ctx|,1) ‖ ctx ‖ OID ‖ PH_M )
6: σ ← ML-DSA.Sign_internal(sk, M', rnd)
7: return σ
```

### Algorithm 5 — HashML-DSA.Verify(pk, M, σ, ctx, PH)
```
1: if |ctx| > 255:  return false
2: switch PH: … (同 Alg 4 取得 OID 與 PH_M)
3: M' ← BytesToBits( IntegerToBytes(1,1) ‖ IntegerToBytes(|ctx|,1) ‖ ctx ‖ OID ‖ PH_M )
4: return ML-DSA.Verify_internal(pk, M', σ)
```

## II-5. 內部函數(Internal)

### Algorithm 6 — ML-DSA.KeyGen_internal(ξ)
```
輸入: ξ ∈ 𝔹³²
輸出: pk, sk
1: (ρ, ρ', K) ∈ 𝔹³² × 𝔹⁶⁴ × 𝔹³² ← H(ξ ‖ IntegerToBytes(k,1) ‖ IntegerToBytes(ℓ,1), 128)
2: Â ← ExpandA(ρ)                          矩陣 A,直接以 NTT 表示 Â 儲存
3: (s₁, s₂) ← ExpandS(ρ')                  短係數秘密向量,範圍 [−η, η]
4: t ← NTT⁻¹(Â ∘ NTT(s₁)) + s₂              計算 t = A·s₁ + s₂
5: (t₁, t₀) ← Power2Round(t)               壓縮 t:逐係數丟棄 d 個低位元
6: pk ← pkEncode(ρ, t₁)
7: tr ← H(pk, 64)                          公鑰雜湊,供簽章用
8: sk ← skEncode(ρ, K, tr, s₁, s₂, t₀)
9: return (pk, sk)
```
> ρ 是公開種子(取代整個 A);t₁ 是 t 的高位元(放公鑰);t₀ 是低位元(放私鑰)。

### Algorithm 7 — ML-DSA.Sign_internal(sk, M', rnd)
核心拒絕取樣迴圈。
```
輸入: sk,  格式化訊息 M' ∈ {0,1}*,  rnd ∈ 𝔹³²
輸出: σ
1:  (ρ, K, tr, s₁, s₂, t₀) ← skDecode(sk)
2:  ŝ₁ ← NTT(s₁)
3:  ŝ₂ ← NTT(s₂)
4:  t̂₀ ← NTT(t₀)
5:  Â  ← ExpandA(ρ)
6:  μ  ← H(BytesToBits(tr) ‖ M', 64)              訊息代表值 μ
7:  ρ'' ← H(K ‖ rnd ‖ μ, 64)                       私有隨機種子(hedged 看 rnd)
8:  κ ← 0
9:  (z, h) ← ⊥
10: while (z, h) == ⊥:                              ── 拒絕取樣迴圈 ──
11:     y ← ExpandMask(ρ'', κ)                      y 係數 ∈ [−γ₁+1, γ₁]
12:     w ← NTT⁻¹(Â ∘ NTT(y))                       w = A·y
13:     w₁ ← HighBits(w)                            承諾(commitment)
14:     c̃ ← H(μ ‖ w1Encode(w₁), λ/4)                承諾雜湊
15:     c ← SampleInBall(c̃)                         挑戰多項式(係數 {−1,0,1},權重 τ)
16:     ĉ ← NTT(c)
17:     ⟨⟨c·s₁⟩⟩ ← NTT⁻¹(ĉ ∘ ŝ₁)
18:     ⟨⟨c·s₂⟩⟩ ← NTT⁻¹(ĉ ∘ ŝ₂)
19:     z ← y + ⟨⟨c·s₁⟩⟩                            回應(response)
20:     r₀ ← LowBits(w − ⟨⟨c·s₂⟩⟩)
21:     if ‖z‖∞ ≥ γ₁ − β  or  ‖r₀‖∞ ≥ γ₂ − β:
22:         (z, h) ← ⊥                              界限不過 → 重試
23:     else:
24:         ⟨⟨c·t₀⟩⟩ ← NTT⁻¹(ĉ ∘ t̂₀)
25:         h ← MakeHint(−⟨⟨c·t₀⟩⟩, w − ⟨⟨c·s₂⟩⟩ + ⟨⟨c·t₀⟩⟩)
26:         if ‖⟨⟨c·t₀⟩⟩‖∞ ≥ γ₂  or  (h 中 1 的個數 > ω):
27:             (z, h) ← ⊥
28:     κ ← κ + ℓ                                   計數器遞增
29: σ ← sigEncode(c̃, z mod±q, h)
30: return σ
```
> `‖·‖∞` 為無窮範數;`mod±q` 取對稱餘 (−q/2, q/2]。迴圈期望重複次數約 4.25 / 5.1 / 3.85 次(44/65/87)。

### Algorithm 8 — ML-DSA.Verify_internal(pk, M', σ)
```
輸入: pk,  M' ∈ {0,1}*,  σ
輸出: Boolean
1:  (ρ, t₁) ← pkDecode(pk)
2:  (c̃, z, h) ← sigDecode(σ)
3:  if h == ⊥:  return false                        hint 編碼不合法
4:  Â ← ExpandA(ρ)
5:  tr ← H(pk, 64)
6:  μ ← H(BytesToBits(tr) ‖ M', 64)
7:  c ← SampleInBall(c̃)
8:  w'_Approx ← NTT⁻¹( Â ∘ NTT(z) − NTT(c) ∘ NTT(t₁·2^d) )    w' = A·z − c·t₁·2^d
9:  w₁' ← UseHint(h, w'_Approx)                      重建承諾
10: c̃' ← H(μ ‖ w1Encode(w₁'), λ/4)
11: return ⟦ ‖z‖∞ < γ₁ − β ⟧  and  ⟦ c̃ == c̃' ⟧
```
> 驗證原理:正確簽章下 `A·y = A·z − c·t + c·s₂ ≈ A·z − c·t₁·2^d`,故重建的 w₁' 應與簽章承諾一致。

## II-6. 輔助函數(Auxiliary)

### II-6-1. 資料型別轉換

#### Algorithm 9 — IntegerToBits(x, α)
整數 → α 位元(小端)。
```
1: x' ← x
2: for i ← 0 … α−1:  y[i] ← x' mod 2;  x' ← ⌊x'/2⌋
3: return y
```

#### Algorithm 10 — BitsToInteger(y, α)
α 位元 → 整數(小端)。
```
1: x ← 0
2: for i ← 1 … α:  x ← 2x + y[α − i]
3: return x
```

#### Algorithm 11 — IntegerToBytes(x, α)
整數 → α 位元組(小端,base-256)。
```
1: x' ← x
2: for i ← 0 … α−1:  y[i] ← x' mod 256;  x' ← ⌊x'/256⌋
3: return y
```

#### Algorithm 12 — BitsToBytes(y)
位元字串 → 位元組字串。
```
1: z ← 0^⌈α/8⌉
2: for i ← 0 … α−1:  z[⌊i/8⌋] ← z[⌊i/8⌋] + y[i]·2^(i mod 8)
3: return z
```

#### Algorithm 13 — BytesToBits(z)
位元組字串 → 位元字串。
```
1: z' ← z
2: for i ← 0 … α−1:
3:     for j ← 0 … 7:  y[8i+j] ← z'[i] mod 2;  z'[i] ← ⌊z'[i]/2⌋
4: return y
```

#### Algorithm 14 — CoeffFromThreeBytes(b₀, b₁, b₂)
3 位元組 → {0,…,q−1} 或 ⊥(拒絕取樣)。
```
1: b₂' ← b₂;  if b₂' > 127:  b₂' ← b₂' − 128      清最高位
2: z ← 2¹⁶·b₂' + 2⁸·b₁ + b₀                        0 ≤ z ≤ 2²³−1
3: if z < q:  return z   else:  return ⊥
```

#### Algorithm 15 — CoeffFromHalfByte(b)
半位元組 → {−η,…,η} 或 ⊥。(η ∈ {2, 4})
```
1: if η == 2 and b < 15:  return 2 − (b mod 5)
2: else if η == 4 and b < 9:  return 4 − b
3: else:  return ⊥
```

### II-6-2. 多項式打包 / 解包

#### Algorithm 16 — SimpleBitPack(w, b)
係數 ∈ [0, b] 的多項式 → 位元組(長 32·bitlen b)。
```
1: z ← ()  (空位元字串)
2: for i ← 0 … 255:  z ← z ‖ IntegerToBits(w_i, bitlen b)
3: return BitsToBytes(z)
```

#### Algorithm 17 — BitPack(w, a, b)
係數 ∈ [−a, b] 的多項式 → 位元組(長 32·bitlen(a+b))。
```
1: z ← ()
2: for i ← 0 … 255:  z ← z ‖ IntegerToBits(b − w_i, bitlen(a+b))
3: return BitsToBytes(z)
```

#### Algorithm 18 — SimpleBitUnpack(v, b)
SimpleBitPack 之逆。
```
1: c ← bitlen b;   z ← BytesToBits(v)
2: for i ← 0 … 255:  w_i ← BitsToInteger((z[ic], …, z[ic+c−1]), c)
3: return w
```

#### Algorithm 19 — BitUnpack(v, a, b)
BitPack 之逆。
```
1: c ← bitlen(a+b);   z ← BytesToBits(v)
2: for i ← 0 … 255:  w_i ← b − BitsToInteger((z[ic], …, z[ic+c−1]), c)
3: return w
```

#### Algorithm 20 — HintBitPack(h)
稀疏二元 hint 向量 h ∈ R₂^k(總共 ≤ ω 個 1)→ 長 ω+k 位元組。
```
1: y ← 0^(ω+k);   Index ← 0
2: for i ← 0 … k−1:                          逐個多項式 h[i]
3:     for j ← 0 … 255:
4:         if h[i]_j ≠ 0:  y[Index] ← j;  Index ← Index+1
5:     y[ω + i] ← Index                       記錄到此為止的累計索引
6: return y
```

#### Algorithm 21 — HintBitUnpack(y)
HintBitPack 之逆;含**惡意輸入檢查**(正式版補回,關乎強不可偽造)。
```
1: h ← 0^k;   Index ← 0
2: for i ← 0 … k−1:
3:     if y[ω+i] < Index or y[ω+i] > ω:  return ⊥        格式錯誤
4:     First ← Index
5:     while Index < y[ω+i]:
6:         if Index > First and y[Index−1] ≥ y[Index]:  return ⊥
7:         h[i]_{y[Index]} ← 1;  Index ← Index+1
8: for i ← Index … ω−1:                                    剩餘位元組須為 0
9:     if y[i] ≠ 0:  return ⊥
10: return h
```

### II-6-3. 金鑰與簽章編碼

#### Algorithm 22 — pkEncode(ρ, t₁)
```
1: pk ← ρ
2: for i ← 0 … k−1:  pk ← pk ‖ SimpleBitPack(t₁[i], 2^(bitlen(q−1)−d) − 1)
3: return pk
```

#### Algorithm 23 — pkDecode(pk)
```
1: (ρ, z₀, …, z_{k−1}) ← pk
2: for i ← 0 … k−1:  t₁[i] ← SimpleBitUnpack(z_i, 2^(bitlen(q−1)−d) − 1)
3: return (ρ, t₁)
```

#### Algorithm 24 — skEncode(ρ, K, tr, s₁, s₂, t₀)
```
1: sk ← ρ ‖ K ‖ tr
2: for i ← 0 … ℓ−1:  sk ← sk ‖ BitPack(s₁[i], η, η)
3: for i ← 0 … k−1:  sk ← sk ‖ BitPack(s₂[i], η, η)
4: for i ← 0 … k−1:  sk ← sk ‖ BitPack(t₀[i], 2^(d−1) − 1, 2^(d−1))
5: return sk
```

#### Algorithm 25 — skDecode(sk)
```
1: (ρ, K, tr, y₀…y_{ℓ−1}, z₀…z_{k−1}, w₀…w_{k−1}) ← sk
2: for i ← 0 … ℓ−1:  s₁[i] ← BitUnpack(y_i, η, η)
3: for i ← 0 … k−1:  s₂[i] ← BitUnpack(z_i, η, η)
4: for i ← 0 … k−1:  t₀[i] ← BitUnpack(w_i, 2^(d−1) − 1, 2^(d−1))
5: return (ρ, K, tr, s₁, s₂, t₀)
```
> skDecode 只能餵**可信來源**(惡意輸入可能讓 s₁/s₂ 落在範圍外)。

#### Algorithm 26 — sigEncode(c̃, z, h)
```
1: σ ← c̃
2: for i ← 0 … ℓ−1:  σ ← σ ‖ BitPack(z[i], γ₁−1, γ₁)
3: σ ← σ ‖ HintBitPack(h)
4: return σ
```

#### Algorithm 27 — sigDecode(σ)
```
1: (c̃, x₀, …, x_{ℓ−1}, y) ← σ
2: for i ← 0 … ℓ−1:  z[i] ← BitUnpack(x_i, γ₁−1, γ₁)
3: h ← HintBitUnpack(y)
4: return (c̃, z, h)
```

#### Algorithm 28 — w1Encode(w₁)
把承諾 w₁ 編碼以供 H 雜湊。
```
1: w̃₁ ← ()
2: for i ← 0 … k−1:  w̃₁ ← w̃₁ ‖ SimpleBitPack(w₁[i], (q−1)/(2γ₂) − 1)
3: return w̃₁
```

### II-6-4. 偽隨機取樣

#### Algorithm 29 — SampleInBall(ρ)
產生係數 ∈ {−1,0,1}、Hamming 權重 τ 的多項式(Fisher-Yates 洗牌)。
```
輸入: ρ ∈ 𝔹^(λ/4)        輸出: c ∈ R
1: c ← 0
2: ctx ← H.Init();  ctx ← H.Absorb(ctx, ρ)
3: (ctx, s) ← H.Squeeze(ctx, 8)
4: hbits ← BytesToBits(s)                            前 8 位元組決定 τ 個符號
5: for i ← 256−τ … 255:
6:     (ctx, j) ← H.Squeeze(ctx, 1)
7:     while j > i:  (ctx, j) ← H.Squeeze(ctx, 1)    拒絕取樣 j ∈ {0,…,i}
8:     c_i ← c_j
9:     c_j ← (−1)^hbits[i + τ − 256]
10: return c
```

#### Algorithm 30 — RejNTTPoly(ρ)
均勻取樣 T_q 的元素(用較快的 G = SHAKE128)。
```
輸入: ρ ∈ 𝔹³⁴        輸出: â ∈ T_q
1: j ← 0;  ctx ← G.Init();  ctx ← G.Absorb(ctx, ρ)
2: while j < 256:
3:     (ctx, s) ← G.Squeeze(ctx, 3)
4:     â[j] ← CoeffFromThreeBytes(s[0], s[1], s[2])
5:     if â[j] ≠ ⊥:  j ← j+1
6: return â
```

#### Algorithm 31 — RejBoundedPoly(ρ)
取樣係數 ∈ [−η, η] 的多項式(用 H = SHAKE256)。
```
輸入: ρ ∈ 𝔹⁶⁶        輸出: a ∈ R
1: j ← 0;  ctx ← H.Init();  ctx ← H.Absorb(ctx, ρ)
2: while j < 256:
3:     z ← H.Squeeze(ctx, 1)
4:     z₀ ← CoeffFromHalfByte(z mod 16)
5:     z₁ ← CoeffFromHalfByte(⌊z/16⌋)
6:     if z₀ ≠ ⊥:  a_j ← z₀;  j ← j+1
7:     if z₁ ≠ ⊥ and j < 256:  a_j ← z₁;  j ← j+1
8: return a
```

#### Algorithm 32 — ExpandA(ρ)
由種子 ρ 展開 k×ℓ 矩陣 Â(NTT 表示)。
```
輸入: ρ ∈ 𝔹³²        輸出: Â ∈ (T_q)^(k×ℓ)
1: for r ← 0 … k−1:
2:     for s ← 0 … ℓ−1:
3:         ρ' ← ρ ‖ IntegerToBytes(s,1) ‖ IntegerToBytes(r,1)
4:         Â[r,s] ← RejNTTPoly(ρ')
5: return Â
```

#### Algorithm 33 — ExpandS(ρ)
展開短係數向量 s₁ ∈ R^ℓ、s₂ ∈ R^k。
```
輸入: ρ ∈ 𝔹⁶⁴        輸出: (s₁, s₂)
1: for r ← 0 … ℓ−1:  s₁[r] ← RejBoundedPoly(ρ ‖ IntegerToBytes(r, 2))
2: for r ← 0 … k−1:  s₂[r] ← RejBoundedPoly(ρ ‖ IntegerToBytes(r + ℓ, 2))
3: return (s₁, s₂)
```

#### Algorithm 34 — ExpandMask(ρ, μ)
展開遮罩向量 y ∈ R^ℓ,係數 ∈ [−γ₁+1, γ₁]。
```
輸入: ρ ∈ 𝔹⁶⁴,  μ ∈ ℕ        輸出: y ∈ R^ℓ
1: c ← 1 + bitlen(γ₁ − 1)                         γ₁ 恆為 2 的冪
2: for r ← 0 … ℓ−1:
3:     ρ' ← ρ ‖ IntegerToBytes(μ + r, 2)
4:     v ← H(ρ', 32c)
5:     y[r] ← BitUnpack(v, γ₁ − 1, γ₁)
6: return y
```

### II-6-5. 高低位元與 Hint

> 用途:把 t 丟掉 d 個低位元(Power2Round)以壓縮公鑰;簽章再附 hint 讓驗證者重建被丟棄資訊。所有函數對向量時**逐係數**套用。

#### Algorithm 35 — Power2Round(r)
`r mod q = r₁·2^d + r₀`,直接的位元切分。
```
輸入: r ∈ ℤ_q        輸出: (r₁, r₀)
1: r⁺ ← r mod q
2: r₀ ← r⁺ mod±2^d
3: return ((r⁺ − r₀) / 2^d, r₀)
```

#### Algorithm 36 — Decompose(r)
`r mod q = r₁·(2γ₂) + r₀`,且處理邊界(避免 r 近 q−1 時 r₁ 跳變)。
```
輸入: r ∈ ℤ_q        輸出: (r₁, r₀)
1: r⁺ ← r mod q
2: r₀ ← r⁺ mod±(2γ₂)
3: if r⁺ − r₀ == q − 1:  r₁ ← 0;  r₀ ← r₀ − 1
4: else:  r₁ ← (r⁺ − r₀) / (2γ₂)
5: return (r₁, r₀)
```

#### Algorithm 37 — HighBits(r)
```
1: (r₁, r₀) ← Decompose(r);  return r₁
```

#### Algorithm 38 — LowBits(r)
```
1: (r₁, r₀) ← Decompose(r);  return r₀
```

#### Algorithm 39 — MakeHint(z, r)
判斷把 z 加到 r 是否改變了 r 的高位元。
```
輸入: z, r ∈ ℤ_q        輸出: Boolean
1: r₁ ← HighBits(r)
2: v₁ ← HighBits(r + z)
3: return ⟦ r₁ ≠ v₁ ⟧
```

#### Algorithm 40 — UseHint(h, r)
依 hint h 調整 r 的高位元。
```
輸入: Boolean h,  r ∈ ℤ_q        輸出: r₁ ∈ [0, (q−1)/(2γ₂))
1: m ← (q − 1) / (2γ₂)
2: (r₁, r₀) ← Decompose(r)
3: if h == 1 and r₀ > 0:  return (r₁ + 1) mod m
4: if h == 1 and r₀ ≤ 0:  return (r₁ − 1) mod m
5: return r₁
```

### II-6-6. NTT 與其反變換

> ML-DSA 的 ζ = 1753(512 次原根),`zetas[k] = ζ^BitRev8(k) mod q`(見附錄)。

#### Algorithm 41 — NTT(w)
```
輸入: w(X) = Σ w_j X^j ∈ R_q        輸出: ŵ ∈ T_q
1: for j ← 0 … 255:  ŵ[j] ← w_j
2: m ← 0;  len ← 128
3: while len ≥ 1:
4:     start ← 0
5:     while start < 256:
6:         m ← m + 1;   z ← zetas[m]
7:         for j ← start … start+len−1:
8:             t ← (z · ŵ[j+len]) mod q
9:             ŵ[j+len] ← (ŵ[j] − t) mod q
10:            ŵ[j]     ← (ŵ[j] + t) mod q
11:        start ← start + 2·len
12:    len ← ⌊len/2⌋
13: return ŵ
```

#### Algorithm 42 — NTT⁻¹(ŵ)
最後乘 `f = 8347681 ≡ 256⁻¹ (mod q)`。
```
輸入: ŵ ∈ T_q        輸出: w ∈ R_q
1: for j ← 0 … 255:  w_j ← ŵ[j]
2: m ← 256;  len ← 1
3: while len < 256:
4:     start ← 0
5:     while start < 256:
6:         m ← m − 1;   z ← −zetas[m]
7:         for j ← start … start+len−1:
8:             t ← w_j
9:             w_j      ← (t + w_{j+len}) mod q
10:            w_{j+len} ← (t − w_{j+len}) mod q
11:            w_{j+len} ← (z · w_{j+len}) mod q
12:        start ← start + 2·len
13:    len ← 2·len
14: f ← 8347681
15: for j ← 0 … 255:  w_j ← (f · w_j) mod q
16: return w
```

#### Algorithm 43 — BitRev8(m)
反轉一個位元組的 8 位元順序。
```
1: b ← IntegerToBits(m, 8)
2: b_rev ← (0,…,0)
3: for i ← 0 … 7:  b_rev[i] ← b[7 − i]
4: return BitsToInteger(b_rev, 8)
```

### II-6-7. NTT 域下的線性代數

#### Algorithm 44 — AddNTT(â, b̂)
```
1: for i ← 0 … 255:  ĉ[i] ← â[i] + b̂[i]
2: return ĉ
```

#### Algorithm 45 — MultiplyNTT(â, b̂)
逐座標純量乘(ML-DSA 的 NTT 點乘很單純)。
```
1: for i ← 0 … 255:  ĉ[i] ← â[i] · b̂[i]
2: return ĉ
```

#### Algorithm 46 — AddVectorNTT(v̂, ŵ)
```
1: for i ← 0 … ℓ−1:  û[i] ← AddNTT(v̂[i], ŵ[i])
2: return û
```

#### Algorithm 47 — ScalarVectorNTT(ĉ, v̂)
```
1: for i ← 0 … ℓ−1:  ŵ[i] ← MultiplyNTT(ĉ, v̂[i])
2: return ŵ
```

#### Algorithm 48 — MatrixVectorNTT(M̂, v̂)
矩陣 × 向量(NTT 域)。
```
輸入: M̂ ∈ T_q^(k×ℓ),  v̂ ∈ T_q^ℓ        輸出: ŵ ∈ T_q^k
1: ŵ ← 0^k
2: for i ← 0 … k−1:
3:     for j ← 0 … ℓ−1:
4:         ŵ[i] ← AddNTT(ŵ[i], MultiplyNTT(M̂[i,j], v̂[j]))
5: return ŵ
```

### II-6-8. Montgomery 乘法(最佳化)

#### Algorithm 49 — MontgomeryReduce(a)
計算 `a · 2⁻³² mod q`(避免昂貴的 mod 運算)。
```
輸入: 整數 a,  −2³¹·q ≤ a ≤ 2³¹·q        輸出: r ≡ a·2⁻³² (mod q)
1: QINV ← 58728449                        q⁻¹ mod 2³²
2: t ← ((a mod 2³²) · QINV) mod 2³²
3: r ← (a − t·q) / 2³²
4: return r
```
> Montgomery 形式:a 的 Montgomery 表示為 r ≡ a·2³² (mod q)。兩個此形式的數相乘後再 MontgomeryReduce,結果仍為 Montgomery 形式。


---

# Part III — 附錄

## A. 演算法索引(快速對照)

**FIPS 203 / ML-KEM(共 21 個)**

| # | 名稱 | 角色 |
|---|---|---|
| 3, 4 | BitsToBytes / BytesToBits | 位元 ↔ 位元組 |
| 5, 6 | ByteEncode_d / ByteDecode_d | 整數 ↔ 位元組 |
| — | Compress_d / Decompress_d | 係數壓縮 |
| 7 | SampleNTT | 均勻取樣 T_q |
| 8 | SamplePolyCBD_η | CBD 雜訊取樣 |
| 9, 10 | NTT / NTT⁻¹ | 數論變換 |
| 11, 12 | MultiplyNTTs / BaseCaseMultiply | T_q 乘法 |
| 13–15 | K-PKE.KeyGen / Encrypt / Decrypt | 底層 PKE |
| 16–18 | ML-KEM.{KeyGen,Encaps,Decaps}_internal | 內部(確定性) |
| 19–21 | ML-KEM.KeyGen / Encaps / Decaps | 對外 KEM |

**FIPS 204 / ML-DSA(共 49 個)**

| # | 名稱 | 角色 |
|---|---|---|
| 1–3 | ML-DSA.KeyGen / Sign / Verify | 對外 |
| 4, 5 | HashML-DSA.Sign / Verify | 預雜湊變體 |
| 6–8 | ML-DSA.{KeyGen,Sign,Verify}_internal | 內部 |
| 9–13 | IntegerToBits/BitsToInteger/IntegerToBytes/BitsToBytes/BytesToBits | 型別轉換 |
| 14, 15 | CoeffFromThreeBytes / CoeffFromHalfByte | 係數取樣輔助 |
| 16–19 | SimpleBitPack/BitPack/SimpleBitUnpack/BitUnpack | 多項式打包 |
| 20, 21 | HintBitPack / HintBitUnpack | hint 編碼 |
| 22–28 | pkEncode/pkDecode/skEncode/skDecode/sigEncode/sigDecode/w1Encode | 金鑰簽章編碼 |
| 29 | SampleInBall | 挑戰多項式 |
| 30, 31 | RejNTTPoly / RejBoundedPoly | 拒絕取樣 |
| 32–34 | ExpandA / ExpandS / ExpandMask | 種子展開 |
| 35–40 | Power2Round/Decompose/HighBits/LowBits/MakeHint/UseHint | 高低位元與 hint |
| 41–43 | NTT / NTT⁻¹ / BitRev8 | 數論變換 |
| 44–48 | AddNTT/MultiplyNTT/AddVectorNTT/ScalarVectorNTT/MatrixVectorNTT | NTT 域線代 |
| 49 | MontgomeryReduce | 模乘最佳化 |

## B. 與 Round-3 提交版的主要差異

**ML-KEM vs CRYSTALS-Kyber**
- 共享金鑰固定為 256 位元(原本長度可變)。
- FO 轉換改版:Encaps 不再把密文雜湊納入共享金鑰推導。
- 移除 Encaps 起始的 `m ← H(m)`(因強制使用核可 RBG)。
- 新增明確輸入檢查(型別、模數)。
- K-PKE.KeyGen 的 `G(d ‖ k)` 加入 k 做**域分離**;修正了 ipd 誤植的矩陣 Â 索引。

**ML-DSA vs CRYSTALS-Dilithium(v3.1)**
- 正式版:c̃ 的**全部**位元都用於產生 c(草稿只用前 256 位元)。
- ExpandMask 改為從 H 輸出的**開頭**取位元。
- HintBitUnpack 補回**惡意輸入檢查**(否則破壞強不可偽造性)。
- 簽章預設改為 **hedged**(rnd 由 RBG 產生);保留確定性變體(rnd = {0}³²)。
- 補充 HashML-DSA 的域分離與 OID 規範。
- Alg 6 第 1 行加入 (k, ℓ) 域分離,避免不同參數集從同一種子展開出相關金鑰。

## C. NTT 預計算表(zetas)

兩個標準都把 `ζ^BitRev*(i) mod q` 預先算成陣列以加速 NTT。

**ML-KEM(ζ = 17,128 個值,供 Alg 9/10)** — `ζ^BitRev7(i) mod q`:
```
1, 1729, 2580, 3289, 2642, 630, 1897, 848, 1062, 1919, 193, 797, 2786, 3260, 569, 1746,
296, 2447, 1339, 1476, 3046, 56, 2240, 1333, 1426, 2094, 535, 2882, 2393, 2879, 1974, 821,
289, 331, 3253, 1756, 1197, 2304, 2277, 2055, 650, 1977, 2513, 632, 2865, 33, 1320, 1915,
2319, 1435, 807, 452, 1438, 2868, 1534, 2402, 2647, 2617, 1481, 648, 2474, 3110, 1227, 910,
17, 2761, 583, 2649, 1637, 723, 2288, 1100, 1409, 2662, 3281, 233, 756, 2156, 3015, 3050,
1703, 1651, 2789, 1789, 1847, 952, 1461, 2687, 939, 2308, 2437, 2388, 733, 2337, 268, 641,
1584, 2298, 2037, 3220, 375, 2549, 2090, 1645, 1063, 319, 2773, 757, 2099, 561, 2466, 2594,
2804, 1092, 403, 1026, 1143, 2150, 2775, 886, 1722, 1212, 1874, 1029, 2110, 2935, 885, 2154
```
(BaseCaseMultiply 用的是 `ζ^(2·BitRev7(i)+1) mod q`,即上列各值的「±配對」版本。)

**ML-DSA(ζ = 1753,zetas[0..255],供 Alg 41/42)** — `ζ^BitRev8(k) mod q`,前段:
```
0, 4808194, 3765607, 3761513, 5178923, 5496691, 5234739, 5178987,
7778734, 3542485, 2682288, 2129892, 3764867, 7375178, 557458, 7159240, …
… (共 256 個,完整表見 FIPS 204 Appendix B)
```
> 若用 Montgomery 乘法,zetas 通常以 Montgomery 形式儲存。ML-DSA 另有 `256⁻¹ = 8347681`、`q⁻¹ mod 2³² = 58728449` 等常數。

## D. 迴圈次數上限(避免無界迴圈,FIPS 204 Appendix C)

| 演算法 | 迭代上限 | XOF 輸出位元組上限 |
|---|---|---|
| ML-DSA.Sign_internal | 814 | — |
| RejBoundedPoly | 481 | 481 |
| RejNTTPoly | 298 | 894 |
| SampleInBall | 121 | 221 |

> 這些上限對應「正確實作下被觸及機率 ≤ 2⁻²⁵⁶」。實作**不建議**設上限;若設,不得低於上表,且觸頂時須銷毀所有中間結果並回傳固定錯誤值。

---

*整理完畢。如需把任一演算法展開成可執行的 Python/虛擬碼,或補上正確性/安全性證明草稿,再告訴我。*
