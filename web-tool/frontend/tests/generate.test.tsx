import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { SelectPage } from '../src/pages/SelectPage';
import { GeneratePage } from '../src/pages/GeneratePage';
import type { Capabilities, Job } from '../src/api/types';

const capabilities: Capabilities = {
  algorithms: [
    {
      algorithm: 'ML-KEM',
      modes: ['keyGen', 'encapDecap'],
      parameterSets: ['ML-KEM-512', 'ML-KEM-768', 'ML-KEM-1024'],
    },
    {
      algorithm: 'ML-DSA',
      modes: ['keyGen', 'sigGen', 'sigVer'],
      parameterSets: ['ML-DSA-44', 'ML-DSA-65', 'ML-DSA-87'],
    },
  ],
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

describe('SelectPage', () => {
  it('blocks continuing until algorithm, mode and a parameter set are chosen', async () => {
    vi.mocked(fetch).mockResolvedValueOnce(jsonResponse(capabilities));
    const onConfirmed = vi.fn();
    const user = userEvent.setup();

    render(<SelectPage onConfirmed={onConfirmed} />);
    const continueButton = await screen.findByRole('button', { name: /下一步/ });
    expect(continueButton).toBeDisabled();

    await user.click(screen.getByRole('button', { name: /ML-KEM/ }));
    expect(continueButton).toBeDisabled();

    await user.click(screen.getByRole('button', { name: 'keyGen' }));
    expect(continueButton).toBeDisabled();

    await user.click(screen.getByRole('button', { name: 'ML-KEM-768' }));
    expect(continueButton).toBeEnabled();

    await user.click(continueButton);
    expect(onConfirmed).toHaveBeenCalledWith({
      algorithm: 'ML-KEM',
      mode: 'keyGen',
      parameterSets: ['ML-KEM-768'],
    });
  });

  it('only offers modes belonging to the chosen algorithm', async () => {
    vi.mocked(fetch).mockResolvedValueOnce(jsonResponse(capabilities));
    const user = userEvent.setup();

    render(<SelectPage onConfirmed={vi.fn()} />);
    await user.click(await screen.findByRole('button', { name: /ML-KEM/ }));

    expect(screen.getByRole('button', { name: 'encapDecap' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'sigGen' })).not.toBeInTheDocument();
  });
});

describe('GeneratePage', () => {
  const configuration = {
    algorithm: 'ML-KEM' as const,
    mode: 'keyGen' as const,
    parameterSets: ['ML-KEM-768'],
  };

  it('shows the package download once the job succeeds', async () => {
    const queued: Job = {
      jobId: 'job-1',
      vsId: 101,
      kind: 'generate',
      status: 'Queued',
      createdAt: new Date().toISOString(),
    };
    const succeeded: Job = {
      ...queued,
      status: 'Succeeded',
      completedAt: new Date().toISOString(),
    };

    vi.mocked(fetch)
      .mockResolvedValueOnce(jsonResponse(queued)) // POST /api/generate
      .mockResolvedValueOnce(jsonResponse(succeeded)); // GET /api/jobs/job-1

    const user = userEvent.setup();
    let job: Job | null = null;
    const { rerender } = render(
      <GeneratePage
        configuration={configuration}
        job={job}
        onJobChanged={(next) => {
          job = next;
        }}
        onBack={vi.fn()}
        onContinue={vi.fn()}
      />,
    );

    await user.click(screen.getByRole('button', { name: '產生測試向量' }));

    await waitFor(() => expect(job?.status).toBe('Succeeded'));
    rerender(
      <GeneratePage
        configuration={configuration}
        job={job}
        onJobChanged={vi.fn()}
        onBack={vi.fn()}
        onContinue={vi.fn()}
      />,
    );

    const download = await screen.findByRole('link', { name: /下載測試向量包/ });
    expect(download).toHaveAttribute('href', '/api/jobs/job-1/prompt-package');
  });

  it('surfaces a failed job with its safe error message', async () => {
    const queued: Job = {
      jobId: 'job-2',
      vsId: 102,
      kind: 'generate',
      status: 'Queued',
      createdAt: new Date().toISOString(),
    };
    const failed: Job = {
      ...queued,
      status: 'Failed',
      error: { code: 'ENGINE_UNAVAILABLE', message: '驗證引擎目前無法連線' },
    };

    vi.mocked(fetch)
      .mockResolvedValueOnce(jsonResponse(queued))
      .mockResolvedValueOnce(jsonResponse(failed));

    const user = userEvent.setup();
    render(
      <GeneratePage
        configuration={configuration}
        job={null}
        onJobChanged={vi.fn()}
        onBack={vi.fn()}
        onContinue={vi.fn()}
      />,
    );

    await user.click(screen.getByRole('button', { name: '產生測試向量' }));

    expect(await screen.findByText(/驗證引擎目前無法連線/)).toBeInTheDocument();
  });
});
