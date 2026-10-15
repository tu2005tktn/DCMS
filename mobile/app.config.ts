import type { ExpoConfig } from 'expo/config';

const allowHttpApi = process.env.EXPO_PUBLIC_API_URL?.startsWith('http://') ?? false;

const config: ExpoConfig = {
  name: 'DCMS Mobile',
  slug: 'dcms-mobile',
  version: '0.1.0',
  scheme: 'dcms',
  android: { package: 'com.dcms.mobile' },
  orientation: 'portrait',
  userInterfaceStyle: 'light',
  plugins: [
    'expo-router',
    'expo-secure-store',
    ['expo-build-properties', { android: { usesCleartextTraffic: allowHttpApi } }],
  ],
  extra: { eas: { projectId: '44909f35-acee-42b9-9a21-b286ab11b80c' } },
  web: { bundler: 'metro' },
};
export default config;
