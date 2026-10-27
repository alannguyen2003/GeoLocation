import type { LocationObject } from 'expo-location';
import * as TaskManager from 'expo-task-manager';
import { AppState } from 'react-native';

import { toQueuedPoint } from './points';
import { recordPoints } from './record';

export const BACKGROUND_LOCATION_TASK = 'location-tracker.background-location';

type BackgroundLocationData = { locations?: LocationObject[] };

// Defined at module top level and imported from app/_layout.tsx: when the OS starts the
// app in the background no component is mounted, so a task defined in React would not exist.
// No React state, hooks or context here; credentials are read from storage by the API client.
TaskManager.defineTask<BackgroundLocationData>(BACKGROUND_LOCATION_TASK, async ({ data, error }) => {
  if (error) {
    console.warn(`Background location error: ${error.message}`);
    return;
  }

  const locations = data?.locations ?? [];
  if (locations.length === 0) {
    return;
  }

  // Background updates keep arriving while the app is open, too.
  const source = AppState.currentState === 'active' ? 'foreground' : 'background';

  try {
    // One quick upload attempt; whatever does not go through stays queued for the next run.
    await recordPoints(
      locations.map((location) => toQueuedPoint(location, source)),
      { retries: 0, maxBatches: 3 },
    );
  } catch (taskError) {
    console.warn(`Background location task failed: ${taskError instanceof Error ? taskError.message : 'unknown error'}`);
  }
});
