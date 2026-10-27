import { useRouter } from 'expo-router';
import { useEffect, useState } from 'react';
import { Platform } from 'react-native';

import { describeError } from '@/api/http';
import { API_URL } from '@/config';
import { getStoredDevice } from '@/identity/ensureDevice';
import { useStore } from '@/lib/store';
import { deleteMyData, updateTrackingSettings } from '@/location/controller';
import { settingsStore, type AccuracyLevel } from '@/location/settings';
import { Body, Button, Card, Choice, Row, Screen } from '@/ui/components';
import { confirm, notify } from '@/ui/confirm';

const intervalOptions = [15, 30, 60, 120].map((value) => ({ label: `${value} giây`, value }));
const distanceOptions = [10, 25, 50, 100].map((value) => ({ label: `${value} m`, value }));
const accuracyOptions: { label: string; value: AccuracyLevel }[] = [
  { label: 'Thấp', value: 'low' },
  { label: 'Cân bằng', value: 'balanced' },
  { label: 'Cao', value: 'high' },
];

export default function SettingsScreen() {
  const router = useRouter();
  const settings = useStore(settingsStore);
  const [deviceId, setDeviceId] = useState<string | null>(null);
  const [deleting, setDeleting] = useState(false);

  useEffect(() => {
    void getStoredDevice().then((device) => setDeviceId(device?.deviceId ?? null));
  }, []);

  const onDelete = async () => {
    const ok = await confirm(
      'Xoá dữ liệu của tôi?',
      'Theo dõi sẽ dừng, toàn bộ điểm vị trí của thiết bị này trên máy chủ và trên máy sẽ bị xoá. Không thể hoàn tác.',
      'Xoá',
    );
    if (!ok) {
      return;
    }

    setDeleting(true);
    try {
      await deleteMyData();
      setDeviceId(null);
      notify('Đã xoá', 'Toàn bộ dữ liệu vị trí của thiết bị này đã được xoá.');
      router.back();
    } catch (error) {
      notify('Chưa xoá được', `${describeError(error)} Dữ liệu chưa bị xoá, hãy thử lại.`);
    } finally {
      setDeleting(false);
    }
  };

  return (
    <Screen>
      <Card title="Khoảng thời gian giữa hai lần ghi">
        <Choice
          options={intervalOptions}
          value={settings.timeIntervalSec}
          onChange={(timeIntervalSec) => void updateTrackingSettings({ timeIntervalSec })}
        />
        {Platform.OS === 'ios' ? (
          <Body muted>iOS không hỗ trợ khoảng thời gian cố định; hệ thống dựa vào khoảng cách di chuyển.</Body>
        ) : null}
      </Card>

      <Card title="Khoảng cách tối thiểu">
        <Choice
          options={distanceOptions}
          value={settings.distanceIntervalM}
          onChange={(distanceIntervalM) => void updateTrackingSettings({ distanceIntervalM })}
        />
      </Card>

      <Card title="Độ chính xác">
        <Choice
          options={accuracyOptions}
          value={settings.accuracy}
          onChange={(accuracy) => void updateTrackingSettings({ accuracy })}
        />
        <Body muted>Độ chính xác cao tốn pin hơn.</Body>
      </Card>

      <Card title="Dữ liệu của tôi">
        <Body>Dừng theo dõi và xoá vĩnh viễn mọi điểm vị trí của thiết bị này, trên máy chủ và trên máy.</Body>
        <Button label="Xoá dữ liệu của tôi" variant="danger" onPress={() => void onDelete()} loading={deleting} />
      </Card>

      <Card title="Thông tin">
        <Row label="Mã thiết bị" value={deviceId ? `${deviceId.slice(0, 8)}…` : 'Chưa đăng ký'} />
        <Row label="Máy chủ" value={API_URL || 'Chưa cấu hình'} />
        <Body muted>Tên thành phố, tỉnh và quốc gia: dữ liệu GeoNames (geonames.org), giấy phép CC BY 4.0.</Body>
      </Card>
    </Screen>
  );
}
