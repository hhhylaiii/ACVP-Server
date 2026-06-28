# FIPS 203 & 204 CLI Demo Workflow Walkthrough

我們已經成功為 FIPS 203 (ML-KEM) 與 FIPS 204 (ML-DSA) 建立了完整的 ACVP CLI 示範流程。

## 建立與新增的檔案

我們在 [fips-203-204-demo/](../../fips-203-204-demo) 目錄下新增了以下檔案：

1. **註冊設定檔（Registrations）**
   - [registration_mlkem_keyGen.json](../../fips-203-204-demo/registration_mlkem_keyGen.json) (ML-KEM 密鑰產生)
   - [registration_mlkem_encapDecap.json](../../fips-203-204-demo/registration_mlkem_encapDecap.json) (ML-KEM 封裝與解封裝)
   - [registration_mldsa_keyGen.json](../../fips-203-204-demo/registration_mldsa_keyGen.json) (ML-DSA 密鑰產生)
   - [registration_mldsa_sigGen.json](../../fips-203-204-demo/registration_mldsa_sigGen.json) (ML-DSA 簽章產生)
   - [registration_mldsa_sigVer.json](../../fips-203-204-demo/registration_mldsa_sigVer.json) (ML-DSA 簽章驗證)

2. **IUT 測試代理腳本（Python Harnesses）**
   - [iut_mlkem.py](../../fips-203-204-demo/iut_mlkem.py) (對應 ML-KEM 的自動解題代理)
   - [iut_mldsa.py](../../fips-203-204-demo/iut_mldsa.py) (對應 ML-DSA 的自動解題代理)

3. **使用指南說明文件**
   - [README_DEMO_PQC-zh-TW.md](../../fips-203-204-demo/README_DEMO_PQC-zh-TW.md) (包含各個演算法與模式的執行指令與結果確認)

---

## 驗證結果

我們針對全部五種 PQC 模式執行了完整端到端測試，所有測試均已通過：

1. **ML-KEM keyGen**：產生 75 個測試案例，批改結果為 `passed`。
2. **ML-KEM encapDecap**：產生 165 個測試案例，批改結果為 `passed`。
3. **ML-DSA keyGen**：產生 75 個測試案例，批改結果為 `passed`。
4. **ML-DSA sigGen**：產生 360 個測試案例，批改結果為 `passed`。
5. **ML-DSA sigVer**：產生 180 個測試案例，批改結果為 `passed`。

各個模式的執行指令、清除舊有快取以及結果比對，都已完整登載於說明的 [README_DEMO_PQC-zh-TW.md](../../fips-203-204-demo/README_DEMO_PQC-zh-TW.md) 中。
