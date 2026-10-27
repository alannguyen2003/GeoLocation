import { Platform } from 'react-native';

import { request } from '@/api/http';
import type { RegisterDeviceResponse } from '@/api/types';

import { clearCredentials, loadCredentials, saveCredentials } from './deviceStore';
import type { DeviceCredentials } from './types';

let pending: Promise<DeviceCredentials> | null = null;

function platformName(): 'android' | 'ios' | 'web' {
  return Platform.OS === 'android' || Platform.OS === 'ios' ? Platform.OS : 'web';
}

/**
 * Returns the stored device credentials, registering the device first if there are none.
 * Concurrent callers share one registration. Safe to call from the background task.
 */
export function ensureDevice(): Promise<DeviceCredentials> {
  pending ??= (async () => {
    const existing = await loadCredentials();
    if (existing) {
      return existing;
    }

    const registered = await request<RegisterDeviceResponse>('/api/devices', {
      method: 'POST',
      body: { platform: platformName() },
    });
    if (!registered) {
      throw new Error('The server returned no device credentials.');
    }

    await saveCredentials(registered);
    return registered;
  })().finally(() => {
    pending = null;
  });

  return pending;
}

export function getStoredDevice(): Promise<DeviceCredentials | null> {
  return loadCredentials();
}

export function forgetDevice(): Promise<void> {
  return clearCredentials();
}
