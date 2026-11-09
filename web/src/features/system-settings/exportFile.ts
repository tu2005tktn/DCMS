import { ApiError, readAccessToken } from '../../services/api';

// The file is fetched with the bearer token, so a plain link would not be authorized (DCMS-132 exports).
export async function downloadExport(path: string, fileName: string) {
  const apiBase = (import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5080').replace(/\/$/, '');
  const response = await fetch(`${apiBase}${path}`, { headers: { Authorization: `Bearer ${readAccessToken() ?? ''}` }, cache: 'no-store' });
  if (response.status === 403) throw new ApiError(403, 'MSG06', 'Bạn không có quyền xuất Excel danh mục này.');
  if (!response.ok) throw new ApiError(response.status, 'MSG144', 'Không thể xuất Excel. Vui lòng thử lại.');
  const url = URL.createObjectURL(await response.blob());
  const link = Object.assign(document.createElement('a'), { href: url, download: fileName });
  link.click(); URL.revokeObjectURL(url);
}
