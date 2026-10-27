import { deleteMyDevice } from '@/api/client';
import { forgetDevice, getStoredDevice } from '@/identity/ensureDevice';

import { clearQueue, refreshPendingCount } from './queue';
import { loadSettings, resetSettings, saveSettings, settingsStore, type TrackingSettings } from './settings';
import { trackingStore } from './state';
import { clearStatus, loadStatus } from './status';
import * as tracking from './tracking';
import type { StartResult } from './types';
import { waitForUploaderIdle } from './uploader';

export { trackingStore };

function noticeFor(result: StartResult): string | null {
  if (result.ok) {
    if (result.detail) {
      return result.detail;
    }
    switch (result.warning) {
      case 'background-denied':
        return 'Chưa có quyền vị trí "Luôn cho phép", nên chỉ theo dõi khi ứng dụng đang mở. Hãy cấp quyền trong Cài đặt hệ thống.';
      case 'background-unavailable':
        return 'Theo dõi nền không chạy được trong bản này (ví dụ Expo Go). Đang theo dõi khi ứng dụng mở. Hãy dùng development build hoặc preview APK.';
      default:
        return null;
    }
  }
  switch (result.reason) {
    case 'foreground-denied':
      return 'Ứng dụng chưa có quyền truy cập vị trí. Hãy cho phép trong cài đặt của hệ thống hoặc trình duyệt.';
    case 'services-disabled':
      return 'Dịch vụ định vị của thiết bị đang tắt. Hãy bật GPS/Vị trí rồi thử lại.';
    case 'insecure-context':
      return 'Trình duyệt chỉ cho phép định vị qua HTTPS hoặc localhost.';
    default:
      return 'Thiết bị hoặc trình duyệt này không hỗ trợ định vị.';
  }
}

async function runStart(settings: TrackingSettings, prompt: boolean): Promise<StartResult> {
  trackingStore.set((s) => ({ ...s, busy: true }));
  try {
    const result = await tracking.startTracking(settings, { prompt });
    trackingStore.set({ mode: result.ok ? result.mode : null, busy: false, notice: noticeFor(result) });
    return result;
  } catch (error) {
    trackingStore.set({
      mode: await tracking.getActiveMode(),
      busy: false,
      notice: error instanceof Error ? error.message : 'Không bắt đầu theo dõi được.',
    });
    return { ok: false, reason: 'unsupported' };
  }
}

/** Loads persisted state and resumes tracking if it was on, without showing any permission prompt. */
export async function initializeTracking(): Promise<void> {
  const [settings] = await Promise.all([loadSettings(), loadStatus(), refreshPendingCount()]);
  const mode = await tracking.getActiveMode();
  trackingStore.set((s) => ({ ...s, mode }));

  if (settings.trackingEnabled && settings.disclosureAccepted && (await tracking.hasForegroundPermission())) {
    await runStart(settings, false);
  }
}

/** Starts tracking. Callers must have shown the disclosure screen first. */
export async function startTracking(): Promise<StartResult> {
  const settings = await saveSettings({ trackingEnabled: true });
  const result = await runStart(settings, true);
  if (!result.ok) {
    await saveSettings({ trackingEnabled: false });
  }
  return result;
}

export async function stopTracking(): Promise<void> {
  trackingStore.set((s) => ({ ...s, busy: true }));
  await saveSettings({ trackingEnabled: false });
  await tracking.stopTracking();
  trackingStore.set({ mode: null, busy: false, notice: null });
}

/**
 * Turning background tracking on asks for the background permission right away,
 * as required: never earlier than the moment the user opts in.
 */
export async function setBackgroundEnabled(enabled: boolean): Promise<boolean> {
  if (enabled && !(await tracking.requestBackgroundPermission())) {
    trackingStore.set((s) => ({
      ...s,
      notice: 'Cần quyền vị trí "Luôn cho phép" (Android) / "Luôn luôn" (iOS) để theo dõi khi ứng dụng đóng. Hãy cấp quyền trong Cài đặt hệ thống.',
    }));
    return false;
  }

  const settings = await saveSettings({ backgroundEnabled: enabled });
  if (settings.trackingEnabled) {
    await runStart(settings, true);
  }
  return true;
}

/** Saves interval/accuracy settings and restarts tracking so they take effect. */
export async function updateTrackingSettings(
  patch: Partial<Pick<TrackingSettings, 'timeIntervalSec' | 'distanceIntervalM' | 'accuracy'>>,
): Promise<void> {
  const settings = await saveSettings(patch);
  if (settings.trackingEnabled && trackingStore.get().mode) {
    await runStart(settings, false);
  }
}

export async function acceptDisclosure(): Promise<void> {
  await saveSettings({ disclosureAccepted: true });
}

/**
 * Stops tracking, deletes the device and all its points on the server, then clears everything
 * stored locally. If the server cannot be reached this throws and the device credentials are
 * kept, so the user can retry.
 */
export async function deleteMyData(): Promise<void> {
  await stopTracking();
  // Drop unsent points and let a running upload finish first; otherwise it could hit the
  // deleted device, get a 401 and register a new device with the old points.
  await clearQueue();
  await waitForUploaderIdle();
  if (await getStoredDevice()) {
    await deleteMyDevice();
  }
  await clearStatus();
  await forgetDevice();
  await resetSettings();
}

export function currentSettings(): TrackingSettings {
  return settingsStore.get();
}
