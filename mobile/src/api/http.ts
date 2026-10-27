import { API_URL, isApiConfigured } from '@/config';

import type { ProblemDetails } from './types';

export class ApiError extends Error {
  constructor(
    readonly status: number,
    readonly problem: ProblemDetails | null,
    readonly retryAfterMs: number | null,
  ) {
    super(problem?.title ?? `Request failed with status ${status}`);
    this.name = 'ApiError';
  }
}

export class NetworkError extends Error {
  constructor(readonly timedOut: boolean) {
    super(timedOut ? 'The server did not answer in time.' : 'Could not reach the server.');
    this.name = 'NetworkError';
  }
}

export type RequestOptions = {
  method?: 'GET' | 'POST' | 'DELETE';
  body?: unknown;
  headers?: Record<string, string>;
  timeoutMs?: number;
  /** Extra attempts after the first one for timeouts, network errors, 429 and 5xx. */
  retries?: number;
};

const DEFAULT_TIMEOUT_MS = 30_000;
const DEFAULT_RETRIES = 3;
// The free Render instance takes about a minute to wake after idling, so the
// schedule (plus each attempt's timeout) comfortably spans that.
const BACKOFF_MS = [2_000, 5_000, 15_000, 30_000];

export function isRetryable(error: unknown): boolean {
  if (error instanceof NetworkError) {
    return true;
  }
  return error instanceof ApiError && (error.status >= 500 || error.status === 429 || error.status === 408);
}

/** Sends a JSON request. Returns null for 204 No Content. */
export async function request<T>(path: string, options: RequestOptions = {}): Promise<T | null> {
  if (!isApiConfigured) {
    throw new Error('EXPO_PUBLIC_API_URL is not set.');
  }

  const retries = options.retries ?? DEFAULT_RETRIES;
  for (let attempt = 0; ; attempt++) {
    try {
      return await send<T>(path, options);
    } catch (error) {
      if (attempt >= retries || !isRetryable(error)) {
        throw error;
      }
      await sleep(backoffMs(attempt, error));
    }
  }
}

async function send<T>(path: string, options: RequestOptions): Promise<T | null> {
  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), options.timeoutMs ?? DEFAULT_TIMEOUT_MS);

  try {
    const hasBody = options.body !== undefined;
    let response: Response;
    let text: string;
    try {
      response = await fetch(`${API_URL}${path}`, {
        method: options.method ?? 'GET',
        headers: {
          Accept: 'application/json',
          ...(hasBody ? { 'Content-Type': 'application/json' } : {}),
          ...options.headers,
        },
        body: hasBody ? JSON.stringify(options.body) : undefined,
        signal: controller.signal,
      });
      text = await response.text();
    } catch {
      throw new NetworkError(controller.signal.aborted);
    }

    const data = text ? safeJson(text) : null;
    if (!response.ok) {
      throw new ApiError(response.status, data as ProblemDetails | null, parseRetryAfter(response));
    }

    return response.status === 204 ? null : (data as T);
  } finally {
    clearTimeout(timer);
  }
}

function backoffMs(attempt: number, error: unknown): number {
  if (error instanceof ApiError && error.retryAfterMs !== null) {
    return error.retryAfterMs;
  }
  const base = BACKOFF_MS[Math.min(attempt, BACKOFF_MS.length - 1)];
  return base + Math.random() * base * 0.3;
}

function parseRetryAfter(response: Response): number | null {
  const seconds = Number(response.headers.get('Retry-After'));
  return Number.isFinite(seconds) && seconds > 0 ? seconds * 1000 : null;
}

function safeJson(text: string): unknown {
  try {
    return JSON.parse(text);
  } catch {
    return null;
  }
}

function sleep(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

/** A short, user-facing description of a failed request. */
export function describeError(error: unknown): string {
  if (error instanceof NetworkError) {
    return error.timedOut
      ? 'Máy chủ chưa phản hồi (có thể đang khởi động, mất khoảng 1 phút). Sẽ thử lại sau.'
      : 'Không kết nối được tới máy chủ. Sẽ thử lại khi có mạng.';
  }
  if (error instanceof ApiError) {
    if (error.status === 429) {
      return 'Gửi quá nhiều yêu cầu. Sẽ thử lại sau.';
    }
    return error.status >= 500 ? `Lỗi máy chủ (${error.status}). Sẽ thử lại sau.` : `Yêu cầu bị từ chối (${error.status}).`;
  }
  return error instanceof Error ? error.message : 'Đã xảy ra lỗi không xác định.';
}
