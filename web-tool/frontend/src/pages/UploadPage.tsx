import { useState, type ChangeEvent, type DragEvent } from 'react';
import { ApiError, pollJobUntilDone, startValidate } from '../api/client';
import type { Job, SafeError } from '../api/types';
import { IconFile, IconUpload } from '../components/icons';

export interface UploadPageProps {
  generateJob: Job;
  onValidated: (job: Job) => void;
  onBack: () => void;
}

function formatBytes(bytes: number): string {
  if (bytes < 1024) {
    return `${bytes} B`;
  }
  if (bytes < 1024 * 1024) {
    return `${(bytes / 1024).toFixed(1)} KB`;
  }
  return `${(bytes / 1024 / 1024).toFixed(1)} MB`;
}

/** US2 — upload the IUT-produced responses.json and poll the validate job. */
export function UploadPage({ generateJob, onValidated, onBack }: UploadPageProps) {
  const [file, setFile] = useState<File | null>(null);
  const [dragOver, setDragOver] = useState(false);
  const [working, setWorking] = useState(false);
  const [status, setStatus] = useState<string | null>(null);
  const [error, setError] = useState<SafeError | null>(null);

  const onFileChange = (e: ChangeEvent<HTMLInputElement>) => {
    setFile(e.target.files?.[0] ?? null);
    // Allow re-selecting the same file after removal.
    e.target.value = '';
  };

  const onDrop = (e: DragEvent) => {
    e.preventDefault();
    setDragOver(false);
    if (working) {
      return;
    }
    const dropped = e.dataTransfer.files?.[0];
    if (dropped) {
      setFile(dropped);
    }
  };

  const upload = async () => {
    if (!file) {
      return;
    }
    setWorking(true);
    setError(null);
    setStatus('Uploading…');
    try {
      const accepted = await startValidate(generateJob.jobId, file);
      setStatus('Validating…');
      const finished = await pollJobUntilDone(accepted.jobId, (job) =>
        setStatus(`Validating… (${job.status})`),
      );
      if (finished.status === 'Succeeded') {
        onValidated(finished);
      } else {
        setError(
          finished.error ?? {
            code: 'UNEXPECTED_ERROR',
            message: 'Validation failed. Please try again later.',
          },
        );
        setStatus(null);
      }
    } catch (e) {
      if (e instanceof ApiError && e.safeError) {
        setError(e.safeError);
      } else {
        setError({
          code: 'NETWORK',
          message: 'Unable to reach the server. Make sure the service is running.',
        });
      }
      setStatus(null);
    } finally {
      setWorking(false);
    }
  };

  return (
    <section className="card">
      <header className="card-header">
        <span className="overline">Step 3</span>
        <h2>Upload Responses</h2>
        <p className="hint">
          Upload the responses.json your module produced for prompt.json. To preview the workflow,
          you can also upload the example-responses.json included in the prompt package (for
          demonstration — it always passes).
        </p>
      </header>

      <div className="field">
        <label
          className={`dropzone ${dragOver ? 'dropzone-active' : ''} ${working ? 'dropzone-disabled' : ''}`}
          onDragOver={(e) => {
            e.preventDefault();
            if (!working) {
              setDragOver(true);
            }
          }}
          onDragLeave={() => setDragOver(false)}
          onDrop={onDrop}
        >
          <input
            id="responses-file"
            type="file"
            accept=".json,application/json"
            className="visually-hidden"
            onChange={onFileChange}
            disabled={working}
          />
          <IconUpload className="dropzone-icon" />
          <span className="dropzone-title">
            {file ? 'Click or drop to replace the file' : 'Drop the file here, or click to browse'}
          </span>
          <span className="dropzone-sub">responses.json (JSON format)</span>
        </label>

        {file && (
          <div className="file-chip">
            <IconFile className="file-icon" />
            <span className="file-name mono">{file.name}</span>
            <span className="file-size">{formatBytes(file.size)}</span>
            <button
              type="button"
              className="file-remove"
              onClick={() => setFile(null)}
              disabled={working}
              aria-label="Remove file"
            >
              Remove
            </button>
          </div>
        )}
      </div>

      {error && (
        <div className="error-box" role="alert">
          <strong>{error.code}</strong>: {error.message}
          {error.tcId != null && <div>Test case tcId: {error.tcId}</div>}
          {error.field && <div>Field: {error.field}</div>}
          {error.hint && <div>Hint: {error.hint}</div>}
        </div>
      )}

      {status && (
        <p className="loading-line">
          <span className="spinner" aria-hidden />
          {status}
        </p>
      )}

      <div className="actions">
        <button type="button" className="btn btn-secondary" onClick={onBack} disabled={working}>
          Back
        </button>
        <button
          type="button"
          className="btn btn-primary"
          onClick={upload}
          disabled={!file || working}
        >
          Upload & Validate
        </button>
      </div>
    </section>
  );
}
