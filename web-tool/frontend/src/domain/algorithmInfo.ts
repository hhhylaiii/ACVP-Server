import type { Algorithm, Mode } from '../api/types';

/** Display metadata for the two supported FIPS algorithms. */
export interface AlgorithmInfo {
  standard: string;
  title: string;
  description: string;
}

export const ALGORITHM_INFO: Record<Algorithm, AlgorithmInfo> = {
  'ML-KEM': {
    standard: 'FIPS 203',
    title: 'Module-Lattice-Based Key-Encapsulation Mechanism',
    description:
      'Post-quantum KEM (Kyber) used to establish shared secrets over insecure channels.',
  },
  'ML-DSA': {
    standard: 'FIPS 204',
    title: 'Module-Lattice-Based Digital Signature Algorithm',
    description: 'Post-quantum signature scheme (Dilithium) for signing and verifying messages.',
  },
};

export const MODE_INFO: Record<Mode, string> = {
  keyGen: 'Key generation',
  encapDecap: 'Encapsulation / decapsulation',
  sigGen: 'Signature generation',
  sigVer: 'Signature verification',
};

/** NIST PQC security category per parameter set (FIPS 203 / 204). */
export interface ParameterSetInfo {
  category: 1 | 2 | 3 | 5;
  strength: string;
}

export const PARAMETER_SET_INFO: Record<string, ParameterSetInfo> = {
  'ML-KEM-512': { category: 1, strength: '≈ AES-128' },
  'ML-KEM-768': { category: 3, strength: '≈ AES-192' },
  'ML-KEM-1024': { category: 5, strength: '≈ AES-256' },
  'ML-DSA-44': { category: 2, strength: '≈ SHA3-256' },
  'ML-DSA-65': { category: 3, strength: '≈ AES-192' },
  'ML-DSA-87': { category: 5, strength: '≈ AES-256' },
};
