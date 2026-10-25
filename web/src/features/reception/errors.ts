export function receptionErrorPrefix(status: number, code: string): string {
  if (status === 0) return 'Mất kết nối';
  if (status === 401) return 'Phiên đăng nhập đã hết hạn';
  if (status === 403) return 'Thiếu quyền';
  if (status === 409 && code === 'MSG111') return 'Dữ liệu đã thay đổi';
  if (code === 'MSG43') return 'Cần đối chiếu người đến khám';
  if (['MSG26', 'MSG27', 'MSG28'].includes(code)) return 'Khung giờ hoặc tài nguyên không còn phù hợp';
  if (status === 409) return 'Chưa thể thực hiện thao tác';
  if (status >= 500) return 'Chưa thể lưu';
  return 'Kiểm tra thông tin';
}
