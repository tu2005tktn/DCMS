const apiUrl = process.env.EXPO_PUBLIC_API_URL?.trim() || 'http://localhost:5080';
if (!/^https?:\/\//.test(apiUrl)) throw new Error('EXPO_PUBLIC_API_URL must use http or https.');
export const env = { apiUrl: apiUrl.replace(/\/+$/, '') };
