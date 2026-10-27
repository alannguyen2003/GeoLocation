export type DeviceCredentials = {
  deviceId: string;
  deviceToken: string;
};

export function parseCredentials(raw: string | null): DeviceCredentials | null {
  if (!raw) {
    return null;
  }

  try {
    const value = JSON.parse(raw) as Partial<DeviceCredentials>;
    return typeof value.deviceId === 'string' && typeof value.deviceToken === 'string'
      ? { deviceId: value.deviceId, deviceToken: value.deviceToken }
      : null;
  } catch {
    return null;
  }
}
