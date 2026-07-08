import type {
  AlgorithmConfiguration,
  Capabilities,
  CheckResult,
  Job,
  SafeError,
  ValidationReport,
} from './types';

/** Error carrying the server's SafeError payload for precise UI display. */
export class ApiError extends Error {
  readonly status: number;
  readonly safeError: SafeError | null;

  constructor(status: number, safeError: SafeError | null) {
    super(safeError?.message ?? `Request failed with status ${status}`);
    this.status = status;
    this.safeError = safeError;
  }
}

async function parseSafeError(response: Response): Promise<never> {
  let safeError: SafeError | null = null;
  try {
    safeError = (await response.json()) as SafeError;
  } catch {
    // Non-JSON error body; keep safeError null.
  }
  throw new ApiError(response.status, safeError);
}

async function getJson<T>(url: string): Promise<T> {
  const response = await fetch(url);
  if (!response.ok) {
    return parseSafeError(response);
  }
  return (await response.json()) as T;
}

async function postJson<T>(url: string, body: unknown): Promise<T> {
  const response = await fetch(url, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  });
  if (!response.ok) {
    return parseSafeError(response);
  }
  return (await response.json()) as T;
}

export function getCapabilities(): Promise<Capabilities> {
  return getJson('/api/capabilities');
}

export function checkConfiguration(config: AlgorithmConfiguration): Promise<CheckResult> {
  return postJson('/api/check', config);
}

export function startGenerate(config: AlgorithmConfiguration): Promise<Job> {
  return postJson('/api/generate', config);
}

export function getJob(jobId: string): Promise<Job> {
  return getJson(`/api/jobs/${encodeURIComponent(jobId)}`);
}

export function promptPackageUrl(jobId: string): string {
  return `/api/jobs/${encodeURIComponent(jobId)}/prompt-package`;
}

export async function startValidate(jobId: string, responsesFile: File): Promise<Job> {
  const form = new FormData();
  form.append('jobId', jobId);
  form.append('responses', responsesFile);
  const response = await fetch('/api/validate', { method: 'POST', body: form });
  if (!response.ok) {
    return parseSafeError(response);
  }
  return (await response.json()) as Job;
}

export function getReport(jobId: string): Promise<ValidationReport> {
  return getJson(`/api/jobs/${encodeURIComponent(jobId)}/report`);
}

export function validationJsonUrl(jobId: string): string {
  return `/api/jobs/${encodeURIComponent(jobId)}/validation-json`;
}

/** Polls a job until it reaches a terminal state (Succeeded / Failed). */
export async function pollJobUntilDone(
  jobId: string,
  onUpdate?: (job: Job) => void,
  intervalMs = 1500,
): Promise<Job> {
  // Deliberately unbounded: long ML-DSA runs are expected; the UI stays responsive.
  for (;;) {
    const job = await getJob(jobId);
    onUpdate?.(job);
    if (job.status === 'Succeeded' || job.status === 'Failed') {
      return job;
    }
    await new Promise((resolve) => setTimeout(resolve, intervalMs));
  }
}
