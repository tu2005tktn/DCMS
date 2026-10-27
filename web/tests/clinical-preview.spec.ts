import { expect, test } from '@playwright/test';

test('S22 and S63 preview uses synthetic data without contacting the real API', async ({ page }) => {
  const liveCalls: string[] = [];
  page.on('request', request => {
    if (request.url().startsWith('http://localhost:5080/api/')) liveCalls.push(request.url());
  });
  await page.goto('/__preview/clinical');
  await expect(page.getByText('Chế độ xem thử, không lưu vào hồ sơ thật')).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Ghi việc đã làm' })).toBeVisible();
  await page.getByLabel('Kết quả thực hiện').fill('Đã hoàn thành trám răng 25 trong dữ liệu giả');
  await page.getByRole('button', { name: 'Lưu nháp' }).click();
  await expect(page.getByText('Đã lưu nháp. Chưa ghi nhận việc điều trị hoặc khoản thu.')).toBeVisible();
  await page.getByRole('button', { name: 'Xác nhận đã làm' }).click();
  await expect(page.getByText('Đã ghi nhận hoạt động thực tế. Lượt khám và thanh toán vẫn là thao tác riêng.')).toBeVisible();
  await page.getByRole('button', { name: 'S63 · Đánh giá sau điều trị' }).click();
  await expect(page.getByRole('heading', { name: 'Đánh giá sau điều trị' })).toBeVisible();
  await page.getByRole('combobox', { name: 'Hoạt động đã hoàn tất' }).click();
  await page.locator('.ant-select-item-option').filter({ hasText: 'Dịch vụ #3' }).first().click();
  await page.getByLabel('Người bệnh phản ánh').fill('Ê buốt sau trám');
  await page.getByLabel('Bác sĩ đánh giá').fill('Theo dõi trong bản giả');
  await page.getByRole('button', { name: 'Lưu đánh giá' }).click();
  await expect(page.getByText('Đã lưu đánh giá. Chưa ghi hoạt động điều trị hoặc khoản thu.')).toBeVisible();
  expect(liveCalls).toEqual([]);
});
