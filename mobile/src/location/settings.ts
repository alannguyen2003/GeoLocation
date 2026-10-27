import AsyncStorage from '@react-native-async-storage/async-storage';
import * as Location from 'expo-location';

import { createStore } from '@/lib/store';

export type AccuracyLevel = 'low' | 'balanced' | 'high';

export type TrackingSettings = {
  /** Foreground watch: minimum seconds between updates (Android). */
  timeIntervalSec: number;
  /** Minimum metres moved between updates. */
  distanceIntervalM: number;
  accuracy: AccuracyLevel;
  /** The user asked for tracking while the app is closed (mobile only). */
  backgroundEnabled: boolean;
  /** Tracking is switched on; resumed automatically when the app opens. */
  trackingEnabled: boolean;
  /** The prominent disclosure has been shown and accepted. */
  disclosureAccepted: boolean;
};

export const DEFAULT_SETTINGS: TrackingSettings = {
  timeIntervalSec: 30,
  distanceIntervalM: 25,
  accuracy: 'balanced',
  backgroundEnabled: false,
  trackingEnabled: false,
  disclosureAccepted: false,
};

// Background updates are batched by the OS so they cost less battery.
export const BACKGROUND_DEFERRED_INTERVAL_MS = 60_000;
export const BACKGROUND_DEFERRED_DISTANCE_M = 50;

const KEY = 'lt.settings.v1';

export const settingsStore = createStore<TrackingSettings>(DEFAULT_SETTINGS);

export async function loadSettings(): Promise<TrackingSettings> {
  try {
    const raw = await AsyncStorage.getItem(KEY);
    if (raw) {
      settingsStore.set({ ...DEFAULT_SETTINGS, ...(JSON.parse(raw) as Partial<TrackingSettings>) });
    }
  } catch {
    // Fall back to the defaults.
  }
  return settingsStore.get();
}

export async function saveSettings(patch: Partial<TrackingSettings>): Promise<TrackingSettings> {
  const next = { ...settingsStore.get(), ...patch };
  settingsStore.set(next);
  await AsyncStorage.setItem(KEY, JSON.stringify(next));
  return next;
}

export async function resetSettings(): Promise<void> {
  settingsStore.set(DEFAULT_SETTINGS);
  await AsyncStorage.removeItem(KEY);
}

export function toExpoAccuracy(level: AccuracyLevel): Location.Accuracy {
  switch (level) {
    case 'low':
      return Location.Accuracy.Low;
    case 'high':
      return Location.Accuracy.High;
    default:
      return Location.Accuracy.Balanced;
  }
}
