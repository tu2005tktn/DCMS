import { Typography } from 'antd';
import type { ReactNode } from 'react';
import dayjs from 'dayjs';
import { genderLabels, type Gender } from '../patients/types';
import './patientPrescriptions.css';

// Tờ đơn thuốc chỉ đọc dùng chung cho web staff và cổng bệnh nhân (docs/specs/S23-PATIENT-PRESCRIPTIONS.vi.md).
export type Item = { id?: string; medicineId: string; medicineName?: string; medicineCode?: string; strength?: string | null; dosageForm?: string | null; unit?: string; quantity: number; instructions?: string };
export type Prescription = {
  id: string; branchId: number; branchName: string; prescribedOn: string; diagnosis: string; otherDiagnosis?: string | null; advice?: string | null;
  prescriberName: string; prescriberEmployeeId: number; version: number; items: Item[];
  branchAddress?: string | null; branchPhone?: string | null; branchEmail?: string | null;
  patientCode: string; patientName: string; patientBirthDate?: string | null; patientGender?: string | null;
};

export const formatDate = (value?: string | null) => (value ? dayjs(value).format('DD/MM/YYYY') : '—');
export const genderText = (value?: string | null) => (value && value in genderLabels ? genderLabels[value as Gender] : value || '—');

export type Person = { name: string; code: string; birthDate?: string | null; gender?: string | null };

export function PatientGrid({ person }: { person: Person }) {
  return <div className="rx-grid">
    <div className="rx-row"><label>Họ và tên</label><strong>{person.name}</strong></div>
    <div className="rx-row"><label>Mã BN</label><strong>{person.code}</strong></div>
    <div className="rx-row"><label>Ngày sinh</label><span className="rx-box">{formatDate(person.birthDate)}</span></div>
    <div className="rx-row"><label>Giới tính</label><strong>{genderText(person.gender)}</strong></div>
  </div>;
}

export function ReadOnlySheet({ doc, person }: { doc: Prescription; person: Person }) {
  return <div className="rx-form">
    <section className="rx-section">
      <h3>BỆNH NHÂN</h3>
      <PatientGrid person={person} />
      <div className="rx-row"><label>Chẩn đoán</label><span>{doc.diagnosis || '—'}</span></div>
      <div className="rx-row"><label>Chẩn đoán khác</label><span>{doc.otherDiagnosis || '—'}</span></div>
    </section>
    <section className="rx-section">
      <h3>CHỈ ĐỊNH</h3>
      <ol className="rx-read-lines">
        {doc.items.map(item => <li key={item.id ?? item.medicineId}>
          <div className="rx-read-top"><strong>{item.medicineName}{item.strength ? ` · ${item.strength}` : ''}</strong><span>{item.quantity} {item.unit}</span></div>
          {item.instructions && <Typography.Text type="secondary">{item.instructions}</Typography.Text>}
        </li>)}
      </ol>
    </section>
    <section className="rx-section">
      <h3>GHI CHÚ/LỜI DẶN</h3>
      <p className="rx-advice">{doc.advice || '—'}</p>
    </section>
    <section className="rx-section">
      <h3>NGÀY &amp; BÁC SĨ KÊ ĐƠN</h3>
      <div className="rx-grid">
        <div className="rx-row"><label>Ngày</label><strong>{formatDate(doc.prescribedOn)}</strong></div>
        <div className="rx-row"><label>Bác sĩ</label><strong>{doc.prescriberName}</strong></div>
      </div>
    </section>
  </div>;
}

export function PrescriptionHeader({ name, phone, email, children }: { name: string; phone?: string | null; email?: string | null; children?: ReactNode }) {
  return <header className="rx-head">
    <div><h2 className="rx-title">ĐƠN THUỐC</h2>{children}</div>
    <div className="rx-clinic">
      <strong>{name}</strong>
      {(phone || email) && <div className="rx-contact">{phone && <span>☎ {phone}</span>}{email && <span>✉ {email}</span>}</div>}
    </div>
  </header>;
}

// Toàn bộ tờ đơn chỉ đọc từ DTO đơn đã lưu.
export function PrescriptionSheetView({ doc }: { doc: Prescription }) {
  return <div className="rx-sheet">
    <PrescriptionHeader name={doc.branchName} phone={doc.branchPhone} email={doc.branchEmail} />
    <ReadOnlySheet doc={doc} person={{ name: doc.patientName, code: doc.patientCode, birthDate: doc.patientBirthDate, gender: doc.patientGender }} />
  </div>;
}
