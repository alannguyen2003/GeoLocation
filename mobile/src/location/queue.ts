import AsyncStorage from '@react-native-async-storage/async-storage';

import { createStore } from '@/lib/store';

import type { QueuedPoint } from './types';

const KEY = 'lt.queue.v1';
export const MAX_QUEUE_SIZE = 5_000;

/** Number of points waiting to be uploaded, for the UI. Refreshed on every queue change. */
export const pendingCountStore = createStore(0);

// Every read-modify-write goes through this chain so the foreground watcher, the
// background task and the uploader never overwrite each other's changes.
let chain: Promise<unknown> = Promise.resolve();

function withLock<T>(work: () => Promise<T>): Promise<T> {
  const run = chain.then(work, work);
  chain = run.catch(() => undefined);
  return run;
}

async function read(): Promise<QueuedPoint[]> {
  try {
    const raw = await AsyncStorage.getItem(KEY);
    const parsed: unknown = raw ? JSON.parse(raw) : [];
    return Array.isArray(parsed) ? (parsed as QueuedPoint[]) : [];
  } catch {
    return [];
  }
}

async function write(points: QueuedPoint[]): Promise<void> {
  await AsyncStorage.setItem(KEY, JSON.stringify(points));
  pendingCountStore.set(points.length);
}

/** Appends points; when the queue is full the oldest points are dropped first. */
export function enqueue(points: QueuedPoint[]): Promise<number> {
  return withLock(async () => {
    const all = [...(await read()), ...points];
    const kept = all.length > MAX_QUEUE_SIZE ? all.slice(all.length - MAX_QUEUE_SIZE) : all;
    await write(kept);
    return kept.length;
  });
}

/** The oldest `count` points, without removing them. */
export function peek(count: number): Promise<QueuedPoint[]> {
  return withLock(async () => (await read()).slice(0, count));
}

export function remove(clientPointIds: string[]): Promise<number> {
  const ids = new Set(clientPointIds);
  return removeWhere((point) => ids.has(point.clientPointId));
}

export function removeWhere(predicate: (point: QueuedPoint) => boolean): Promise<number> {
  return withLock(async () => {
    const all = await read();
    const kept = all.filter((point) => !predicate(point));
    if (kept.length !== all.length) {
      await write(kept);
    }
    return kept.length;
  });
}

export function refreshPendingCount(): Promise<number> {
  return withLock(async () => {
    const count = (await read()).length;
    pendingCountStore.set(count);
    return count;
  });
}

export function clearQueue(): Promise<void> {
  return withLock(() => write([]));
}
