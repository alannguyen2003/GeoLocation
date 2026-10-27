import NetInfo from '@react-native-community/netinfo';
import { AppState } from 'react-native';

import { getLatestLocation, uploadBatch } from '@/api/client';
import { ApiError, describeError } from '@/api/http';
import type { ProblemDetails } from '@/api/types';

import { addDistricts } from './district';
import { peek, refreshPendingCount, remove, removeWhere } from './queue';
import { recordLastPlace, recordSync } from './status';

const BATCH_SIZE = 100;
const FOREGROUND_FLUSH_INTERVAL_MS = 60_000;
// The server rejects points older than 7 days; drop them a little earlier so a batch never fails on them.
const MAX_POINT_AGE_MS = 7 * 24 * 60 * 60 * 1000 - 60 * 60 * 1000;

export type FlushOptions = {
  /** Retries per batch. The background task uses 0: the OS gives it little time and points stay queued. */
  retries?: number;
  maxBatches?: number;
};

export type FlushResult = {
  uploaded: number;
  remaining: number;
  error?: string;
};

let inFlight: Promise<FlushResult> | null = null;

/** Uploads the queue in batches of 100. Concurrent calls share the run already in progress. */
export function flushQueue(options: FlushOptions = {}): Promise<FlushResult> {
  inFlight ??= run(options).finally(() => {
    inFlight = null;
  });
  return inFlight;
}

/** Resolves once no upload is running. */
export function waitForUploaderIdle(): Promise<void> {
  return inFlight ? inFlight.then(() => undefined, () => undefined) : Promise.resolve();
}

async function run({ retries, maxBatches = Number.POSITIVE_INFINITY }: FlushOptions): Promise<FlushResult> {
  const cutoff = Date.now() - MAX_POINT_AGE_MS;
  await removeWhere((point) => Date.parse(point.recordedAt) < cutoff);

  let uploaded = 0;
  for (let batchNumber = 0; batchNumber < maxBatches; batchNumber++) {
    const batch = await peek(BATCH_SIZE);
    if (batch.length === 0) {
      break;
    }

    try {
      const result = await uploadBatch(await addDistricts(batch), { retries });
      // Duplicates are points the server already has from an earlier, interrupted upload.
      await remove(batch.map((point) => point.clientPointId));
      uploaded += result.accepted;
    } catch (error) {
      if (error instanceof ApiError && error.status === 400) {
        // Retrying a rejected point would block the queue forever, so drop what the server refused.
        const rejected = rejectedIndexes(error.problem);
        const toDrop = rejected.size > 0 ? batch.filter((_, index) => rejected.has(index)) : batch;
        await remove(toDrop.map((point) => point.clientPointId));
        continue;
      }

      const remaining = await refreshPendingCount();
      const message = describeError(error);
      await recordSync({ at: new Date().toISOString(), ok: false, uploaded, error: message });
      return { uploaded, remaining, error: message };
    }
  }

  const remaining = await refreshPendingCount();
  await recordSync({ at: new Date().toISOString(), ok: true, uploaded });
  if (uploaded > 0 && AppState.currentState === 'active') {
    await refreshLastPlace();
  }
  return { uploaded, remaining };
}

/** Place names are resolved on the server, so ask it where the newest point is. Only while the app is open. */
async function refreshLastPlace(): Promise<void> {
  try {
    const latest = await getLatestLocation();
    if (latest) {
      await recordLastPlace({
        district: latest.district,
        city: latest.city,
        region: latest.region,
        country: latest.country,
        recordedAt: latest.recordedAt,
      });
    }
  } catch {
    // Informational only; the next upload tries again.
  }
}

/** Indexes of points named in validation errors such as "points[3].latitude". */
function rejectedIndexes(problem: ProblemDetails | null): Set<number> {
  const indexes = new Set<number>();
  for (const key of Object.keys(problem?.errors ?? {})) {
    const match = /^points\[(\d+)\]/.exec(key);
    if (match) {
      indexes.add(Number(match[1]));
    }
  }
  return indexes;
}

/**
 * While the app is open: flush when it comes to the foreground, when the network
 * comes back, and once a minute. Returns a function that stops all of it.
 */
export function startAutoFlush(): () => void {
  const flush = () => {
    void flushQueue();
  };

  let wasConnected = true;
  const unsubscribeNet = NetInfo.addEventListener((state) => {
    const connected = state.isConnected !== false;
    if (connected && !wasConnected) {
      flush();
    }
    wasConnected = connected;
  });

  const appStateSubscription = AppState.addEventListener('change', (state) => {
    if (state === 'active') {
      flush();
    }
  });

  const interval = setInterval(() => {
    if (AppState.currentState === 'active') {
      flush();
    }
  }, FOREGROUND_FLUSH_INTERVAL_MS);

  flush();

  return () => {
    unsubscribeNet();
    appStateSubscription.remove();
    clearInterval(interval);
  };
}
