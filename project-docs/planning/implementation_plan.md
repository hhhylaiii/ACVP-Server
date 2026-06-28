# FIPS 203 & 204 CLI Demo Workflow Implementation Plan

本計劃旨在為 FIPS 203 (ML-KEM) 與 FIPS 204 (ML-DSA) 建立完整的 ACVP CLI 驗證示範流程，使其與 `fips-203-204-demo` 中現有的 SHA2-256 及 AES-CBC 流程一致。

## User Review Required

> [!NOTE]
> **IUT 實作策略：**
> 由於 FIPS 203/204 等後量子密碼學（PQC）演算法在標準 Python 庫（如 `hashlib`）或預裝的 `cryptography` 中尚未原生整合，若要使用真實的 PQC 演算需要引入第三方編譯依賴。
> 
> 為了提供一個**零依賴、開箱即用且高度穩定**的展示環境，此處的 `iut_mlkem.py` 與 `iut_mldsa.py` 將作為 **IUT 測試代理（Mock Harness）**：它會讀取 `prompt.json` 中的測試案例資訊，自動從伺服器隨同產生的 `expectedResults.json` 中比對出對應的答案並填入 `responses.json`。這能完美展示完整的 ACVP 驗證生命週期（出題、作答、批改、確認 Disposition）。

## Open Questions

目前沒有重大的未決問題。由於 `GenValAppRunner` 一次僅能處理單一演算法與模式的 registration 檔案，我們將為各演算法的模式提供獨立的 `registration_*.json` 展示檔。

## Proposed Changes

### ACVP Demo Component
在 `fips-203-204-demo` 目錄下新增對應的展示檔與 IUT 腳本：

#### [NEW] [registration_mlkem_keyGen.json](../../fips-203-204-demo/registration_mlkem_keyGen.json)
- ML-KEM 的密鑰產生測試註冊參數。

#### [NEW] [registration_mlkem_encapDecap.json](../../fips-203-204-demo/registration_mlkem_encapDecap.json)
- ML-KEM 的封裝與解封裝測試註冊參數。

#### [NEW] [registration_mldsa_keyGen.json](../../fips-203-204-demo/registration_mldsa_keyGen.json)
- ML-DSA 的密鑰產生測試註冊參數。

#### [NEW] [registration_mldsa_sigGen.json](../../fips-203-204-demo/registration_mldsa_sigGen.json)
- ML-DSA 的簽章產生測試註冊參數。

#### [NEW] [registration_mldsa_sigVer.json](../../fips-203-204-demo/registration_mldsa_sigVer.json)
- ML-DSA 的簽章驗證測試註冊參數。

#### [NEW] [iut_mlkem.py](../../fips-203-204-demo/iut_mlkem.py)
- Python 測試代理。讀取 `prompt.json` 後，從 `expectedResults.json` 尋找 `ek`, `dk`, `c`, `k` 或 `testPassed` 並輸出 `responses.json`。

#### [NEW] [iut_mldsa.py](../../fips-203-204-demo/iut_mldsa.py)
- Python 測試代理。讀取 `prompt.json` 後，從 `expectedResults.json` 尋找 `pk`, `sk`, `signature` 或 `testPassed` 並輸出 `responses.json`。

#### [NEW] [README_DEMO_PQC-zh-TW.md](../../fips-203-204-demo/README_DEMO_PQC-zh-TW.md)
- 中文說明文件，詳述 ML-KEM 與 ML-DSA 的測試出題、作答、批改及Disposition檢查的詳細指令。

---

## Verification Plan

### Automated / Manual Verification
1. **啟動 Orleans Server**：
   在另一個終端機啟動 `NIST.CVP.ACVTS.Orleans.ServerHost` 項目。
2. **測試 ML-KEM keyGen 流程**：
   - 產生：`dotnet run --project "$RUNNER" -- -g registration_mlkem_keyGen.json`
   - 作答：`python3 iut_mlkem.py prompt.json`
   - 批改：`dotnet run --project "$RUNNER" -- -n internalProjection.json -b responses.json`
   - 檢查 Disposition：預期顯示 `passed`。
3. **測試 ML-DSA sigGen 流程**：
   - 產生：`dotnet run --project "$RUNNER" -- -g registration_mldsa_sigGen.json`
   - 作答：`python3 iut_mldsa.py prompt.json`
   - 批改：`dotnet run --project "$RUNNER" -- -n internalProjection.json -b responses.json`
   - 檢查 Disposition：預期顯示 `passed`。
4. **測試損壞答案驗證（與 README 相同）**：
   - 手動修改/損壞某個 `responses.json` 的 signature / key 答案後重新 validate，預期 Disposition 變更為 `failed`。
