import React from 'react';
import ReactDOM from 'react-dom/client';
import { ConfigProvider } from 'antd';
import { BrowserRouter } from 'react-router-dom';
import { AuthProvider } from './features/authentication/AuthContext';
import App from './app/App';
import './app/styles.css';

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <ConfigProvider theme={{ token: { colorPrimary: '#175cd3', borderRadius: 10 } }}>
      <BrowserRouter><AuthProvider><App /></AuthProvider></BrowserRouter>
    </ConfigProvider>
  </React.StrictMode>,
);
