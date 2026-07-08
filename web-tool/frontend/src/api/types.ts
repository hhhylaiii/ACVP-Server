// Typed mirror of specs/001-validation-web-tool/contracts/openapi.yaml

export type Algorithm = 'ML-KEM' | 'ML-DSA';

export type Mode = 'keyGen' | 'encapDecap' | 'sigGen' | 'sigVer';

export interface AlgorithmCapability {
  algorithm: Algorithm;
  modes: Mode[];
  parameterSets: string[];
}

export interface Capabilities {
  algorithms: AlgorithmCapability[];
}

export interface AlgorithmConfiguration {
  algorithm: Algorithm;
  mode: Mode;
  parameterSets: string[];
  advancedOptions?: Record<string, unknown> | null;
}

export interface CheckResult {
  valid: boolean;
  messages: string[];
}

export type JobKind = 'generate' | 'validate';

export type JobStatus = 'Queued' | 'Running' | 'Succeeded' | 'Failed';

export interface SafeError {
  code: string;
  message: string;
  tcId?: number | null;
  field?: string | null;
  hint?: string | null;
}

export interface Job {
  jobId: string;
  vsId: number;
  kind: JobKind;
  status: JobStatus;
  createdAt: string;
  completedAt?: string | null;
  error?: SafeError | null;
}

export interface ValidationSummary {
  total: number;
  passed: number;
  failed: number;
}

export interface ValidationCase {
  tcId: number;
  passed: boolean;
  reason?: string | null;
}

export interface ValidationReport {
  jobId: string;
  disposition: 'passed' | 'failed';
  summary: ValidationSummary;
  cases: ValidationCase[];
}
