import type { ExpoConfig } from 'expo/config';

const config: ExpoConfig = {
  name: 'DCMS Mobile',
  slug: 'dcms-mobile',
  version: '0.1.0',
  scheme: 'dcms',
  orientation: 'portrait',
  userInterfaceStyle: 'light',
  plugins: ['expo-router', 'expo-secure-store'],
  web: { bundler: 'metro' },
};
export default config;
