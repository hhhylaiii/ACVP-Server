# 欄位對照表 Field Mapping — FIPS 203 (ML-KEM) / FIPS 204 (ML-DSA)

本文件列出每個支援模式的 **prompt 輸入欄位**（貴模組收到的題目）與 **response 必填輸出欄位**
（貴模組必須回答的欄位），以及各欄位的編碼與位元組長度。

This document enumerates, per supported mode, the prompt input fields your module
receives and the response output fields it must produce, with encoding and byte length.

**共通規則 (Common rules)**

- 所有二進位值一律為**小寫十六進位字串**（hex，無 `0x` 前綴）。All binary values are lowercase hex strings.
- `responses.json` 的頂層 `vsId`、`algorithm`、`revision`（以及 `mode`、`isSample` 若存在）必須與 `prompt.json` 相同。
- 每個作答的測試案例都要帶原 `tcId`；不得新增 prompt 中不存在的 `tcId`。
- 長度隨參數集而異，詳見各表。Lengths depend on the parameter set.

---

## ML-KEM keyGen（FIPS 203 金鑰產生）

**Prompt 輸入欄位 (per test case)**

| 欄位 | 編碼 | 長度 (bytes) | 說明 |
|------|------|--------------|------|
| `d` | hex | 32 | 金鑰產生種子 d |
| `z` | hex | 32 | 金鑰產生種子 z |

**Response 必填欄位 (per test case)**

| 欄位 | 編碼 | 長度 (bytes) | 說明 |
|------|------|--------------|------|
| `ek` | hex | 512: 800 / 768: 1184 / 1024: 1568 | encapsulation key（公鑰） |
| `dk` | hex | 512: 1632 / 768: 2400 / 1024: 3168 | decapsulation key（私鑰） |

---

## ML-KEM encapDecap（FIPS 203 封裝/解封裝）

測試群組依 `function` 分為四種；必填欄位隨 function 而異。

### function = `encapsulation`

| Prompt 欄位 | 編碼 | 長度 (bytes) | 說明 |
|------|------|--------------|------|
| `ek` | hex | 同上 ek | 對方公鑰 |
| `m`  | hex | 32 | 封裝隨機值 |

| Response 欄位 | 編碼 | 長度 (bytes) | 說明 |
|------|------|--------------|------|
| `c` | hex | 512: 768 / 768: 1088 / 1024: 1568 | 密文 ciphertext |
| `k` | hex | 32 | 共享金鑰 shared secret |

### function = `decapsulation`

| Prompt 欄位 | 編碼 | 說明 |
|------|------|------|
| `dk`（群組層）| hex | 解封裝金鑰 |
| `c` | hex | 待解封裝密文 |

| Response 欄位 | 編碼 | 長度 (bytes) | 說明 |
|------|------|--------------|------|
| `k` | hex | 32 | 解出的共享金鑰 |

### function = `encapsulationKeyCheck` / `decapsulationKeyCheck`

| Prompt 欄位 | 編碼 | 說明 |
|------|------|------|
| `ek` 或 `dk` | hex | 待檢查的金鑰 |

| Response 欄位 | 型別 | 說明 |
|------|------|------|
| `testPassed` | boolean | 金鑰檢查是否通過 |

---

## ML-DSA keyGen（FIPS 204 金鑰產生）

**Prompt 輸入欄位 (per test case)**

| 欄位 | 編碼 | 長度 (bytes) | 說明 |
|------|------|--------------|------|
| `seed` | hex | 32 | 金鑰產生種子 ξ |

**Response 必填欄位 (per test case)**

| 欄位 | 編碼 | 長度 (bytes) | 說明 |
|------|------|--------------|------|
| `pk` | hex | 44: 1312 / 65: 1952 / 87: 2592 | 公鑰 |
| `sk` | hex | 44: 2560 / 65: 4032 / 87: 4896 | 私鑰 |

---

## ML-DSA sigGen（FIPS 204 簽章產生）

Prompt 欄位依群組設定（`deterministic`、`preHash`、`signatureInterface`、`externalMu`）而異：

| Prompt 欄位 | 編碼 | 說明 |
|------|------|------|
| `message` 或 `mu` | hex | 待簽訊息（externalMu 群組給 `mu`） |
| `sk` | hex | 簽章私鑰 |
| `rnd` | hex (32 bytes) | 非決定性簽章的隨機值（deterministic 群組無此欄位） |
| `context` | hex | 簽章 context（external interface 群組） |
| `hashAlg` | string | preHash 群組使用的雜湊演算法 |

**Response 必填欄位 (per test case)**

| 欄位 | 編碼 | 長度 (bytes) | 說明 |
|------|------|--------------|------|
| `signature` | hex | 44: 2420 / 65: 3309 / 87: 4627 | 簽章值 |

---

## ML-DSA sigVer（FIPS 204 簽章驗證）

| Prompt 欄位 | 編碼 | 說明 |
|------|------|------|
| `pk` | hex | 驗章公鑰 |
| `message` 或 `mu` | hex | 原訊息 |
| `signature` | hex | 待驗簽章 |
| `context` / `hashAlg` | hex / string | 視群組設定出現 |

**Response 必填欄位 (per test case)**

| 欄位 | 型別 | 說明 |
|------|------|------|
| `testPassed` | boolean | 簽章驗證是否通過（注意：部分題目故意給壞簽章，正確答案是 `false`） |

---

## 常見錯誤 (Common pitfalls)

1. `vsId` / `algorithm` / `mode` 沒有照抄 prompt → 上傳被判 `MISMATCHED_VECTORSET`。
2. 漏答某個 `tcId` 的必填欄位 → `MISSING_FIELD`，錯誤訊息會指出 tcId 與欄位名。
3. 多答了 prompt 沒有的 `tcId` → `UNKNOWN_TCID`。
4. hex 大小寫皆可被解析，但建議一律小寫；不得含空白或 `0x` 前綴。
