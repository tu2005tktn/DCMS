# 3. Test Reports

## 3.2 Test Levels' Defects

| Metric | Review | UT | IT | ST | AT | Total | Note |
|---|---|---|---|---|---|---|---|
| Total Defects | - | 8 | - | 0 | - | 8 | UT Round 1 (2026-11-24): 1,618 xUnit cases executed (1,610 Passed, 8 Failed); 986 curated report cases (978 Passed, 8 Failed); Method 78.2%, Line 78.2%, Branch 71.4%. ST Round 1: 67 cases, all Passed. IT, AT not reported yet. |
| Open Defects (Critical) | - | 0 | - | 0 | - | 0 | |
| Open Defects (Major) | - | 8 | - | 0 | - | 8 | 8 failing UT cases on 2026-11-24 run (`ItemPlanRowCreateTests`, `TreatmentLabTests` x2, `TreatmentDossierServiceTests`, `ServicePriceServiceTests`, `PrescriptionServiceTests` x2, `CareTaskServiceTests`). |
