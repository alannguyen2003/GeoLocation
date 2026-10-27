import type { QueuedPoint } from './types';

// Browsers have no geocoder (expo-location's reverseGeocodeAsync is Android/iOS only).
// Web points still get city, region and country from the server.
export async function addDistricts(points: QueuedPoint[]): Promise<QueuedPoint[]> {
  return points;
}
