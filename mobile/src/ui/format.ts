export function formatCoordinate(value: number): string {
  return value.toFixed(5);
}

export function formatDateTime(iso: string): string {
  const date = new Date(iso);
  return Number.isNaN(date.getTime()) ? iso : date.toLocaleString('vi-VN');
}

export function formatAccuracy(metres: number | null): string {
  return metres === null ? '–' : `±${Math.round(metres)} m`;
}

/** "Hoàn Kiếm, Hanoi, Vietnam". Skips missing parts and repeats such as city "Hanoi" in region "Hanoi". */
export function formatPlace(place: { district: string | null; city: string | null; region: string | null; country: string | null }): string | null {
  const parts: string[] = [];
  for (const part of [place.district, place.city, place.region, place.country]) {
    if (part && !parts.some((existing) => existing.toLowerCase() === part.toLowerCase())) {
      parts.push(part);
    }
  }
  return parts.length > 0 ? parts.join(', ') : null;
}

export const sourceLabels: Record<string, string> = {
  foreground: 'Khi mở app',
  background: 'Chạy nền',
  web: 'Web',
};
