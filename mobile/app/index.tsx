import { useRouter } from 'expo-router';
import { useState } from 'react';
import { Platform, StyleSheet, Switch, View } from 'react-native';

import { isApiConfigured } from '@/config';
import { useStore } from '@/lib/store';
import { setBackgroundEnabled, startTracking, stopTracking, trackingStore } from '@/location/controller';
import { pendingCountStore } from '@/location/queue';
import { settingsStore } from '@/location/settings';
import { statusStore } from '@/location/status';
import { supportsBackgroundTracking } from '@/location/tracking';
import type { TrackingMode } from '@/location/types';
import { flushQueue } from '@/location/uploader';
import { Body, Button, Card, Notice, Row, Screen } from '@/ui/components';
import { formatAccuracy, formatCoordinate, formatDateTime, formatPlace, sourceLabels } from '@/ui/format';
import { useTheme } from '@/ui/theme';

const modeLabels: Record<TrackingMode, string> = {
  foreground: 'Đang theo dõi (khi ứng dụng mở)',
  background: 'Đang theo dõi (cả khi ứng dụng đóng)',
  web: 'Đang theo dõi (khi tab này mở)',
};

export default function HomeScreen() {
  const router = useRouter();
  const theme = useTheme();
  const { mode, busy, notice } = useStore(trackingStore);
  const settings = useStore(settingsStore);
  const pending = useStore(pendingCountStore);
  const { lastPoint, lastSync, lastPlace } = useStore(statusStore);
  const placeLabel = lastPlace ? formatPlace(lastPlace) : null;
  const [flushing, setFlushing] = useState(false);

  const onStart = () => {
    if (settings.disclosureAccepted) {
      void startTracking();
    } else {
      router.push('/disclosure');
    }
  };

  const onToggleBackground = (enabled: boolean) => {
    if (enabled && !settings.disclosureAccepted) {
      router.push({ pathname: '/disclosure', params: { background: '1' } });
      return;
    }
    void setBackgroundEnabled(enabled);
  };

  const onFlush = async () => {
    setFlushing(true);
    try {
      await flushQueue();
    } finally {
      setFlushing(false);
    }
  };

  return (
    <Screen>
      {!isApiConfigured ? (
        <Notice>Chưa cấu hình EXPO_PUBLIC_API_URL nên không thể gửi dữ liệu lên máy chủ.</Notice>
      ) : null}

      {Platform.OS === 'web' ? (
        <Notice>
          Trên web, việc theo dõi chỉ chạy khi tab này đang mở. Đóng tab hoặc trình duyệt là dừng theo dõi. Muốn theo dõi
          cả khi đóng ứng dụng, hãy dùng ứng dụng trên điện thoại.
        </Notice>
      ) : null}

      <Card title="Trạng thái">
        <View style={styles.statusLine}>
          <View style={[styles.dot, { backgroundColor: mode ? theme.success : theme.muted }]} />
          <Body style={styles.statusText}>{mode ? modeLabels[mode] : 'Đã dừng'}</Body>
        </View>
        {notice ? <Notice>{notice}</Notice> : null}
        {mode ? (
          <Button label="Dừng theo dõi" variant="danger" onPress={() => void stopTracking()} loading={busy} />
        ) : (
          <Button label="Bắt đầu theo dõi" onPress={onStart} loading={busy} />
        )}

        {supportsBackgroundTracking ? (
          <View style={styles.switchRow}>
            <View style={styles.switchText}>
              <Body>Theo dõi nền</Body>
              <Body muted>Tiếp tục ghi vị trí khi ứng dụng đóng hoặc màn hình khoá.</Body>
            </View>
            <Switch
              value={settings.backgroundEnabled}
              onValueChange={onToggleBackground}
              disabled={busy}
              accessibilityLabel="Theo dõi nền"
            />
          </View>
        ) : null}
      </Card>

      <Card title="Điểm gần nhất">
        {lastPoint ? (
          <>
            <Row label="Vĩ độ" value={formatCoordinate(lastPoint.latitude)} />
            <Row label="Kinh độ" value={formatCoordinate(lastPoint.longitude)} />
            <Row label="Độ chính xác" value={formatAccuracy(lastPoint.accuracy)} />
            <Row label="Thời điểm" value={formatDateTime(lastPoint.recordedAt)} />
            {/* Place names come from the server, so they describe the newest point already uploaded. */}
            <Row label="Khu vực" value={placeLabel ?? (pending > 0 ? 'Chờ gửi lên máy chủ…' : 'Không xác định')} />
            <Row label="Nguồn" value={sourceLabels[lastPoint.source] ?? lastPoint.source} />
          </>
        ) : (
          <Body muted>Chưa có điểm nào.</Body>
        )}
      </Card>

      <Card title="Đồng bộ">
        <Row label="Đang chờ gửi" value={`${pending} điểm`} />
        {lastSync ? (
          <>
            <Row label="Lần gửi gần nhất" value={formatDateTime(lastSync.at)} />
            {lastSync.error ? <Body muted>{lastSync.error}</Body> : <Row label="Kết quả" value="Thành công" />}
          </>
        ) : null}
        <Button label="Gửi ngay" variant="secondary" onPress={() => void onFlush()} loading={flushing} disabled={pending === 0} />
      </Card>

      <View style={styles.links}>
        <View style={styles.link}>
          <Button label="Lịch sử" variant="secondary" onPress={() => router.push('/history')} />
        </View>
        <View style={styles.link}>
          <Button label="Cài đặt" variant="secondary" onPress={() => router.push('/settings')} />
        </View>
      </View>
    </Screen>
  );
}

const styles = StyleSheet.create({
  statusLine: { flexDirection: 'row', alignItems: 'center', gap: 8 },
  dot: { width: 10, height: 10, borderRadius: 5 },
  statusText: { fontWeight: '600' },
  switchRow: { flexDirection: 'row', alignItems: 'center', gap: 12, marginTop: 4 },
  switchText: { flex: 1 },
  links: { flexDirection: 'row', gap: 12 },
  link: { flex: 1 },
});
