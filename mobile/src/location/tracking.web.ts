import type { LocationObject } from 'expo-location';

import { toQueuedPoint } from './points';
import { recordPoints } from './record';
import type { TrackingSettings } from './settings';
import { setTrackingNotice } from './state';
import type { StartResult, TrackingMode } from './types';

type StartOptions = { prompt: boolean };

// Browsers stop geolocation when the tab closes; there is no background mode on the web.
export const supportsBackgroundTracking = false;

// The browser Geolocation API is used directly instead of expo-location's web shim: the shim
// drops watch errors, passes no timeout (so a missing fix waits forever) and matches watch
// callbacks by an ID the browser does not share, so updates can silently never arrive.
const FIRST_FIX_TIMEOUT_MS = 20_000;
const WATCH_TIMEOUT_MS = 60_000;

let watchId: number | null = null;
let lastRecorded: { at: number; latitude: number; longitude: number } | null = null;

function geolocation(): Geolocation | null {
  return typeof navigator !== 'undefined' && 'geolocation' in navigator ? navigator.geolocation : null;
}

export async function hasForegroundPermission(): Promise<boolean> {
  try {
    const status = await navigator.permissions.query({ name: 'geolocation' });
    return status.state === 'granted';
  } catch {
    // Permissions API missing: report "not granted" so nothing prompts without a user action.
    return false;
  }
}

export async function requestBackgroundPermission(): Promise<boolean> {
  return false;
}

export async function startTracking(settings: TrackingSettings, { prompt }: StartOptions): Promise<StartResult> {
  const geo = geolocation();
  if (!geo) {
    return { ok: false, reason: 'unsupported' };
  }
  // Browser geolocation only works over HTTPS (Vercel) or on localhost.
  if (typeof window !== 'undefined' && !window.isSecureContext) {
    return { ok: false, reason: 'insecure-context' };
  }
  if (!prompt && !(await hasForegroundPermission())) {
    return { ok: false, reason: 'foreground-denied' };
  }

  await stopTracking();
  const options = positionOptions(settings);

  // Asking for one position first shows the browser's permission prompt, records a point
  // straight away and tells us at once whether this browser can locate the device at all.
  let detail: string | undefined;
  try {
    const first = await currentPosition(geo, { ...options, timeout: FIRST_FIX_TIMEOUT_MS });
    void handlePosition(first, settings);
  } catch (error) {
    if (isPositionError(error) && error.code === error.PERMISSION_DENIED) {
      return { ok: false, reason: 'foreground-denied' };
    }
    detail = describePositionError(error);
  }

  watchId = geo.watchPosition(
    (position) => {
      setTrackingNotice(null);
      void handlePosition(position, settings);
    },
    (error) => {
      // Timeouts just mean "no new fix yet"; the watch keeps running.
      if (error.code !== error.TIMEOUT || !lastRecorded) {
        setTrackingNotice(describePositionError(error));
      }
    },
    { ...options, timeout: WATCH_TIMEOUT_MS },
  );

  return detail ? { ok: true, mode: 'web', warning: 'position-error', detail } : { ok: true, mode: 'web' };
}

export async function stopTracking(): Promise<void> {
  if (watchId !== null) {
    geolocation()?.clearWatch(watchId);
  }
  watchId = null;
  lastRecorded = null;
}

export async function getActiveMode(): Promise<TrackingMode | null> {
  return watchId !== null ? 'web' : null;
}

function positionOptions(settings: TrackingSettings): PositionOptions {
  return {
    enableHighAccuracy: settings.accuracy === 'high',
    maximumAge: 10_000,
  };
}

function currentPosition(geo: Geolocation, options: PositionOptions): Promise<GeolocationPosition> {
  return new Promise((resolve, reject) => geo.getCurrentPosition(resolve, reject, options));
}

/**
 * Browsers ignore interval and distance settings, so apply them here: after the first point,
 * record only when both the minimum time has passed and the device moved far enough.
 */
async function handlePosition(position: GeolocationPosition, settings: TrackingSettings): Promise<void> {
  const { latitude, longitude } = position.coords;
  const now = position.timestamp;

  if (lastRecorded) {
    const elapsedMs = now - lastRecorded.at;
    const movedM = distanceMetres(lastRecorded.latitude, lastRecorded.longitude, latitude, longitude);
    if (elapsedMs < settings.timeIntervalSec * 1000 || movedM < settings.distanceIntervalM) {
      return;
    }
  }

  lastRecorded = { at: now, latitude, longitude };
  await recordPoints([toQueuedPoint(toLocationObject(position), 'web')]);
}

function toLocationObject(position: GeolocationPosition): LocationObject {
  const { coords } = position;
  return {
    coords: {
      latitude: coords.latitude,
      longitude: coords.longitude,
      altitude: coords.altitude,
      accuracy: coords.accuracy,
      altitudeAccuracy: coords.altitudeAccuracy,
      heading: coords.heading,
      speed: coords.speed,
    },
    timestamp: position.timestamp,
  };
}

function distanceMetres(lat1: number, lon1: number, lat2: number, lon2: number): number {
  const toRad = (degrees: number) => (degrees * Math.PI) / 180;
  const dLat = toRad(lat2 - lat1);
  const dLon = toRad(lon2 - lon1);
  const a = Math.sin(dLat / 2) ** 2 + Math.cos(toRad(lat1)) * Math.cos(toRad(lat2)) * Math.sin(dLon / 2) ** 2;
  return 2 * 6_371_000 * Math.asin(Math.sqrt(a));
}

function isPositionError(error: unknown): error is GeolocationPositionError {
  return typeof error === 'object' && error !== null && 'code' in error && 'PERMISSION_DENIED' in error;
}

function describePositionError(error: unknown): string {
  if (!isPositionError(error)) {
    return 'Không lấy được vị trí từ trình duyệt.';
  }
  switch (error.code) {
    case error.PERMISSION_DENIED:
      return 'Trình duyệt đã chặn quyền vị trí cho trang này. Bấm biểu tượng ổ khoá cạnh thanh địa chỉ để cho phép, rồi bấm Dừng và Bắt đầu lại.';
    case error.TIMEOUT:
      return 'Đang chờ trình duyệt xác định vị trí… Trên máy tính việc này có thể mất một lúc. Ứng dụng vẫn tiếp tục chờ.';
    default:
      return 'Trình duyệt không xác định được vị trí. Trên Windows, hãy bật Cài đặt → Quyền riêng tư & bảo mật → Vị trí và cho phép ứng dụng desktop dùng vị trí, kiểm tra Wi-Fi đang bật, rồi bấm Dừng và Bắt đầu lại.';
  }
}
