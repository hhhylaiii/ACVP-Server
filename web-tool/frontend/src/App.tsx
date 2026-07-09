import { useState } from 'react';
import type { AlgorithmConfiguration, Job } from './api/types';
import { StepIndicator, type Step } from './components/StepIndicator';
import { IconLock, IconShield } from './components/icons';
import { SelectPage } from './pages/SelectPage';
import { GeneratePage } from './pages/GeneratePage';
import { UploadPage } from './pages/UploadPage';
import { ReportPage } from './pages/ReportPage';

export default function App() {
  const [step, setStep] = useState<Step>('select');
  const [configuration, setConfiguration] = useState<AlgorithmConfiguration | null>(null);
  const [generateJob, setGenerateJob] = useState<Job | null>(null);
  const [validateJob, setValidateJob] = useState<Job | null>(null);

  return (
    <div className="app">
      <header className="app-header">
        <div className="header-inner">
          <div className="brand">
            <span className="brand-mark">
              <IconShield className="brand-icon" />
            </span>
            <div className="brand-text">
              <span className="brand-overline">Post-Quantum Cryptography Validation</span>
              <h1>FIPS 203 / 204 Validation Tool</h1>
              <p className="subtitle">
                ML-KEM / ML-DSA test vector generation & validation (ACVP Gen/Val)
              </p>
            </div>
          </div>
          <div className="header-badges">
            <span className="header-badge">FIPS 203 · ML-KEM</span>
            <span className="header-badge">FIPS 204 · ML-DSA</span>
            <span className="header-badge header-badge-secure">
              <IconLock className="badge-icon" />
              Local-only
            </span>
          </div>
        </div>
      </header>

      <div className="app-body">
        <StepIndicator current={step} />

        <main className="app-main">
          {step === 'select' && (
            <SelectPage
              onConfirmed={(config) => {
                setConfiguration(config);
                setGenerateJob(null);
                setValidateJob(null);
                setStep('generate');
              }}
            />
          )}
          {step === 'generate' && configuration && (
            <GeneratePage
              configuration={configuration}
              job={generateJob}
              onJobChanged={setGenerateJob}
              onBack={() => setStep('select')}
              onContinue={() => setStep('upload')}
            />
          )}
          {step === 'upload' && generateJob && (
            <UploadPage
              generateJob={generateJob}
              onValidated={(job) => {
                setValidateJob(job);
                setStep('report');
              }}
              onBack={() => setStep('generate')}
            />
          )}
          {step === 'report' && validateJob && (
            <ReportPage
              validateJob={validateJob}
              onRestart={() => {
                setConfiguration(null);
                setGenerateJob(null);
                setValidateJob(null);
                setStep('select');
              }}
              onUploadAgain={() => setStep('upload')}
            />
          )}
        </main>
      </div>

      <footer className="app-footer">
        <span className="footer-secure">
          <IconLock className="footer-icon" />
          Air-gapped by design: all test data stays on this machine and is never uploaded to any
          external service.
        </span>
        <span className="footer-refs">
          NIST FIPS 203 (ML-KEM) · FIPS 204 (ML-DSA) · ACVP Gen/Val
        </span>
      </footer>
    </div>
  );
}
