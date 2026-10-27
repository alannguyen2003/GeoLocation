import * as SecureStore from 'expo-secure-store';

import { parseCredentials, type DeviceCredentials } from './types';

const KEY = 'lt.device.v1';

// AFTER_FIRST_UNLOCK lets the background task read the credentials while the iPhone is locked.
const options: SecureStore.SecureStoreOptions = {
  keychainAccessible: SecureStore.AFTER_FIRST_UNLOCK,
};

export async function loadCredentials(): Promise<DeviceCredentials | null> {
  return parseCredentials(await SecureStore.getItemAsync(KEY, options));
}

export async function saveCredentials(credentials: DeviceCredentials): Promise<void> {
  await SecureStore.setItemAsync(KEY, JSON.stringify(credentials), options);
}

export async function clearCredentials(): Promise<void> {
  await SecureStore.deleteItemAsync(KEY, options);
}
