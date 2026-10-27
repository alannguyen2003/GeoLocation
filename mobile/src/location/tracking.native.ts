import * as Location from 'expo-location';

import { BACKGROUND_LOCATION_TASK } from './backgroundTask';
import { toQueuedPoint } from './points';
import { recordPoints } from './record';
import {
  BACKGROUND_DEFERRED_DISTANCE_M,
  BACKGROUND_DEFERRED_INTERVAL_MS,
  toExpoAccuracy,
  type TrackingSettings,
} from './settings';
import type { StartResult, TrackingMode } from './types';

type StartOptions = { prompt: boolean };

export const supportsBackgroundTracking = true;

let watcher: Location.LocationSubscription | null = null;

export async function hasForegroundPermission(): Promise<boolean> {
  return (await Location.getForegroundPermissionsAsync()).granted;
}

async function ensureForegroundPermission(prompt: boolean): Promise<boolean> {
  const current = await Location.getForegroundPermissionsAsync();
  if (current.granted || !prompt || !current.canAskAgain) {
    return current.granted;
  }
  return (await Location.requestForegroundPermissionsAsync()).granted;
}

export async function requestBackgroundPermission(): Promise<boolean> {
  // Both platforms only grant background access on top of foreground access.
  if (!(await ensureForegroundPermission(true))) {
    return false;
  }
  const current = await Location.getBackgroundPermissionsAsync();
  if (current.granted || !current.canAskAgain) {
    return current.granted;
  }
  return (await Location.requestBackgroundPermissionsAsync()).granted;
}

export async function startTracking(settings: TrackingSettings, { prompt }: StartOptions): Promise<StartResult> {
  if (!(await Location.hasServicesEnabledAsync())) {
    return { ok: false, reason: 'services-disabled' };
  }
  if (!(await ensureForegroundPermission(prompt))) {
    return { ok: false, reason: 'foreground-denied' };
  }

  if (settings.backgroundEnabled) {
    const backgroundGranted = prompt
      ? await requestBackgroundPermission()
      : (await Location.getBackgroundPermissionsAsync()).granted;

    if (backgroundGranted) {
      try {
        // Background updates also arrive while the app is open, so the foreground watch is not needed.
        await stopForegroundWatch();
        await startBackgroundUpdates(settings);
        return { ok: true, mode: 'background' };
      } catch {
        // Typically Expo Go, which cannot run background location. Fall through to foreground tracking.
        await startForegroundWatch(settings);
        return { ok: true, mode: 'foreground', warning: 'background-unavailable' };
      }
    }

    await stopBackgroundUpdates();
    await startForegroundWatch(settings);
    return { ok: true, mode: 'foreground', warning: 'background-denied' };
  }

  await stopBackgroundUpdates();
  await startForegroundWatch(settings);
  return { ok: true, mode: 'foreground' };
}

export async function stopTracking(): Promise<void> {
  await stopForegroundWatch();
  await stopBackgroundUpdates();
}

export async function getActiveMode(): Promise<TrackingMode | null> {
  if (await isBackgroundRunning()) {
    return 'background';
  }
  return watcher ? 'foreground' : null;
}

async function startForegroundWatch(settings: TrackingSettings): Promise<void> {
  await stopForegroundWatch();
  watcher = await Location.watchPositionAsync(
    {
      accuracy: toExpoAccuracy(settings.accuracy),
      timeInterval: settings.timeIntervalSec * 1000,
      distanceInterval: settings.distanceIntervalM,
    },
    (location) => {
      void recordPoints([toQueuedPoint(location, 'foreground')]);
    },
  );
}

async function stopForegroundWatch(): Promise<void> {
  watcher?.remove();
  watcher = null;
}

async function startBackgroundUpdates(settings: TrackingSettings): Promise<void> {
  // Restart so changed settings take effect.
  await stopBackgroundUpdates();
  await Location.startLocationUpdatesAsync(BACKGROUND_LOCATION_TASK, {
    accuracy: toExpoAccuracy(settings.accuracy),
    timeInterval: settings.timeIntervalSec * 1000,
    distanceInterval: settings.distanceIntervalM,
    deferredUpdatesInterval: BACKGROUND_DEFERRED_INTERVAL_MS,
    deferredUpdatesDistance: BACKGROUND_DEFERRED_DISTANCE_M,
    pausesUpdatesAutomatically: true,
    activityType: Location.ActivityType.Other,
    showsBackgroundLocationIndicator: true,
    foregroundService: {
      notificationTitle: 'Location Tracker đang ghi vị trí',
      notificationBody: 'Vị trí của thiết bị đang được ghi lại. Mở ứng dụng để dừng.',
      notificationColor: '#208AEF',
    },
  });
}

async function stopBackgroundUpdates(): Promise<void> {
  if (await isBackgroundRunning()) {
    await Location.stopLocationUpdatesAsync(BACKGROUND_LOCATION_TASK);
  }
}

async function isBackgroundRunning(): Promise<boolean> {
  try {
    return await Location.hasStartedLocationUpdatesAsync(BACKGROUND_LOCATION_TASK);
  } catch {
    return false;
  }
}
