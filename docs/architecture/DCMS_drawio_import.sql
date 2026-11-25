CREATE TABLE ACCESS_GROUP (
  Id uuid PRIMARY KEY,
  RecordKind varchar(24) NOT NULL,
  IdentityRoleId integer,
  Code varchar(40) NOT NULL,
  Name varchar(160) NOT NULL,
  Description varchar(1000),
  BaseRole varchar(40) NOT NULL,
  Active boolean NOT NULL,
  Grants jsonb NOT NULL
);

CREATE TABLE APPOINTMENT (
  Id uuid PRIMARY KEY,
  Code varchar(40) NOT NULL,
  BranchId integer NOT NULL,
  BookerAccountId integer,
  FamilyMemberId uuid,
  PatientId integer,
  AttendeeName varchar(100) NOT NULL,
  Kind varchar(16) NOT NULL,
  Source varchar(24) NOT NULL,
  ServiceId integer,
  Reason varchar(1000),
  DentistId integer NOT NULL,
  ChairId integer NOT NULL,
  StartAt timestamptz NOT NULL,
  EndAt timestamptz NOT NULL,
  DossierId uuid,
  Status varchar(24) NOT NULL,
  CreatedAt timestamptz NOT NULL,
  BookingInfo jsonb NOT NULL,
  Reminders jsonb NOT NULL,
  SupportRequests jsonb NOT NULL
);

CREATE TABLE AUDIT_LOG (
  StorageId uuid PRIMARY KEY,
  AuditType varchar(34) NOT NULL,
  LegacyNumber bigint,
  ActorId integer,
  TargetId integer,
  OccurredAt timestamptz,
  Action varchar(64),
  Reason varchar(1000),
  BeforeJson jsonb,
  AfterJson jsonb,
  LegacyId uuid,
  AppointmentId uuid,
  ActorAccountId integer,
  ActorKind varchar(16),
  OldStart timestamptz,
  OldEnd timestamptz,
  OldDentistId integer,
  OldChairId integer,
  NewStart timestamptz,
  NewEnd timestamptz,
  NewDentistId integer,
  NewChairId integer,
  RequesterVerification varchar(500),
  RequestKey uuid,
  RequestId uuid,
  Notes varchar(500),
  ChannelType varchar(16),
  Result varchar(20),
  DocumentId uuid,
  Note varchar(500),
  AccessId uuid,
  LinkId integer,
  PatientId integer,
  ChangesJson jsonb,
  BranchId integer,
  VisitId uuid,
  TemplateId uuid,
  At timestamptz,
  BranchIds integer[],
  Section varchar(40),
  Target varchar(100),
  DossierId uuid,
  Kind varchar(80),
  PayloadJson jsonb,
  EventActorId integer,
  DossierVersion integer,
  AggregateId uuid,
  StableKey varchar(160),
  DecidingDentistId integer
);

CREATE TABLE CARE_TASK (
  Id uuid PRIMARY KEY,
  PatientId integer NOT NULL,
  BranchId integer NOT NULL,
  AppointmentId uuid,
  Type varchar(40) NOT NULL,
  Content varchar(4000) NOT NULL,
  OwnerAccountId integer,
  DueAt timestamptz NOT NULL,
  Status varchar(16) NOT NULL,
  ContactAttempts jsonb NOT NULL
);

CREATE TABLE CATALOG_MEDICINE (
  Id uuid PRIMARY KEY,
  Code varchar(40) NOT NULL,
  Name varchar(200) NOT NULL,
  BaseUnit varchar(40) NOT NULL,
  ReferencePrice bigint,
  PrescriptionEligible boolean NOT NULL,
  InternalStockTracked boolean NOT NULL,
  IsActive boolean NOT NULL,
  Spec jsonb NOT NULL
);

CREATE TABLE CATALOG_PROVIDER (
  Id uuid PRIMARY KEY,
  Type varchar(16) NOT NULL,
  Address varchar(1000),
  Phone varchar(20),
  Email varchar(254),
  Code varchar(40) NOT NULL,
  Name varchar(160) NOT NULL,
  IsActive boolean NOT NULL
);

CREATE TABLE CLINICAL_ACTIVITY (
  Id uuid PRIMARY KEY,
  DossierId uuid NOT NULL,
  ItemId uuid,
  PlanVersion integer,
  IsPlanTransfer boolean NOT NULL,
  ServiceId integer NOT NULL,
  ServiceNameSnapshot varchar(160),
  Region varchar(80),
  Unit varchar(40) NOT NULL,
  Quantity numeric NOT NULL,
  LabWorkSnapshot varchar(1000),
  PerformedAt timestamptz NOT NULL,
  PerformerId integer NOT NULL,
  AssistantId integer,
  Outcome varchar(4000) NOT NULL,
  PriceSnapshotVnd numeric NOT NULL,
  DiscountSnapshotVnd bigint NOT NULL,
  FeeStatus varchar(24) NOT NULL,
  CreatedAt timestamptz NOT NULL
);

CREATE TABLE CLINICAL_DOCUMENT (
  Id uuid PRIMARY KEY,
  PatientId integer NOT NULL,
  VisitId uuid,
  DossierId uuid,
  FileName varchar(255) NOT NULL,
  ContentType varchar(64) NOT NULL,
  DocumentType varchar(24) NOT NULL,
  SourceDate date NOT NULL,
  FileState varchar(16) NOT NULL,
  UploaderId integer NOT NULL,
  UploadedAt timestamptz NOT NULL,
  AiAnalyses jsonb NOT NULL,
  Metadata jsonb NOT NULL,
  Reviews jsonb NOT NULL
);

CREATE TABLE CLINIC_SETTINGS_VERSION (
  EffectiveAt timestamptz NOT NULL,
  ActorId integer NOT NULL,
  Reason varchar(500) NOT NULL,
  RestoredFromVersion integer,
  ContentJson jsonb NOT NULL
);

CREATE TABLE COMMAND_RECEIPT (
  StorageId uuid PRIMARY KEY,
  RequestKey uuid NOT NULL,
  Fingerprint varchar(64) NOT NULL,
  ResultJson jsonb NOT NULL,
  ReceiptType varchar(34) NOT NULL,
  ActorAccountId integer,
  AppointmentId uuid,
  ActorScope varchar(96),
  SupportRequestId uuid,
  Id uuid,
  SessionId uuid,
  ExpectedVersion integer,
  ActorId integer,
  VisitId uuid,
  OwnerAccountId integer,
  CreatedAt timestamptz,
  AccessId uuid,
  PatientId integer,
  PrescriptionId uuid,
  Command varchar(48),
  ResourceId uuid,
  BeforeJson jsonb,
  OccurredAt timestamptz,
  ResultId uuid,
  DossierId uuid
);

CREATE TABLE CUSTOMER_FAMILY_MEMBER (
  Id uuid PRIMARY KEY,
  OwnerAccountId integer NOT NULL,
  FullName varchar(100) NOT NULL,
  Relationship varchar(20) NOT NULL,
  BirthDate date,
  Gender varchar(16),
  ContactPhone varchar(10),
  DeletedAt timestamptz
);

CREATE TABLE FAMILY_RECORD_ACCESS (
  Id uuid PRIMARY KEY,
  FamilyMemberId uuid NOT NULL,
  OwnerAccountId integer NOT NULL,
  Code varchar(40) NOT NULL,
  Status varchar(16) NOT NULL,
  PatientId integer,
  BranchId integer,
  Scopes text[] NOT NULL,
  RequestedAt timestamptz NOT NULL,
  ExpiresAt timestamptz,
  DecidedBy integer,
  Details jsonb NOT NULL
);

CREATE TABLE IN_APP_NOTIFICATION (
  Id uuid PRIMARY KEY,
  Data jsonb NOT NULL,
  NotificationRecordKind varchar(34) NOT NULL,
  AppointmentId uuid,
  Kind varchar(80),
  Status varchar(16),
  CreatedAt timestamptz,
  PublicId varchar(40),
  AccountId integer,
  BranchId integer,
  Title varchar(160),
  Body varchar(1000),
  ReadAt timestamptz,
  DedupeKey varchar(160),
  NotificationId uuid,
  Channel varchar(16),
  AttemptCount integer,
  NextRetryAt timestamptz,
  Error varchar(1000)
);

CREATE TABLE LAB_ORDER (
  Id uuid PRIMARY KEY,
  ActivityId uuid NOT NULL,
  Code varchar(40),
  DentistId integer NOT NULL,
  LabProviderId uuid,
  LabWorkTypeId uuid,
  WorkName varchar(160) NOT NULL,
  OrderedOn date NOT NULL,
  DueOn date,
  DeliveredOn date,
  Quantity integer NOT NULL,
  TotalFeeVnd bigint NOT NULL,
  PaidVnd bigint NOT NULL,
  Note varchar(1000),
  Status varchar(20) NOT NULL,
  Spec jsonb NOT NULL,
  StatusLogs jsonb NOT NULL
);

CREATE TABLE LAB_WORK_TYPE (
  Id uuid PRIMARY KEY,
  ProviderId uuid,
  Unit varchar(16) NOT NULL,
  ReferencePrice bigint,
  Warranty varchar(1000),
  Code varchar(40) NOT NULL,
  Name varchar(160) NOT NULL,
  IsActive boolean NOT NULL
);

CREATE TABLE MATERIAL_CATALOG_ITEM (
  Id uuid PRIMARY KEY,
  Name varchar(160) NOT NULL,
  CategoryId uuid NOT NULL,
  UsageUnit varchar(40),
  IsDeleted boolean NOT NULL
);

CREATE TABLE MATERIAL_CATEGORY (
  Id uuid PRIMARY KEY,
  Name varchar(160) NOT NULL,
  IsDeleted boolean NOT NULL
);

CREATE TABLE MATERIAL_PURCHASE (
  Id uuid PRIMARY KEY,
  MaterialId uuid NOT NULL,
  SupplierId uuid,
  LotNumber varchar(60),
  EnteredOn date NOT NULL,
  ReceivedOn date,
  ExpiresOn date,
  Quantity numeric NOT NULL,
  Unit varchar(40) NOT NULL,
  ConversionFactor numeric NOT NULL,
  UsageQuantity numeric NOT NULL,
  UnitPriceVnd bigint NOT NULL,
  TotalVnd bigint NOT NULL,
  PaidVnd bigint NOT NULL,
  IsDeleted boolean NOT NULL,
  CreatedAt timestamptz NOT NULL
);

CREATE TABLE MATERIAL_USAGE (
  Id uuid PRIMARY KEY,
  ActivityId uuid NOT NULL,
  MaterialId uuid NOT NULL,
  Quantity numeric NOT NULL,
  UsedAt timestamptz NOT NULL,
  RecordedBy integer
);

CREATE TABLE PATIENT_ACCOUNT_LINK (
  Id integer PRIMARY KEY,
  RequestCode varchar(32) NOT NULL,
  AccountId integer NOT NULL,
  PatientId integer,
  DeclaredFullName varchar(160) NOT NULL,
  Status varchar(16) NOT NULL,
  RequestedAt timestamptz NOT NULL,
  ReviewingEmployeeId integer,
  RevokingEmployeeId integer,
  Details jsonb NOT NULL
);

CREATE TABLE PATIENT_PROFILE (
  Id integer PRIMARY KEY,
  PatientCode varchar(40) NOT NULL,
  FullName varchar(160) NOT NULL,
  NormalizedName varchar(160) NOT NULL,
  BirthDate date,
  Gender varchar(16),
  Phone varchar(24),
  Address varchar(255),
  SourceId uuid,
  CustomerGroupId uuid,
  OccupationId uuid,
  IsArchived boolean NOT NULL,
  CreatedAt timestamptz NOT NULL,
  UpdatedAt timestamptz NOT NULL,
  Details jsonb NOT NULL
);

CREATE TABLE PRESCRIPTION (
  Id uuid PRIMARY KEY,
  PatientId integer NOT NULL,
  BranchId integer NOT NULL,
  PrescriberEmployeeId integer NOT NULL,
  PrescribedOn date NOT NULL,
  Diagnosis varchar(1000) NOT NULL,
  OtherDiagnosis varchar(1000),
  Advice varchar(4000),
  CreatedAt timestamptz NOT NULL,
  Items jsonb NOT NULL
);

CREATE TABLE PUSH_DEVICE (
  Id uuid PRIMARY KEY,
  AccountId integer NOT NULL,
  Token varchar(512) NOT NULL,
  Platform varchar(16) NOT NULL,
  UpdatedAt timestamptz NOT NULL
);

CREATE TABLE REFERENCE_ENTRY (
  Id uuid PRIMARY KEY,
  Kind varchar(32) NOT NULL,
  Code varchar(40),
  Name varchar(160) NOT NULL,
  Description varchar(1000),
  IsReserved boolean NOT NULL,
  IsActive boolean NOT NULL
);

CREATE TABLE SCHEDULING_BRANCH (
  Id integer PRIMARY KEY,
  Code varchar(40),
  Name varchar(160) NOT NULL,
  Address varchar(1000),
  Phone varchar(20),
  Email varchar(254),
  Active boolean NOT NULL,
  SlotIntervalMinutes integer NOT NULL,
  Chairs jsonb NOT NULL,
  Members jsonb NOT NULL
);

CREATE TABLE SCHEDULING_SERVICE (
  Id integer PRIMARY KEY,
  Unit varchar(40) NOT NULL,
  IsInitialExamination boolean NOT NULL,
  PublicBookable boolean NOT NULL,
  BranchId integer NOT NULL,
  Name varchar(160) NOT NULL,
  PublicDurationMinutes integer NOT NULL,
  Active boolean NOT NULL,
  ReferenceUnitPriceVnd bigint,
  PriceVersion integer NOT NULL
);

CREATE TABLE TREATMENT_DOSSIER (
  Id uuid PRIMARY KEY,
  SourceExaminationVisitId uuid,
  Code varchar(40) NOT NULL,
  PatientId integer NOT NULL,
  BranchId integer NOT NULL,
  DentistId integer NOT NULL,
  StartedOn date NOT NULL,
  Diagnosis varchar(4000) NOT NULL,
  Note varchar(4000),
  State varchar(16) NOT NULL,
  CreatedAt timestamptz NOT NULL,
  UpdatedAt timestamptz NOT NULL,
  SheetDrafts jsonb NOT NULL
);

CREATE TABLE TREATMENT_PLAN_ITEM (
  Id uuid PRIMARY KEY,
  DossierId uuid NOT NULL,
  PlannedOrder integer NOT NULL,
  ServiceId integer NOT NULL,
  ServiceName varchar(160) NOT NULL,
  Region varchar(80),
  Unit varchar(40) NOT NULL,
  Quantity numeric NOT NULL,
  EstimatedStart date,
  EstimatedMinutes integer,
  Note varchar(1000),
  ResponsibleDentistId integer,
  AssistantId integer,
  AgreedUnitPriceVnd bigint NOT NULL,
  DiscountType varchar(16),
  DiscountValue numeric,
  DiscountReason varchar(1000),
  DisplayCompleted boolean NOT NULL
);

CREATE TABLE TREATMENT_TEMPLATE (
  Id uuid PRIMARY KEY,
  DeletedAt timestamptz,
  BranchId integer NOT NULL,
  Name varchar(160) NOT NULL,
  CreatedBy integer NOT NULL,
  CreatedAt timestamptz NOT NULL,
  Items jsonb NOT NULL
);

CREATE TABLE USER_ACCOUNT (
  Id integer PRIMARY KEY,
  EmployeeId integer NOT NULL,
  FullName varchar(160) NOT NULL,
  AccountType varchar(16) NOT NULL,
  IsActive boolean NOT NULL,
  CreatedAt timestamptz NOT NULL,
  UpdatedAt timestamptz NOT NULL,
  AssignedRoles text[] NOT NULL,
  AuthData jsonb NOT NULL,
  EmployeeCode varchar(40),
  GroupId uuid,
  BranchIds integer[],
  UserName varchar(256),
  Email varchar(256),
  EmailConfirmed boolean NOT NULL,
  PasswordHash text,
  PhoneNumber text
);

CREATE TABLE VISIT (
  Id uuid PRIMARY KEY,
  AppointmentId uuid NOT NULL,
  PatientId integer NOT NULL,
  BranchId integer NOT NULL,
  DentistId integer NOT NULL,
  Status varchar(24) NOT NULL,
  ArrivalAt timestamptz NOT NULL,
  RecordedBy integer NOT NULL,
  ArrivalReason varchar(500),
  Purpose varchar(40) NOT NULL,
  Note varchar(1000),
  ExaminationContentJson jsonb,
  UpdatedBy integer,
  UpdatedAt timestamptz,
  Investigations jsonb NOT NULL
);

CREATE TABLE WORKING_SCHEDULE (
  Id uuid PRIMARY KEY,
  EmployeeId integer NOT NULL,
  PublishedBy integer NOT NULL,
  PublishedAt timestamptz NOT NULL,
  ScheduleKind varchar(34) NOT NULL,
  Date date,
  Kind varchar(16),
  Reason varchar(500),
  BranchId integer,
  WeekStart date,
  Periods jsonb NOT NULL
);
