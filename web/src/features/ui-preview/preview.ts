import { createElement } from 'react';
import { Alert } from 'antd';

// UI preview mode: only when the web app is started with VITE_UI_PREVIEW=true, for reviewing screens whose
// backend is not ready yet. Without the flag every screen talks to the real API only.
export const uiPreview = import.meta.env.VITE_UI_PREVIEW === 'true';

export function PreviewBanner() {
  return uiPreview ? createElement(Alert, {
    className: 'form-alert', type: 'warning', showIcon: true,
    message: 'Xem trước giao diện — dữ liệu minh họa, chưa kết nối API',
    description: 'Không có thao tác nào được lưu. Tắt VITE_UI_PREVIEW để dùng API thật.',
  }) : null;
}

export const previewDelay = (ms = 300) => new Promise(resolve => setTimeout(resolve, ms));
