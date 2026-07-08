import { useState } from 'react';
import type { AlgorithmConfiguration, Job } from './api/types';
import { StepIndicator, type Step } from './components/StepIndicator';
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
        <h1>FIPS 203/204 驗證工具</h1>
        <p className="subtitle">ML-KEM / ML-DSA 測試向量產生與批改（ACVP Gen/Val）</p>
      </header>

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

      <footer className="app-footer">
        資料不出門：所有測試資料僅存放於本機環境，絕不上傳外部服務。
      </footer>
    </div>
  );
}
