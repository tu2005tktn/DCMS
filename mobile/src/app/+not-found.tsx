import { Link } from 'expo-router';
import { Text, View } from 'react-native';
export default function NotFoundScreen() {
  return <View style={{ padding: 24, gap: 16 }}><Text>Không tìm thấy màn hình.</Text><Link href="/">Về trang đầu</Link></View>;
}
