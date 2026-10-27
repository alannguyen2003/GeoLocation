import * as Location from 'expo-location';
import { AppState } from 'react-native';

import type { QueuedPoint } from './types';

// The phone's geocoder (Google on Android, Apple on iOS) is rate-limited, needs the network and
// is discouraged in the background. So: only while the app is open, at most one request every
// 30 s, and results cached per cell of about 1 km. Points without a district upload anyway;
// city, region and country always come from the server.
const MIN_LOOKUP_INTERVAL_MS = 30_000;
const LOOKUP_TIMEOUT_MS = 5_000;
const MAX_CACHE_ENTRIES = 500;

const cache = new Map<string, string | null>();
let lastLookupAt = 0;

export async function addDistricts(points: QueuedPoint[]): Promise<QueuedPoint[]> {
  const result: QueuedPoint[] = [];
  for (const point of points) {
    if (point.district) {
      result.push(point);
      continue;
    }

    const key = cellKey(point.latitude, point.longitude);
    if (!cache.has(key) && canLookUp()) {
      await lookUp(key, point);
    }

    const district = cache.get(key);
    result.push(district ? { ...point, district } : point);
  }
  return result;
}

// Two decimals is about 1.1 km of latitude: close enough for a ward name, coarse enough to reuse.
function cellKey(latitude: number, longitude: number): string {
  return `${latitude.toFixed(2)},${longitude.toFixed(2)}`;
}

function canLookUp(): boolean {
  return AppState.currentState === 'active' && Date.now() - lastLookupAt >= MIN_LOOKUP_INTERVAL_MS;
}

async function lookUp(key: string, point: QueuedPoint): Promise<void> {
  lastLookupAt = Date.now();
  try {
    const [address] = await withTimeout(
      Location.reverseGeocodeAsync({ latitude: point.latitude, longitude: point.longitude }),
      LOOKUP_TIMEOUT_MS,
    );
    remember(key, pickDistrict(address));
  } catch {
    // Offline, rate-limited or no geocoder on this phone: cache nothing so the cell is retried later.
  }
}

// The most specific area below the city: a ward (`district`) if the OS gives one, else the district (`subregion`).
function pickDistrict(address: Location.LocationGeocodedAddress | undefined): string | null {
  const value = address?.district?.trim() || address?.subregion?.trim();
  return value || null;
}

function remember(key: string, district: string | null): void {
  if (cache.size >= MAX_CACHE_ENTRIES) {
    // Maps keep insertion order, so the first key is the oldest.
    const oldest = cache.keys().next().value;
    if (oldest !== undefined) {
      cache.delete(oldest);
    }
  }
  cache.set(key, district);
}

function withTimeout<T>(promise: Promise<T>, ms: number): Promise<T> {
  return new Promise((resolve, reject) => {
    const timer = setTimeout(() => reject(new Error('Geocoding timed out')), ms);
    promise.then(
      (value) => {
        clearTimeout(timer);
        resolve(value);
      },
      (error: unknown) => {
        clearTimeout(timer);
        reject(error instanceof Error ? error : new Error('Geocoding failed'));
      },
    );
  });
}
