const apiUrl = process.env.EXPO_PUBLIC_API_URL?.trim() || 'http://localhost:5080';
if (!/^https?:\/\//.test(apiUrl)) throw new Error('EXPO_PUBLIC_API_URL must use http or https.');

// Mặc định: dùng mock cho S26 cho đến khi backend TV5 expose endpoint.
// Bật "true" để chuyển sang real API khi backend sẵn sàng.
const useRealNotificationApi = process.env.EXPO_PUBLIC_USE_REAL_NOTIFICATION_API === 'true';

export const env = {
  apiUrl: apiUrl.replace(/\/+$/, ''),
  useRealNotificationApi,
};
