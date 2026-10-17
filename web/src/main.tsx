import React from 'react';
import ReactDOM from 'react-dom/client';
import { ConfigProvider } from 'antd';
import { BrowserRouter } from 'react-router-dom';
import { AuthProvider } from './features/authentication/AuthContext';
import { CustomerAuthProvider } from './features/customer-auth/CustomerAuthContext';
import App from './app/App';
import './app/styles.css';

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <ConfigProvider theme={{
      token: {
        colorPrimary: '#2563eb',
        colorInfo: '#2563eb',
        colorSuccess: '#16a34a',
        colorError: '#dc2626',
        colorWarning: '#d97706',
        borderRadius: 10,
        fontFamily: "Inter, -apple-system, 'Segoe UI', Roboto, sans-serif",
        fontSize: 14,
        colorText: '#17233d',
        colorTextSecondary: '#5a6b85',
        colorBorder: '#e4e9f2',
        colorBgContainer: '#ffffff',
        boxShadow: '0 1px 2px rgba(12, 30, 58, 0.04), 0 12px 32px -16px rgba(12, 30, 58, 0.16)',
      },
      components: {
        Card: { boxShadowTertiary: '0 1px 2px rgba(12, 30, 58, 0.04), 0 12px 32px -16px rgba(12, 30, 58, 0.16)' },
        Button: { controlHeight: 38, fontWeight: 600, primaryShadow: '0 6px 16px -6px rgba(37, 99, 235, 0.5)' },
        Table: { headerBg: '#f7f9fc', headerColor: '#5a6b85', rowHoverBg: '#f7f9fc', borderRadius: 12, borderRadiusOuter: 12 },
        Input: { controlHeightLG: 42 },
        Tag: { borderRadiusSM: 999 },
        Menu: { itemHeight: 44, itemBorderRadius: 10, itemMarginInline: 10 },
      },
    }}>
      <BrowserRouter><AuthProvider><App /></AuthProvider></BrowserRouter>
    <ConfigProvider theme={{ token: { colorPrimary: '#175cd3', borderRadius: 10 } }}>
      <BrowserRouter><AuthProvider><CustomerAuthProvider><App /></CustomerAuthProvider></AuthProvider></BrowserRouter>
    </ConfigProvider>
  </React.StrictMode>,
);
