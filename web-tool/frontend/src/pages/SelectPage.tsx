import { useEffect, useMemo, useState } from 'react';
import { getCapabilities } from '../api/client';
import type { Algorithm, AlgorithmConfiguration, Capabilities, Mode } from '../api/types';
import { ALGORITHM_INFO, MODE_INFO, PARAMETER_SET_INFO } from '../domain/algorithmInfo';
import { IconKey, IconSignature } from '../components/icons';

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
    setParameterSets((prev) => (prev.includes(ps) ? prev.filter((p) => p !== ps) : [...prev, ps]));
  };

  const canContinue = algorithm !== null && mode !== null && parameterSets.length > 0;

  if (loadError) {
    return (
      <section className="card">
        <header className="card-header">
          <span className="overline">Step 1</span>
          <h2>Select Algorithm</h2>
        </header>
        <div className="error-box">Failed to load supported algorithms: {loadError}</div>
      </section>
    );
  }

  if (!capabilities) {
    return (
      <section className="card">
        <header className="card-header">
          <span className="overline">Step 1</span>
          <h2>Select Algorithm</h2>
        </header>
        <p className="loading-line">
          <span className="spinner" aria-hidden />
          Loading supported algorithms…
        </p>
      </section>
    );
  }

  return (
    <section className="card">
      <header className="card-header">
        <span className="overline">Step 1</span>
        <h2>Select Algorithm</h2>
        <p className="hint">
          Only FIPS 203 / FIPS 204 combinations supported by this tool are listed — unsupported
          options are unreachable.
        </p>
      </header>

      <div className="field">
        <span className="field-label">Algorithm</span>
        <div className="algo-grid">
          {capabilities.algorithms.map((a) => {
            const info = ALGORITHM_INFO[a.algorithm];
            const selected = algorithm === a.algorithm;
            return (
              <button
                key={a.algorithm}
                type="button"
                className={`algo-card ${selected ? 'algo-card-selected' : ''}`}
                onClick={() => selectAlgorithm(a.algorithm)}
              >
                <span className="algo-head">
                  <span className="algo-icon">
                    {a.algorithm === 'ML-KEM' ? <IconKey /> : <IconSignature />}
                  </span>
                  <span className="algo-name">{a.algorithm}</span>
                  <span className="tag">{info.standard}</span>
                </span>
                <span className="algo-title">{info.title}</span>
                <span className="algo-desc">{info.description}</span>
              </button>
            );
          })}
        </div>
      </div>

      {current && (
        <div className="field">
          <span className="field-label">Mode</span>
          <div className="choice-grid">
            {current.modes.map((m) => (
              <button
                key={m}
                type="button"
                aria-label={m}
                className={`choice ${mode === m ? 'choice-selected' : ''}`}
                onClick={() => setMode(m)}
              >
                <span className="choice-title mono">{m}</span>
                <span className="choice-sub">{MODE_INFO[m]}</span>
              </button>
            ))}
          </div>
        </div>
      )}

      {current && mode && (
        <div className="field">
          <span className="field-label">
            Parameter Sets<span className="field-label-note">multi-select</span>
          </span>
          <div className="choice-grid">
            {current.parameterSets.map((ps) => {
              const info = PARAMETER_SET_INFO[ps];
              return (
                <button
                  key={ps}
                  type="button"
                  aria-label={ps}
                  className={`choice ${parameterSets.includes(ps) ? 'choice-selected' : ''}`}
                  onClick={() => toggleParameterSet(ps)}
                >
                  <span className="choice-title mono">{ps}</span>
                  {info && (
                    <span className="choice-sub">
                      <span className="cat-badge">Cat {info.category}</span>
                      {info.strength}
                    </span>
                  )}
                </button>
              );
            })}
          </div>
          <p className="field-note">
            Cat is the NIST post-quantum security category (1–5); a higher category means a stronger
            security level.
          </p>
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
          Next: Generate Test Vectors
        </button>
        {canContinue ? (
          <span className="hint">
            Selected <strong>{algorithm}</strong> / <strong>{mode}</strong> / {parameterSets.length}{' '}
            parameter set{parameterSets.length > 1 ? 's' : ''}
          </span>
        ) : (
          <span className="hint">
            Choose an algorithm, a mode, and at least one parameter set to continue.
          </span>
        )}
      </div>
    </section>
  );
}
