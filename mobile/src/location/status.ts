import AsyncStorage from '@react-native-async-storage/async-storage';

import { createStore } from '@/lib/store';

import type { PlaceNames, QueuedPoint } from './types';

export type SyncStatus = {
  /** ISO time of the last attempt. */
  at: string;
  ok: boolean;
  uploaded: number;
  error?: string;
};

export type LastPlace = PlaceNames & {
  /** recordedAt of the server point these names belong to. */
  recordedAt: string;
};

type Status = {
  lastPoint: QueuedPoint | null;
  lastSync: SyncStatus | null;
  lastPlace: LastPlace | null;
};

const KEY = 'lt.status.v1';
const EMPTY: Status = { lastPoint: null, lastSync: null, lastPlace: null };

/** Last recorded point, last upload result and where the newest uploaded point is; persisted across restarts. */
export const statusStore = createStore<Status>(EMPTY);

export async function loadStatus(): Promise<void> {
  try {
    const raw = await AsyncStorage.getItem(KEY);
    if (raw) {
      statusStore.set({ ...EMPTY, ...(JSON.parse(raw) as Partial<Status>) });
    }
  } catch {
    // Keep the empty status.
  }
}

async function update(patch: Partial<Status>): Promise<void> {
  statusStore.set((current) => ({ ...current, ...patch }));
  try {
    await AsyncStorage.setItem(KEY, JSON.stringify(statusStore.get()));
  } catch {
    // Status is only informational; losing it is fine.
  }
}

export function recordLastPoint(point: QueuedPoint): Promise<void> {
  return update({ lastPoint: point });
}

export function recordSync(sync: SyncStatus): Promise<void> {
  return update({ lastSync: sync });
}

export function recordLastPlace(place: LastPlace): Promise<void> {
  return update({ lastPlace: place });
}

export async function clearStatus(): Promise<void> {
  statusStore.set(EMPTY);
  await AsyncStorage.removeItem(KEY);
}
