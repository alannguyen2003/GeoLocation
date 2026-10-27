import { ensureDevice, forgetDevice } from '@/identity/ensureDevice';
import type { DeviceCredentials } from '@/identity/types';
import type { QueuedPoint } from '@/location/types';

import { ApiError, request, type RequestOptions } from './http';
import type { BatchUploadRequest, BatchUploadResponse, LocationPointResponse } from './types';

type AuthedOptions = RequestOptions & {
  /** Register a new device and retry once when the server no longer knows this one. */
  reRegisterOnUnauthorized?: boolean;
};

function deviceHeaders(device: DeviceCredentials): Record<string, string> {
  return { 'X-Device-Id': device.deviceId, 'X-Device-Token': device.deviceToken };
}

async function authedRequest<T>(path: string, options: AuthedOptions = {}): Promise<T | null> {
  const { reRegisterOnUnauthorized = true, ...requestOptions } = options;
  const device = await ensureDevice();

  try {
    return await request<T>(path, { ...requestOptions, headers: deviceHeaders(device) });
  } catch (error) {
    if (!(error instanceof ApiError && error.status === 401 && reRegisterOnUnauthorized)) {
      throw error;
    }
    // The server does not know this device any more, for example because the free
    // database was recreated. Register again so queued points are not stuck forever.
    await forgetDevice();
    const fresh = await ensureDevice();
    return request<T>(path, { ...requestOptions, headers: deviceHeaders(fresh) });
  }
}

export async function uploadBatch(points: QueuedPoint[], options: RequestOptions = {}): Promise<BatchUploadResponse> {
  const body: BatchUploadRequest = { points };
  const result = await authedRequest<BatchUploadResponse>('/api/locations/batch', {
    ...options,
    method: 'POST',
    body,
  });
  return result ?? { accepted: 0, duplicates: 0 };
}

export async function getLocations(limit = 100): Promise<LocationPointResponse[]> {
  return (await authedRequest<LocationPointResponse[]>(`/api/locations?limit=${limit}`)) ?? [];
}

export function getLatestLocation(): Promise<LocationPointResponse | null> {
  return authedRequest<LocationPointResponse>('/api/locations/latest');
}

/** Deletes this device and all its points on the server. A device the server no longer knows counts as deleted. */
export async function deleteMyDevice(): Promise<void> {
  try {
    await authedRequest<null>('/api/devices/me', { method: 'DELETE', reRegisterOnUnauthorized: false });
  } catch (error) {
    if (!(error instanceof ApiError && error.status === 401)) {
      throw error;
    }
  }
}
