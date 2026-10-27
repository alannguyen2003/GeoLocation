import * as Crypto from 'expo-crypto';
import type { LocationObject } from 'expo-location';

import type { PointSource, QueuedPoint } from './types';

function finiteOrNull(value: number | null | undefined): number | null {
  return typeof value === 'number' && Number.isFinite(value) ? value : null;
}

// Platforms report -1 (or NaN on the web) when speed, heading or accuracy is unknown.
function nonNegativeOrNull(value: number | null | undefined): number | null {
  const finite = finiteOrNull(value);
  return finite !== null && finite >= 0 ? finite : null;
}

export function toQueuedPoint(location: LocationObject, source: PointSource): QueuedPoint {
  const { coords } = location;
  return {
    clientPointId: Crypto.randomUUID(),
    latitude: coords.latitude,
    longitude: coords.longitude,
    accuracy: nonNegativeOrNull(coords.accuracy),
    altitude: finiteOrNull(coords.altitude),
    speed: nonNegativeOrNull(coords.speed),
    heading: nonNegativeOrNull(coords.heading),
    recordedAt: new Date(location.timestamp).toISOString(),
    source,
  };
}
