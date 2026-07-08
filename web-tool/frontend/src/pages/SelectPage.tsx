import { useEffect, useMemo, useState } from 'react';
import { getCapabilities } from '../api/client';
import type { Algorithm, AlgorithmConfiguration, Capabilities, Mode } from '../api/types';

export interface SelectPageProps {
  onConfirmed: (config: AlgorithmConfiguration) => void;
}

/** US1 — selection matrix fed by GET /api/capabilities; unsupported combos are unreachable. */
export function SelectPage({ onConfirmed }: SelectPageProps) {
  const [capabilities, setCapabilities] = useState<Capabilities | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [algorithm, setAlgorithm] = useState<Algorithm | null>(null);
  const [mode, setMode] = useState<Mode | null>(null);
  const [parameterSets, setParameterSets] = useState<string[]>([]);

  useEffect(() => {
    getCapabilities()
      .then(setCapabilities)
      .catch((error: Error) => setLoadError(error.message));
  }, []);

  const current = useMemo(
    () => capabilities?.algorithms.find((a) => a.algorithm === algorithm) ?? null,
    [capabilities, algorithm],
  );

  const selectAlgorithm = (next: Algorithm) => {
    setAlgorithm(next);
    setMode(null);
    setParameterSets([]);
  };

  const toggleParameterSet = (ps: string) => {
    setParameterSets((prev) =>
      prev.includes(ps) ? prev.filter((p) => p !== ps) : [...prev, ps],
    );
  };

  const canContinue = algorithm !== null && mode !== null && parameterSets.length > 0;

  if (loadError) {
    return (
      <section className="card">
        <h2>選擇演算法</h2>
        <div className="error-box">無法載入支援清單：{loadError}</div>
      </section>
    );
  }

  if (!capabilities) {
    return (
      <section className="card">
        <h2>選擇演算法</h2>
        <p>
          <span className="spinner" aria-hidden />
          載入支援的演算法中…
        </p>
      </section>
    );
  }

  return (
    <section className="card">
      <h2>選擇演算法</h2>
      <p className="hint">僅列出本工具支援的 FIPS 203 / FIPS 204 組合，無法選到不支援的選項。</p>

      <div className="field">
        <span className="field-label">演算法</span>
        <div className="choice-grid">
          {capabilities.algorithms.map((a) => (
            <button
              key={a.algorithm}
              type="button"
              className={`choice ${algorithm === a.algorithm ? 'choice-selected' : ''}`}
              onClick={() => selectAlgorithm(a.algorithm)}
            >
              {a.algorithm}（{a.algorithm === 'ML-KEM' ? 'FIPS 203 金鑰封裝' : 'FIPS 204 數位簽章'}）
            </button>
          ))}
        </div>
      </div>

      {current && (
        <div className="field">
          <span className="field-label">模式</span>
          <div className="choice-grid">
            {current.modes.map((m) => (
              <button
                key={m}
                type="button"
                className={`choice ${mode === m ? 'choice-selected' : ''}`}
                onClick={() => setMode(m)}
              >
                {m}
              </button>
            ))}
          </div>
        </div>
      )}

      {current && mode && (
        <div className="field">
          <span className="field-label">參數集（可複選）</span>
          <div className="choice-grid">
            {current.parameterSets.map((ps) => (
              <button
                key={ps}
                type="button"
                className={`choice ${parameterSets.includes(ps) ? 'choice-selected' : ''}`}
                onClick={() => toggleParameterSet(ps)}
              >
                {ps}
              </button>
            ))}
          </div>
        </div>
      )}

      <div className="actions">
        <button
          type="button"
          className="btn btn-primary"
          disabled={!canContinue}
          onClick={() =>
            onConfirmed({
              algorithm: algorithm!,
              mode: mode!,
              parameterSets,
            })
          }
        >
          下一步：產生測試向量
        </button>
        {!canContinue && <span className="hint">請依序選擇演算法、模式與至少一個參數集。</span>}
      </div>
    </section>
  );
}
