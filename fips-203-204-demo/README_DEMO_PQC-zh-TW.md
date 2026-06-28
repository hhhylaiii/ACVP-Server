# FIPS 203 (ML-KEM) & FIPS 204 (ML-DSA) ACVP 驗證示範流程

本文件說明如何使用 `GenValAppRunner` 與展示測試代理（Python harnesses）來進行 FIPS 203 (ML-KEM) 與 FIPS 204 (ML-DSA) 的 ACVP 出題與批改流程展示。

由於 PQC 演算法的特殊性，此處的 Python 腳本（`iut_mlkem.py` 與 `iut_mldsa.py`）會自動讀取產生的 `expectedResults.json` 並回答題目，以確保流程展示零依賴且完全自動化。

---

## 步驟 1 — 啟動 Orleans 伺服器
請確認 Orleans 伺服器正在運行（開啟一個終端機並保持運行）：
```bash
cd ~/Project/ACVP-Server/gen-val/samples/NIST.CVP.ACVTS.Orleans.ServerHost
dotnet run --console
```
Orleans 伺服器啟動成功後，即可開另一個終端機執行下方的展示指令。

---

## 步驟 2 — 執行示範流程 (於 `fips-203-204-demo` 資料夾中執行)

請先開啟另一個終端機並切換至 `fips-203-204-demo` 資料夾：
```bash
cd ~/Project/ACVP-Server/fips-203-204-demo
RUNNER=~/Project/ACVP-Server/gen-val/samples/GenValAppRunner/src
```

由於各模式共用相同的輸出檔名（如 `prompt.json` 等），建議在執行下一個演算法/模式前先清理舊的檔案，例如：
```bash
rm -f prompt.json internalProjection.json expectedResults.json responses.json validation.json
```

---

### A. FIPS 203: ML-KEM 示範

#### 1. ML-KEM KeyGen (密鑰產生)
```bash
# 清理舊檔案
rm -f prompt.json internalProjection.json expectedResults.json responses.json validation.json

# 1a. Generate (出題)
dotnet run --project "$RUNNER" -- -g registration_mlkem_keyGen.json

# 1b. Answer (作答)
python3 iut_mlkem.py prompt.json

# 1c. Validate (批改)
dotnet run --project "$RUNNER" -- -n internalProjection.json -b responses.json

# 1d. Check Disposition (查看結果)
python3 -c "import json;print('Disposition:', json.load(open('validation.json'))['disposition'])"
```
預期結果顯示：`Disposition: passed`

#### 2. ML-KEM EncapDecap (封裝/解封裝)
```bash
# 清理舊檔案
rm -f prompt.json internalProjection.json expectedResults.json responses.json validation.json

# 2a. Generate (出題)
dotnet run --project "$RUNNER" -- -g registration_mlkem_encapDecap.json

# 2b. Answer (作答)
python3 iut_mlkem.py prompt.json

# 2c. Validate (批改)
dotnet run --project "$RUNNER" -- -n internalProjection.json -b responses.json

# 2d. Check Disposition (查看結果)
python3 -c "import json;print('Disposition:', json.load(open('validation.json'))['disposition'])"
```
預期結果顯示：`Disposition: passed`

---

### B. FIPS 204: ML-DSA 示範

#### 1. ML-DSA KeyGen (密鑰產生)
```bash
# 清理舊檔案
rm -f prompt.json internalProjection.json expectedResults.json responses.json validation.json

# 1a. Generate (出題)
dotnet run --project "$RUNNER" -- -g registration_mldsa_keyGen.json

# 1b. Answer (作答)
python3 iut_mldsa.py prompt.json

# 1c. Validate (批改)
dotnet run --project "$RUNNER" -- -n internalProjection.json -b responses.json

# 1d. Check Disposition (查看結果)
python3 -c "import json;print('Disposition:', json.load(open('validation.json'))['disposition'])"
```
預期結果顯示：`Disposition: passed`

#### 2. ML-DSA SigGen (簽章產生)
```bash
# 清理舊檔案
rm -f prompt.json internalProjection.json expectedResults.json responses.json validation.json

# 2a. Generate (出題)
dotnet run --project "$RUNNER" -- -g registration_mldsa_sigGen.json

# 2b. Answer (作答)
python3 iut_mldsa.py prompt.json

# 2c. Validate (批改)
dotnet run --project "$RUNNER" -- -n internalProjection.json -b responses.json

# 2d. Check Disposition (查看結果)
python3 -c "import json;print('Disposition:', json.load(open('validation.json'))['disposition'])"
```
預期結果顯示：`Disposition: passed`

#### 3. ML-DSA SigVer (簽章驗證)
```bash
# 清理舊檔案
rm -f prompt.json internalProjection.json expectedResults.json responses.json validation.json

# 3a. Generate (出題)
dotnet run --project "$RUNNER" -- -g registration_mldsa_sigVer.json

# 3b. Answer (作答)
python3 iut_mldsa.py prompt.json

# 3c. Validate (批改)
dotnet run --project "$RUNNER" -- -n internalProjection.json -b responses.json

# 3d. Check Disposition (查看結果)
python3 -c "import json;print('Disposition:', json.load(open('validation.json'))['disposition'])"
```
預期結果顯示：`Disposition: passed`

---

## 嘗試讓它失敗 (證明它真的有在批改)

產生 `responses.json` 之後，可使用下方指令故意破壞第一個測試案例的答案並重新驗證：
```bash
python3 -c "import json;d=json.load(open('responses.json'));t=d['testGroups'][0]['tests'][0];t[list(k for k in t if k!='tcId')[0]]='00'*32;json.dump(d,open('responses.json','w'))"
dotnet run --project "$RUNNER" -- -n internalProjection.json -b responses.json
python3 -c "import json;print('Disposition:', json.load(open('validation.json'))['disposition'])"
```
此時 Disposition 應會變更為 `failed`，這證明伺服器端確實對 responses 進行了比對與批改。
