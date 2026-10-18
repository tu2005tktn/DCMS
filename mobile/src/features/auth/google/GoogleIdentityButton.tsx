import Constants, { ExecutionEnvironment } from 'expo-constants';
import { useState } from 'react';
import { Platform, Text } from 'react-native';
import { PrimaryButton } from '@/components/ui/PrimaryButton';
import { ApiError } from '@/services/api/api-error';
import { colors } from '@/theme/colors';
import { googleAuthApi } from './google-auth.api';

// The native Google module is not part of Expo Go; loading it there crashes the whole app at startup.
// It is required lazily and only in a development/standalone build.
const inExpoGo = Constants.executionEnvironment === ExecutionEnvironment.StoreClient;
type GoogleModule = typeof import('@react-native-google-signin/google-signin');
const loadGoogle = (): GoogleModule => require('@react-native-google-signin/google-signin') as GoogleModule;

interface Props {
  onCredential: (token: string) => Promise<void>;
  onError: (message: string) => void;
}

export function GoogleIdentityButton({ onCredential, onError }: Props) {
  const [busy, setBusy] = useState(false);

  async function handlePress() {
    if (busy) return;
    setBusy(true);
    onError('');
    const { GoogleSignin, isErrorWithCode, isSuccessResponse, statusCodes } = loadGoogle();
    try {
      const config = await googleAuthApi.config();
      if (!config.enabled || !config.clientId) {
        onError('Đăng nhập Google chưa được cấu hình trên máy chủ.');
        return;
      }

      GoogleSignin.configure({ webClientId: config.clientId });
      await GoogleSignin.hasPlayServices({ showPlayServicesUpdateDialog: true });
      const response = await GoogleSignin.signIn();
      if (!isSuccessResponse(response)) return;
      if (!response.data.idToken) {
        onError('Google không trả về mã xác thực. Kiểm tra OAuth Client ID loại Web và Android.');
        return;
      }
      await onCredential(response.data.idToken);
    } catch (error) {
      if (isErrorWithCode(error) && error.code === statusCodes.SIGN_IN_CANCELLED) return;
      if (isErrorWithCode(error) && error.code === statusCodes.PLAY_SERVICES_NOT_AVAILABLE) {
        onError('Thiết bị cần Google Play Services để đăng nhập.');
      } else if (error instanceof ApiError) {
        onError(error.message);
      } else {
        onError('Không thể đăng nhập bằng Google. Kiểm tra cấu hình OAuth Android và thử lại.');
      }
    } finally {
      setBusy(false);
    }
  }

  if (inExpoGo) {
    return <Text style={{ color: colors.muted, textAlign: 'center' }}>Đăng nhập Google cần bản build Android riêng, không chạy trên Expo Go.</Text>;
  }

  if (Platform.OS !== 'android') {
    return <Text style={{ color: colors.muted, textAlign: 'center' }}>Đăng nhập Google hiện được cấu hình cho Android.</Text>;
  }

  return <PrimaryButton
    label={busy ? 'Đang kết nối Google…' : 'Đăng nhập bằng Google'}
    onPress={() => void handlePress()}
    loading={busy}
    disabled={busy}
    testID="google-sign-in"
  />;
}
