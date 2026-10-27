import { parseCredentials, type DeviceCredentials } from './types';

const KEY = 'lt.device.v1';

// localStorage can be missing or throw (private mode, blocked storage, static export).
function storage(): Storage | null {
  try {
    return typeof window === 'undefined' ? null : window.localStorage;
  } catch {
    return null;
  }
}

export async function loadCredentials(): Promise<DeviceCredentials | null> {
  try {
    return parseCredentials(storage()?.getItem(KEY) ?? null);
  } catch {
    return null;
  }
}

export async function saveCredentials(credentials: DeviceCredentials): Promise<void> {
  storage()?.setItem(KEY, JSON.stringify(credentials));
}

export async function clearCredentials(): Promise<void> {
  try {
    storage()?.removeItem(KEY);
  } catch {
    // Nothing stored, nothing to clear.
  }
}
