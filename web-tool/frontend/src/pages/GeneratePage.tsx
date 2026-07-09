import { useState } from 'react';
import { ApiError, pollJobUntilDone, promptPackageUrl, startGenerate } from '../api/client';
import type { AlgorithmConfiguration, Job } from '../api/types';
import { ALGORITHM_INFO, MODE_INFO } from '../domain/algorithmInfo';
import { IconCheck, IconFile } from '../components/icons';

export interface GeneratePageProps {
  configuration: AlgorithmConfiguration;
  job: Job | null;
  onJobChanged: (job: Job | null) => void;
  onBack: () => void;
  onContinue: () => void;
}

/** US1 — start the generate job, poll status, offer the prompt-package download. */
export function GeneratePage({
  configuration,
  job,
  onJobChanged,
  onBack,
  onContinue,
}: GeneratePageProps) {
  const [working, setWorking] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const generate = async () => {
    setWorking(true);
    setError(null);
    try {
      const accepted = await startGenerate(configuration);
      onJobChanged(accepted);
      const finished = await pollJobUntilDone(accepted.jobId, onJobChanged);
      if (finished.status === 'Failed') {
        setError(finished.error?.message ?? 'Generation failed. Please try again later.');
      }
    } catch (e) {
      setError(
        e instanceof ApiError
          ? e.message
          : 'Unable to reach the server. Make sure the service is running.',
      );
      onJobChanged(null);
    } finally {
      setWorking(false);
    }
  };

  const succeeded = job?.status === 'Succeeded';
  const info = ALGORITHM_INFO[configuration.algorithm];

  return (
    <section className="card">
      <header className="card-header">
        <span className="overline">Step 2</span>
        <h2>Generate Test Vectors</h2>
      </header>

      <div className="config-summary">
        <div className="config-item">
          <span className="config-label">Algorithm</span>
          <span className="config-value">
            <span className="mono">{configuration.algorithm}</span>
            <span className="tag">{info.standard}</span>
          </span>
        </div>
        <div className="config-item">
          <span className="config-label">Mode</span>
          <span className="config-value">
            <span className="mono">{configuration.mode}</span>
            <span className="config-note">{MODE_INFO[configuration.mode]}</span>
          </span>
        </div>
        <div className="config-item">
          <span className="config-label">Parameter Sets</span>
          <span className="config-value config-chips">
            {configuration.parameterSets.map((ps) => (
              <span key={ps} className="chip mono">
                {ps}
              </span>
            ))}
          </span>
        </div>
      </div>

      {error && <div className="error-box">{error}</div>}

      {!job && (
        <div className="actions">
          <button type="button" className="btn btn-secondary" onClick={onBack} disabled={working}>
            Back
          </button>
          <button type="button" className="btn btn-primary" onClick={generate} disabled={working}>
            {working ? 'Generating…' : 'Generate Test Vectors'}
          </button>
        </div>
      )}

      {job && (
        <div className="job-panel">
          <div className="job-status-row">
            <span>
              Job status:
              <span
                className={`badge ${
                  succeeded
                    ? 'badge-succeeded'
                    : job.status === 'Failed'
                      ? 'badge-failed'
                      : 'badge-running'
                }`}
              >
                {job.status}
              </span>
            </span>
            {!succeeded && job.status !== 'Failed' && (
              <span className="job-progress">
                <span className="spinner" aria-hidden />
                Job in progress… Large vector sets can take several minutes.
              </span>
            )}
          </div>
          <dl className="job-meta">
            <div className="job-meta-item">
              <dt>Job ID</dt>
              <dd className="mono">{job.jobId}</dd>
            </div>
            <div className="job-meta-item">
              <dt>Vector Set (vsId)</dt>
              <dd className="mono">{job.vsId}</dd>
            </div>
            <div className="job-meta-item">
              <dt>Created</dt>
              <dd>{new Date(job.createdAt).toLocaleString()}</dd>
            </div>
          </dl>
        </div>
      )}

      {succeeded && (
        <>
          <div className="success-panel">
            <span className="success-icon">
              <IconCheck />
            </span>
            <div>
              <strong>Test vectors are ready.</strong>
              <p className="hint">
                The prompt package contains the files below. Download it and hand it to the
                implementation under test (IUT):
              </p>
              <ul className="package-files">
                <li>
                  <IconFile className="file-icon" />
                  <code>prompt.json</code>
                  <span className="file-desc">Test vectors (the questions)</span>
                </li>
                <li>
                  <IconFile className="file-icon" />
                  <code>example-responses.json</code>
                  <span className="file-desc">Response format example</span>
                </li>
                <li>
                  <IconFile className="file-icon" />
                  <code>README</code>
                  <span className="file-desc">Workflow and field reference</span>
                </li>
              </ul>
            </div>
          </div>
          <div className="actions">
            <a className="btn btn-primary" href={promptPackageUrl(job.jobId)} download>
              Download Prompt Package
            </a>
            <button type="button" className="btn btn-primary" onClick={onContinue}>
              Next: Upload Responses
            </button>
            <button type="button" className="btn btn-secondary" onClick={onBack}>
              Change Selection
            </button>
          </div>
        </>
      )}

      {job?.status === 'Failed' && (
        <div className="actions">
          <button type="button" className="btn btn-secondary" onClick={onBack}>
            Back
          </button>
          <button
            type="button"
            className="btn btn-primary"
            onClick={() => {
              onJobChanged(null);
              void generate();
            }}
          >
            Retry
          </button>
        </div>
      )}
    </section>
  );
}
