const apiUrl = process.env.EXPO_PUBLIC_API_URL?.trim() || 'http://localhost:5080';
if (!/^https?:\/\//.test(apiUrl)) throw new Error('EXPO_PUBLIC_API_URL must use http or https.');

// Backend S26 đã sẵn sàng; chỉ dùng mock khi chủ động đặt "false" để demo offline.
const useRealNotificationApi = process.env.EXPO_PUBLIC_USE_REAL_NOTIFICATION_API !== 'false';

export const env = {
  apiUrl: apiUrl.replace(/\/+$/, ''),
  useRealNotificationApi,
};
