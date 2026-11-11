import type { ExpoConfig } from 'expo/config';
import { existsSync } from 'node:fs';
import { join } from 'node:path';

const allowHttpApi = process.env.EXPO_PUBLIC_API_URL?.startsWith('http://') ?? false;

const config: ExpoConfig = {
  name: 'DCMS Mobile',
  slug: 'dcms-mobile',
  version: '0.1.0',
  scheme: 'dcms',
  icon: './assets/dcms-app-icon.png',
  android: { package: 'com.dcms.mobile',
    icon: './assets/dcms-app-icon.png',
    adaptiveIcon: { foregroundImage: './assets/dcms-app-icon.png', backgroundColor: '#175CD3' },
    ...(existsSync(join(__dirname, 'google-services.json')) ? { googleServicesFile: './google-services.json' } : {}) },
  orientation: 'portrait',
  userInterfaceStyle: 'light',
  plugins: [
    'expo-router',
    'expo-secure-store',
    ['expo-notifications', { defaultChannel: 'appointments' }],
    ['expo-build-properties', { android: { usesCleartextTraffic: allowHttpApi } }],
  ],
  extra: { eas: { projectId: '44909f35-acee-42b9-9a21-b286ab11b80c' } },
  web: { bundler: 'metro' },
};
export default config;
