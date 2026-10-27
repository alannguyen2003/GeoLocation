import { useColorScheme } from 'react-native';

const light = {
  background: '#F4F6F8',
  card: '#FFFFFF',
  text: '#11181C',
  muted: '#5F6B76',
  border: '#DCE1E6',
  primary: '#1A73E8',
  primaryText: '#FFFFFF',
  danger: '#C62828',
  success: '#2E7D32',
  warningBackground: '#FFF4E5',
  warningText: '#7A4A00',
};

export type Theme = typeof light;

const dark: Theme = {
  background: '#0F1316',
  card: '#1A2026',
  text: '#ECEDEE',
  muted: '#9BA7B1',
  border: '#2C353D',
  primary: '#5E9EFF',
  primaryText: '#0B1220',
  danger: '#FF6B6B',
  success: '#66BB6A',
  warningBackground: '#3A2A10',
  warningText: '#FFD089',
};

export function useTheme(): Theme {
  return useColorScheme() === 'dark' ? dark : light;
}
