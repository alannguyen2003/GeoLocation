import type { QueuedPoint } from '@/location/types';

export type RegisterDeviceResponse = {
  deviceId: string;
  deviceToken: string;
};

export type BatchUploadRequest = {
  points: QueuedPoint[];
};

export type BatchUploadResponse = {
  accepted: number;
  duplicates: number;
};

export type LocationPointResponse = {
  id: number;
  clientPointId: string;
  latitude: number;
  longitude: number;
  accuracy: number | null;
  altitude: number | null;
  speed: number | null;
  heading: number | null;
  recordedAt: string;
  receivedAt: string;
  source: 'foreground' | 'background' | 'web';
  /** From the phone's geocoder; often null. */
  district: string | null;
  /** The rest is resolved on the server from GeoNames; null far from any populated place. */
  city: string | null;
  region: string | null;
  countryCode: string | null;
  country: string | null;
};

/** RFC 7807 problem details as returned by the API. */
export type ProblemDetails = {
  title?: string;
  status?: number;
  errors?: Record<string, string[]>;
};
