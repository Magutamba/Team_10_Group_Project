import { vi } from 'vitest';

// --- DOM shims for Bootstrap JS ---
class NoopObserver {
  observe() {}
  unobserve() {}
  disconnect() {}
}

(globalThis as any).ResizeObserver = NoopObserver;
(globalThis as any).MutationObserver = NoopObserver;

// --- Mock Bootstrap JS globally ---
vi.mock('bootstrap/dist/js/bootstrap.bundle.min.js', () => ({
  default: {}
}));

// Optional: if you import plain "bootstrap"
vi.mock('bootstrap', () => ({
  default: {}
}));

