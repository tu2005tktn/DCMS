import { BellOutlined } from '@ant-design/icons';
import { Alert, Badge, Button, Drawer, Empty, Grid, Segmented, Spin, Tag, Typography } from 'antd';
import { useCallback, useEffect, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { ApiError } from '../../services/api';
import { useCustomerAuth } from '../customer-auth/CustomerAuthContext';
import { customerNotificationsApi, subscribeNotifications, webRouteFor, type CustomerNotification } from './notifications.api';
import './customerNotifications.css';

// S26 web customer notifications (docs/specs/S26-CUSTOMER-NOTIFICATIONS-WEB.vi.md).

const formatTime = (value: string) => {
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? value : new Intl.DateTimeFormat('vi-VN', {
    timeZone: 'Asia/Ho_Chi_Minh', hour: '2-digit', minute: '2-digit', day: '2-digit', month: '2-digit', year: 'numeric', hour12: false,
  }).format(date);
};

export default function NotificationBell() {
  const { state, expire } = useCustomerAuth();
  const navigate = useNavigate();
  const screens = Grid.useBreakpoint();
  const token = state.status === 'authenticated' ? state.accessToken : null;
  const accountId = state.status === 'authenticated' ? state.user.id : null;

  const [open, setOpen] = useState(false);
  const [unread, setUnread] = useState(0);
  const [unreadOnly, setUnreadOnly] = useState(false);
  const [items, setItems] = useState<CustomerNotification[]>([]);
  const [cursor, setCursor] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const latest = useRef(0);

  const fail = useCallback((failure: unknown, fallback: string) => {
    if (failure instanceof ApiError && failure.status === 401) { expire(); return; }
    setError(failure instanceof Error ? failure.message : fallback);
  }, [expire]);

  const refreshCount = useCallback(() => {
    if (!token) return;
    customerNotificationsApi.unreadCount(token).then(value => setUnread(value.count)).catch(failure => {
      if (failure instanceof ApiError && failure.status === 401) expire();
    });
  }, [token, expire]);

  const loadPage = useCallback((append: boolean, from: string | null) => {
    if (!token) return;
    const request = ++latest.current;
    setLoading(true); setError(null);
    customerNotificationsApi.list(token, unreadOnly, from).then(page => {
      if (request !== latest.current) return;
      setItems(current => append ? [...current, ...page.items.filter(item => !current.some(x => x.id === item.id))] : page.items);
      setCursor(page.nextCursor);
    }).catch(failure => { if (request === latest.current) fail(failure, 'Không thể tải thông báo.'); })
      .finally(() => { if (request === latest.current) setLoading(false); });
  }, [token, unreadOnly, fail]);

  // Reset everything when the account changes or signs out.
  useEffect(() => { setItems([]); setCursor(null); setUnread(0); setOpen(false); }, [accountId]);

  useEffect(() => {
    if (!token) return;
    refreshCount();
    const timer = window.setInterval(refreshCount, 60_000);
    const onFocus = () => refreshCount();
    window.addEventListener('focus', onFocus);
    return () => { window.clearInterval(timer); window.removeEventListener('focus', onFocus); };
  }, [token, refreshCount]);

  const openRef = useRef(open);
  openRef.current = open;
  const loadRef = useRef(loadPage);
  loadRef.current = loadPage;
  useEffect(() => {
    if (!token) return;
    return subscribeNotifications(token, () => {
      refreshCount();
      if (openRef.current) loadRef.current(false, null);
    });
  }, [token, refreshCount]);

  useEffect(() => { if (open) loadPage(false, null); }, [open, loadPage]);

  async function openItem(item: CustomerNotification) {
    if (!token || accountId === null) return;
    setNotice(null);
    if (!item.readAt) {
      try {
        await customerNotificationsApi.markReadOne(token, item.id, accountId);
        const readAt = new Date().toISOString();
        setItems(current => unreadOnly ? current.filter(x => x.id !== item.id) : current.map(x => x.id === item.id ? { ...x, readAt } : x));
        setUnread(count => Math.max(0, count - 1));
      } catch (failure) { fail(failure, 'Không thể cập nhật thông báo.'); return; }
    }
    const route = webRouteFor(item.deepLink);
    if (!route || !item.deepLink) return;
    try {
      const check = await customerNotificationsApi.checkDeepLink(token, item.deepLink);
      if (!check.allowed) { setNotice(check.reason ?? 'Bạn không có quyền mở thông báo này.'); return; }
      setOpen(false);
      navigate(route);
    } catch (failure) { fail(failure, 'Không thể kiểm tra quyền mở thông báo.'); }
  }

  async function markAll() {
    if (!token || accountId === null) return;
    const ids = items.filter(item => !item.readAt).map(item => item.id);
    if (ids.length === 0) return;
    try {
      await customerNotificationsApi.markRead(token, ids, accountId);
      loadPage(false, null);
      refreshCount();
    } catch (failure) { fail(failure, 'Không thể cập nhật thông báo.'); }
  }

  if (!token) return null;
  const hasUnreadShown = items.some(item => !item.readAt);

  return <>
    <Badge count={unread} overflowCount={99} size="small" offset={[-4, 4]}>
      <Button type="text" className="customer-notification-bell" icon={<BellOutlined aria-hidden="true" />}
        aria-label={`Thông báo, ${unread} chưa đọc`} onClick={() => setOpen(true)} />
    </Badge>
    <Drawer open={open} onClose={() => setOpen(false)} title="Thông báo" width={screens.sm ? 420 : '100%'}
      className="customer-notification-drawer" destroyOnClose
      extra={<Button type="link" disabled={!hasUnreadShown} onClick={() => void markAll()}>Đánh dấu tất cả đã đọc</Button>}>
      <Segmented block value={unreadOnly ? 'unread' : 'all'} onChange={value => setUnreadOnly(value === 'unread')}
        options={[{ label: 'Tất cả', value: 'all' }, { label: 'Chưa đọc', value: 'unread' }]} />
      {notice && <Alert className="customer-notification-alert" type="warning" showIcon message={notice} closable onClose={() => setNotice(null)} />}
      {error && <Alert className="customer-notification-alert" type="error" showIcon message={error}
        action={<Button size="small" onClick={() => loadPage(false, null)}>Thử lại</Button>} />}
      {items.length === 0 && loading ? <div className="customer-notification-center"><Spin /></div>
        : items.length === 0 && !error ? <Empty className="customer-notification-center" description={unreadOnly ? 'Không có thông báo chưa đọc' : 'Chưa có thông báo'} />
          : <ul className="customer-notification-list">
            {items.map(item => <li key={item.id}>
              <button type="button" className={`customer-notification-item${item.readAt ? '' : ' unread'}`} onClick={() => void openItem(item)}>
                <span className="customer-notification-head">
                  {!item.readAt && <span className="customer-notification-dot" aria-label="Chưa đọc" />}
                  <Typography.Text strong>{item.title}</Typography.Text>
                </span>
                <Typography.Text className="customer-notification-body">{item.body}</Typography.Text>
                <span className="customer-notification-meta">
                  <Typography.Text type="secondary">{formatTime(item.createdAt)}</Typography.Text>
                  {item.hasExternalFailure && <Tag color="orange">Gửi email/push thất bại</Tag>}
                </span>
              </button>
            </li>)}
          </ul>}
      {cursor && <Button block loading={loading} onClick={() => loadPage(true, cursor)}>Xem thêm</Button>}
    </Drawer>
  </>;
}
