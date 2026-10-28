import { Alert, Button, Empty, Input, Select, Space, Table, Tag, Typography } from 'antd';
import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { api } from '../../services/api';
import { arrivalClock, visitLabels } from '../reception/types';
import { dispositionLabels, type PatientExamination } from './types';

export default function PatientExaminationsTab({ patientId, active }: { patientId: number; active: boolean }) {
  const [rows, setRows] = useState<PatientExamination[]>();
  const [error, setError] = useState<string>();
  const [revision, setRevision] = useState(0);
  const [keyword, setKeyword] = useState('');
  const [state, setState] = useState<string>();
  useEffect(() => {
    setRows(undefined); setError(undefined);
    if (!active) return;
    let current = true;
    api<PatientExamination[]>(`/api/patients/${patientId}/examinations`).then(value => { if (current) setRows(value); })
      .catch(e => { if (current) setError((e as Error).message); });
    return () => { current = false; };
  }, [patientId, active, revision]);
  const visible = rows?.filter(r => (!state || (state === 'new' ? !r.examination : state === 'final' ? r.examination?.confirmed : r.examination && !r.examination.confirmed))
    && `${r.appointmentCode} ${r.dentistName} ${r.examination?.content.diagnosis ?? ''}`.toLocaleLowerCase('vi').includes(keyword.toLocaleLowerCase('vi')));
  return <section aria-label="Phiếu khám của bệnh nhân">
    <Typography.Title level={4}>Phiếu khám</Typography.Title>
    <Typography.Paragraph type="secondary">Kết quả khám, chẩn đoán và hướng xử trí của từng lần đến. Mở lượt đã tiếp nhận để bắt đầu khám.</Typography.Paragraph>
    <Space wrap style={{ marginBottom: 16 }}>
      <Input aria-label="Tìm phiếu khám" placeholder="Lịch hẹn, bác sĩ hoặc chẩn đoán" value={keyword} onChange={e => setKeyword(e.target.value)} />
      <Select aria-label="Trạng thái phiếu khám" allowClear placeholder="Tất cả phiếu" style={{ width: 180 }} value={state} onChange={setState}
        options={[{ value: 'new', label: 'Chưa lập phiếu' }, { value: 'draft', label: 'Bản nháp' }, { value: 'final', label: 'Đã hoàn tất khám' }]} />
      <Button onClick={() => setRevision(r => r + 1)}>Tải lại phiếu khám</Button>
      <Link to="/reception">Mở hàng chờ tiếp nhận</Link>
    </Space>
    {error ? <Alert type="error" showIcon message={error} action={<Button onClick={() => setRevision(r => r + 1)}>Thử lại</Button>} /> :
      <Table rowKey="visitId" loading={!rows} dataSource={visible} pagination={{ defaultPageSize: 10, showSizeChanger: true }}
        locale={{ emptyText: <Empty description="Chưa có lượt khám trong các chi nhánh được cấp quyền." /> }} scroll={{ x: 850 }} columns={[
          { title: 'Ngày đến', dataIndex: 'arrivalAt', render: arrivalClock },
          { title: 'Bác sĩ / chi nhánh', render: (_, r: PatientExamination) => <>{r.dentistName}<br /><Typography.Text type="secondary">{r.branchName}</Typography.Text></> },
          { title: 'Phiếu khám', render: (_, r: PatientExamination) => <><Tag color={r.examination?.confirmed ? 'green' : 'default'}>{!r.examination ? 'Chưa lập' : r.examination.confirmed ? 'Đã hoàn tất khám' : 'Bản nháp'}</Tag>{r.examination && <div>Phiên bản {r.examination.version ?? 0}</div>}</> },
          { title: 'Chẩn đoán / hướng xử trí', render: (_, r: PatientExamination) => <>{r.examination?.content.diagnosis ?? 'Chưa có chẩn đoán'}<br />{dispositionLabels[r.examination?.content.disposition ?? '']}</> },
          { title: 'Lượt đến', render: (_, r: PatientExamination) => <Tag>{visitLabels[r.visitStatus]}</Tag> },
          { title: 'Thao tác', render: (_, r: PatientExamination) => <Link to={`/visits/${r.visitId}`}>{r.visitStatus === 'Queued' ? 'Mở khám' : 'Xem phiếu khám'}</Link> },
        ]} />}
    {rows?.length === 100 && <Typography.Paragraph type="secondary">Đang hiển thị 100 lượt gần nhất trong quyền truy cập.</Typography.Paragraph>}
  </section>;
}
