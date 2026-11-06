-- Synthetic local-only examination with an unpaid 100,000 VND charge.
-- Never run against patient data or another database.
BEGIN;
DO $$ BEGIN
  IF current_database() <> 'dcms_schema_check_20261106' THEN
    RAISE EXCEPTION 'Payment demo seed is restricted to dcms_schema_check_20261106';
  END IF;
END $$;

INSERT INTO "RESOURCE_RESERVATION" ("Id","Code","BranchId","DentistId","ChairId","ServiceId","Start","End","AttendeeLabel","Status")
VALUES ('9e7d59b0-6d7c-4f7c-8d16-000000000001','RS-PAYMENT-DEMO-20261106',1,2,1,1,
  '2026-11-06 09:00:00+07','2026-11-06 09:30:00+07','Nguyễn Văn An (DEMO thanh toán)','Confirmed')
ON CONFLICT ("Id") DO NOTHING;

INSERT INTO "APPOINTMENT" ("Id","ReservationId","Code","BranchId","BookerAccountId","BookerName","PatientId",
  "AttendeeName","AttendeeBirthDate","AttendeeRelation","Kind","Source","ServiceId","Reason","DentistId","ChairId",
  "StartAt","EndAt","Status","Version","CreatedAt","ConfirmedToAttend")
VALUES ('9e7d59b0-6d7c-4f7c-8d16-000000000001','9e7d59b0-6d7c-4f7c-8d16-000000000001',
  'AP-PAYMENT-DEMO-20261106',1,3,'Chủ phòng khám demo',1,'Nguyễn Văn An','1990-05-12','Self',
  'Initial','Staff',1,'DEMO: ê nhẹ răng hàm khi ăn đồ lạnh',2,1,
  '2026-11-06 09:00:00+07','2026-11-06 09:30:00+07','Completed',1,'2026-11-06 08:50:00+07','Confirmed')
ON CONFLICT ("Id") DO NOTHING;

INSERT INTO "VISIT" ("Id","AppointmentId","PatientId","BranchId","DentistId","Status","Version",
  "ArrivalAt","RecordedAt","RecordedBy","ArrivalReason","Purpose","Note","RequiresClinicalSourceReview")
VALUES ('9e7d59b0-6d7c-4f7c-8d16-000000000002','9e7d59b0-6d7c-4f7c-8d16-000000000001',1,1,2,'Completed',3,
  '2026-11-06 08:55:00+07','2026-11-06 08:56:00+07',3,'Ê răng hàm khi ăn đồ lạnh',
  'InitialExamination','Ca tổng hợp chỉ để thử thanh toán; chưa thu tiền.',false)
ON CONFLICT ("Id") DO NOTHING;

INSERT INTO "VISIT_EXAMINATION" ("VisitId","ContentJson","Confirmed","UpdatedBy","UpdatedAt")
VALUES ('9e7d59b0-6d7c-4f7c-8d16-000000000002',
  '{"symptoms":"Ê nhẹ răng hàm khi ăn đồ lạnh","medicalHistory":{"state":"Unknown"},"allergies":{"state":"Unknown"},"medications":{"state":"Unknown"},"findings":[],"diagnosis":"Sâu răng nhẹ, cần theo dõi","notes":"Ca DEMO để thử thanh toán","conclusion":"Chưa có dấu hiệu cấp cứu","advice":"Vệ sinh răng miệng và tái khám khi đau tăng","disposition":"FollowUp"}'::jsonb,
  true,2,'2026-11-06 09:20:00+07')
ON CONFLICT ("VisitId") DO NOTHING;

INSERT INTO "FINANCIAL_ACCOUNT" ("PatientId","BranchId","Version")
VALUES (1,1,1) ON CONFLICT ("PatientId","BranchId") DO NOTHING;

INSERT INTO "FINANCE_CHARGE" ("Id","PatientId","BranchId","VisitId","SourceKind","SourceId","ServiceId",
  "Name","Unit","Quantity","UnitPriceVnd","AmountVnd","PriceVersion","PostedBy","PostedAt")
VALUES ('9e7d59b0-6d7c-4f7c-8d16-000000000003',1,1,'9e7d59b0-6d7c-4f7c-8d16-000000000002',
  'Examination','9e7d59b0-6d7c-4f7c-8d16-000000000002',1,'Khám','lần',1,100000,100000,1,2,'2026-11-06 09:25:00+07')
ON CONFLICT ("Id") DO NOTHING;

INSERT INTO "EXAMINATION_FEE" ("VisitId","State","Version","ServiceId","ServiceName","Unit","AmountVnd",
  "PriceVersion","Reason","PriceSource","ChargeId","CreatedBy","CreatedAt","PostedBy","PostedAt")
VALUES ('9e7d59b0-6d7c-4f7c-8d16-000000000002','Posted',1,1,'Khám','lần',100000,1,
  'Giá thỏa thuận cho ca demo kiểm thử thanh toán','Override','9e7d59b0-6d7c-4f7c-8d16-000000000003',
  2,'2026-11-06 09:20:00+07',2,'2026-11-06 09:25:00+07')
ON CONFLICT ("VisitId") DO NOTHING;
COMMIT;
