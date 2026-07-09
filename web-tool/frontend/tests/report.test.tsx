import { render, screen } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ReportPage } from '../src/pages/ReportPage';
import type { Job, ValidationReport } from '../src/api/types';

const validateJob: Job = {
  jobId: 'validate-1',
  vsId: 101,
  kind: 'validate',
  status: 'Succeeded',
  createdAt: new Date().toISOString(),
};

function jsonResponse(body: unknown): Response {
  return new Response(JSON.stringify(body), {
    status: 200,
    headers: { 'Content-Type': 'application/json' },
  });
}

beforeEach(() => {
  vi.stubGlobal('fetch', vi.fn());
});

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('ReportPage', () => {
  it('renders a passed report with summary counts and download link', async () => {
    const report: ValidationReport = {
      jobId: 'validate-1',
      disposition: 'passed',
      summary: { total: 3, passed: 3, failed: 0 },
      cases: [
        { tcId: 1, passed: true },
        { tcId: 2, passed: true },
        { tcId: 3, passed: true },
      ],
    };
    vi.mocked(fetch).mockResolvedValueOnce(jsonResponse(report));

    render(<ReportPage validateJob={validateJob} onRestart={vi.fn()} onUploadAgain={vi.fn()} />);

    expect(await screen.findByText(/通過 \(passed\)/)).toBeInTheDocument();
    expect(screen.getByText('測試案例總數').previousElementSibling).toHaveTextContent('3');

    const download = screen.getByRole('link', { name: /下載 validation\.json/ });
    expect(download).toHaveAttribute('href', '/api/jobs/validate-1/validation-json');
  });

  it('highlights failing cases with their reasons', async () => {
    const report: ValidationReport = {
      jobId: 'validate-1',
      disposition: 'failed',
      summary: { total: 2, passed: 1, failed: 1 },
      cases: [
        { tcId: 1, passed: true },
        { tcId: 2, passed: false, reason: 'EK does not match' },
      ],
    };
    vi.mocked(fetch).mockResolvedValueOnce(jsonResponse(report));

    render(<ReportPage validateJob={validateJob} onRestart={vi.fn()} onUploadAgain={vi.fn()} />);

    expect(await screen.findByText(/未通過 \(failed\)/)).toBeInTheDocument();
    expect(screen.getByText('EK does not match')).toBeInTheDocument();

    const failingRow = screen.getByText('EK does not match').closest('tr');
    expect(failingRow).toHaveClass('case-failed');
  });
});
