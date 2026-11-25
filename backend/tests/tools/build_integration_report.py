"""Fill the Report-5.2 integration test workbook from a real test run.

Inputs come from backend/tests/run-integration-coverage.ps1 (docs/testing/INTEGRATION-TEST-COVERAGE.vi.md):
  <results>/**/*.trx                  outcome, start/end time and failure message of every test
  <results>/http-exchanges.jsonl      every HTTP request the API served (secrets masked)
  <results>/coverage-report/Summary.json   line/branch coverage from coverlet + ReportGenerator
Test descriptions, pre-conditions and steps come from the [Case(...)] attribute in the test source when present;
other tests are described from their method name and their real HTTP exchanges.

Usage:
  python backend/tests/tools/build_integration_report.py --template <Report-5.2.xlsx> --results <dir> --out <file.xlsx>
"""
from __future__ import annotations

import argparse
import copy
import datetime as dt
import glob
import json
import os
import re
import urllib.parse
import xml.etree.ElementTree as ET
from collections import defaultdict
from dataclasses import dataclass, field

import openpyxl
from openpyxl.worksheet.datavalidation import DataValidation

TESTS_DIR = os.path.join(os.path.dirname(__file__), "..", "DCMS.IntegrationTests")
TRX_NS = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
LOCAL_TZ = dt.timezone(dt.timedelta(hours=7))

# Module sheets, in report order: (sheet name, module code, title, requirement under test, test classes)
MODULES = [
    ("AUTH", "Authentication & accounts", "Sign-in, customer registration, Google sign-in, login identifiers, password recovery, staff account management.",
     ["CustomerRegisterTests", "RegistrationApiTests", "LoginIdentifierApiTests", "GoogleApiTests", "RecoveryApiTests", "StaffAccountTests", "AccountStorageTests"]),
    ("PATIENT", "Patients, family & record links", "Patient records, family members, family record access, linking customer accounts to patient records.",
     ["PatientTests", "FamilyMemberTests", "FamilyMemberApiTests", "FamilyRecordApiTests", "PatientLinkApiTests"]),
    ("SCHEDULE", "Working schedules & booking settings", "Weekly recurring working schedules, schedule publishing, free time slots, booking settings per branch.",
     ["SchedulingApiTests", "ScheduleRulesTests", "RecurringSchedulingTests", "SchedulingStorageTests"]),
    ("APPOINTMENT", "Appointments & reception", "Booking, rescheduling/cancelling, appointment lookup, support requests, reception/check-in, patient notifications.",
     ["AppointmentTests", "AppointmentApiTests", "AppointmentChangeApiTests", "AppointmentQueryApiTests", "AppointmentModelTests",
      "SupportNotificationApiTests", "ReceptionApiTests", "PatientNotificationTests"]),
    ("EXAMINATION", "Examination & clinical records", "Examination sheets, diagnoses, investigation orders, clinical documents/images, AI analysis of X-ray films.",
     ["ExaminationApiTests", "ExaminationStorageTests", "ClinicalAiAnalysisTests", "DentalAiLiveContractTests"]),
    ("TREATMENT", "Treatment", "Treatment dossiers, treatment sheets, item plans, booking from a plan, clinical work, lab orders, aftercare.",
     ["TreatmentDossierTests", "TreatmentDossierApiTests", "TreatmentSheetV25Tests", "TreatmentStorageTests", "TreatmentPlanBookingApiTests",
      "ItemPlanTests", "ItemPlanApiTests", "ClinicalWorkApiTests", "CareLabStorageTests", "TreatmentCatalogSeedTests"]),
    ("FINANCE", "Payments", "Treatment payments, PayOS bank transfers, payment data migration.",
     ["TreatmentPayOsApiTests", "TreatmentPaymentMigrationTests"]),
    ("CATALOG", "Catalogs & material stock", "Lab work types, material groups, materials, purchases, stock by lot, material usage; medicines, providers, reference catalogs.",
     ["CatalogTests", "MaterialPurchaseApiTests", "MaterialStockApiTests", "MaterialUsageBranchTests", "SystemSettingsCatalogApiTests"]),
    ("SETTINGS", "System settings & permissions", "General settings, patient code sequence, per-screen permissions, branch data isolation, non-functional requirements.",
     ["GeneralSettingsApiTests", "AllScreensPermissionApiTests", "BranchIsolationTests", "MainSettingsCompatibilityTests",
      "StorageSimplificationTests", "SystemNfrReportTests"]),
]

CLASS_TITLES = {
    "CustomerRegisterTests": "Customer registration – POST /api/auth/register",
    "RegistrationApiTests": "Password policy & registration",
    "LoginIdentifierApiTests": "Sign-in by email / phone number / patient code",
    "GoogleApiTests": "Google sign-in",
    "RecoveryApiTests": "Password recovery",
    "StaffAccountTests": "Create staff account – POST /api/accounts",
    "AccountStorageTests": "Account, session and role storage",
    "PatientTests": "Create patient record – POST /api/patients",
    "FamilyMemberTests": "Add family member – POST /api/me/family-members",
    "FamilyMemberApiTests": "Family member management",
    "FamilyRecordApiTests": "Family records & access approval",
    "PatientLinkApiTests": "Account – patient record link (S07/S08)",
    "SchedulingApiTests": "Booking settings, schedule publishing, free slots",
    "ScheduleRulesTests": "Working schedule rules",
    "RecurringSchedulingTests": "Recurring working schedules",
    "SchedulingStorageTests": "Working schedule storage",
    "AppointmentTests": "Booking – form validation",
    "AppointmentApiTests": "Booking an appointment",
    "AppointmentChangeApiTests": "Reschedule / cancel an appointment",
    "AppointmentQueryApiTests": "Appointment lookup & calendar",
    "AppointmentModelTests": "Appointment data model",
    "SupportNotificationApiTests": "Support requests & notifications",
    "ReceptionApiTests": "Reception / check-in",
    "PatientNotificationTests": "Patient notifications",
    "ExaminationApiTests": "Examination sheets & diagnoses",
    "ExaminationStorageTests": "Examination storage",
    "ClinicalAiAnalysisTests": "AI analysis of X-ray films",
    "DentalAiLiveContractTests": "AI service API contract (runs against the real service)",
    "TreatmentDossierTests": "Quick-open a treatment dossier",
    "TreatmentDossierApiTests": "Treatment dossiers",
    "TreatmentSheetV25Tests": "Treatment sheets",
    "TreatmentStorageTests": "Treatment data storage",
    "TreatmentPlanBookingApiTests": "Booking from a treatment plan",
    "ItemPlanTests": "Reusable item plans – create",
    "ItemPlanApiTests": "Reusable item plans",
    "ClinicalWorkApiTests": "Clinical work",
    "CareLabStorageTests": "Aftercare & lab orders",
    "TreatmentCatalogSeedTests": "Treatment catalog seed data",
    "TreatmentPayOsApiTests": "PayOS bank transfer payments",
    "TreatmentPaymentMigrationTests": "Payment data migration",
    "CatalogTests": "Lab work types, material groups, materials",
    "MaterialPurchaseApiTests": "Material purchases",
    "MaterialStockApiTests": "Stock by lot",
    "MaterialUsageBranchTests": "Material usage by branch",
    "SystemSettingsCatalogApiTests": "Medicines, providers, reference catalogs",
    "GeneralSettingsApiTests": "General settings & patient code sequence",
    "AllScreensPermissionApiTests": "Per-screen permissions",
    "BranchIsolationTests": "Branch data isolation",
    "MainSettingsCompatibilityTests": "Legacy settings compatibility",
    "StorageSimplificationTests": "Prescriptions & consolidated storage",
    "SystemNfrReportTests": "Non-functional requirements (performance, limits)",
}

DEFAULT_PRE = "The DCMS API runs inside the test process on its own PostgreSQL schema (migrated, demo accounts seeded)."


# ---------- source: [Case(...)] attributes ----------

@dataclass
class CaseInfo:
    key: str
    description: str
    pre: str
    steps: list[str] = field(default_factory=list)


def _tokens(text: str):
    """C# argument tokens: string literals (regular/verbatim), identifiers, '+', ',' and parentheses."""
    i, n = 0, len(text)
    while i < n:
        ch = text[i]
        if ch.isspace():
            i += 1
        elif ch == '@' and i + 1 < n and text[i + 1] == '"':
            j, buf = i + 2, []
            while j < n:
                if text[j] == '"' and j + 1 < n and text[j + 1] == '"':
                    buf.append('"'); j += 2
                elif text[j] == '"':
                    break
                else:
                    buf.append(text[j]); j += 1
            yield ("str", "".join(buf)); i = j + 1
        elif ch == '"':
            j, buf = i + 1, []
            while j < n and text[j] != '"':
                if text[j] == '\\' and j + 1 < n:
                    buf.append({'n': '\n', 't': '\t', '"': '"', '\\': '\\'}.get(text[j + 1], text[j + 1])); j += 2
                else:
                    buf.append(text[j]); j += 1
            yield ("str", "".join(buf)); i = j + 1
        elif ch.isalpha() or ch == '_':
            j = i
            while j < n and (text[j].isalnum() or text[j] in "_."):
                j += 1
            yield ("id", text[i:j]); i = j
        else:
            yield ("op", ch); i += 1


def _case_args(attr_text: str, consts: dict[str, str]) -> list[str]:
    start = attr_text.index("Case(") + 5
    depth, args, current = 1, [], []
    for kind, value in _tokens(attr_text[start:]):
        if kind == "op" and value == "(":
            depth += 1
        elif kind == "op" and value == ")":
            depth -= 1
            if depth == 0:
                break
        elif kind == "op" and value == "," and depth == 1:
            args.append("".join(current)); current = []
            continue
        if kind == "str":
            current.append(value)
        elif kind == "id":
            current.append(consts.get(value.split(".")[-1], value))
    if current:
        args.append("".join(current))
    return args


def read_case_attributes() -> dict[str, CaseInfo]:
    """Maps 'Class.Method' to the [Case] metadata written in the test source."""
    result: dict[str, CaseInfo] = {}
    method_re = re.compile(r"public\s+(?:async\s+)?(?:Task|void)\s+(\w+)\s*\(")
    for path in glob.glob(os.path.join(TESTS_DIR, "*.cs")):
        source = open(path, encoding="utf-8-sig").read()
        consts = {m.group(1): "".join(v for k, v in _tokens(m.group(2)) if k == "str")
                  for m in re.finditer(r'const\s+string\s+(\w+)\s*=\s*(.+?);', source)}
        classes = [(m.start(), m.group(1)) for m in re.finditer(r"\bclass\s+(\w+)", source)]
        for m in method_re.finditer(source):
            owner = [name for pos, name in classes if pos < m.start()]
            if not owner:
                continue
            before = source[max(0, m.start() - 3000):m.start()]
            # attributes directly above the method: from its [Fact]/[Theory] (step text may contain "{id}", so the
            # previous member's closing brace cannot mark the start); no other member may sit in between
            cut = max(before.rfind("[Fact"), before.rfind("[Theory"))
            attrs = before[cut:] if cut >= 0 else ""
            if "public " in attrs:
                continue
            if "Case(" not in attrs:
                continue
            args = _case_args(attrs[attrs.index("Case("):], consts)
            if len(args) >= 3:
                result[f"{owner[-1]}.{m.group(1)}"] = CaseInfo(args[0], args[1], args[2], args[3:])
    return result


# ---------- run results ----------

@dataclass
class TestRun:
    full_name: str
    cls: str
    method: str
    data: str
    outcome: str
    start: dt.datetime
    end: dt.datetime
    message: str
    exchanges: list[dict] = field(default_factory=list)


def _time(value: str) -> dt.datetime:
    value = re.sub(r"(\.\d{6})\d+", r"\1", value)
    return dt.datetime.fromisoformat(value)


def read_trx(results: str) -> list[TestRun]:
    files = sorted(glob.glob(os.path.join(results, "**", "*.trx"), recursive=True), key=os.path.getmtime)
    if not files:
        raise SystemExit(f"No .trx under {results}")
    root = ET.parse(files[-1]).getroot()
    runs = []
    for u in root.iterfind(".//t:UnitTestResult", TRX_NS):
        name = u.get("testName")
        base, _, data = name.partition("(")
        parts = base.split(".")
        message_node = u.find(".//t:Message", TRX_NS)
        outcome = {"Passed": "Passed", "Failed": "Failed", "NotExecuted": "N/A"}.get(u.get("outcome"), "Pending")
        runs.append(TestRun(name, parts[-2], parts[-1], ("(" + data) if data else "", outcome,
                            _time(u.get("startTime")), _time(u.get("endTime")),
                            (message_node.text or "").strip() if message_node is not None else ""))
    runs.sort(key=lambda r: r.start)
    return runs


def attach_exchanges(runs: list[TestRun], results: str) -> None:
    path = os.path.join(results, "http-exchanges.jsonl")
    if not os.path.exists(path):
        return
    exchanges = []
    for line in open(path, encoding="utf-8"):
        if line.strip():
            item = json.loads(line)
            item["at"] = _time(item["at"])
            exchanges.append(item)
    exchanges.sort(key=lambda x: x["at"])
    user_names(exchanges)
    index = 0
    for run in runs:
        while index < len(exchanges) and exchanges[index]["at"] < run.start:
            index += 1
        j = index
        while j < len(exchanges) and exchanges[j]["at"] <= run.end:
            run.exchanges.append(exchanges[j]); j += 1


# ---------- text for the report ----------
# The report is in English. Steps and expected results come from the real HTTP exchanges of each test, so every
# step shows who called, which endpoint and every field it sent, and every expected result shows the status,
# the message code and the fields that came back (docs/testing/INTEGRATION-TEST-COVERAGE.vi.md, section 5a).

STATUS = {200: "OK", 201: "Created", 202: "Accepted", 204: "No Content", 302: "Found", 400: "Bad Request",
          401: "Unauthorized", 403: "Forbidden", 404: "Not Found", 405: "Method Not Allowed", 409: "Conflict",
          413: "Payload Too Large", 415: "Unsupported Media Type", 422: "Unprocessable Entity", 429: "Too Many Requests",
          500: "Internal Server Error", 502: "Bad Gateway", 503: "Service Unavailable"}

# English meaning of the system messages (backend/src/DCMS.Application/Common/SystemMessages.cs and the services
# that raise MSG61-MSG92). {field} is filled from the "field" of the response when the message names a field.
MSG_EN = {
    "MSG01": "No matching result was found.",
    "MSG02": "Please enter {field}.",
    "MSG03": "{field} must not exceed the maximum length.",
    "MSG04": "{field} has an invalid format.",
    "MSG05": "You do not have permission to perform this action.",
    "MSG06": "The session has expired. Please sign in again.",
    "MSG07": "The sign-in details are wrong or the account cannot be used.",
    "MSG08": "Account created successfully.",
    "MSG09": "If the account details are valid, password reset instructions are sent to the registered contact.",
    "MSG10": "Password reset successfully. Please sign in with the new password.",
    "MSG11": "Account updated successfully.",
    "MSG12": "Account locked; access from current sessions is revoked.",
    "MSG13": "Account unlocked successfully.",
    "MSG14": "A record link can be requested only after the first visit at the clinic.",
    "MSG15": "Link request sent; the clinic will verify it before granting access.",
    "MSG16": "The account is now linked to the patient record.",
    "MSG17": "The link request is not approved or the link was revoked.",
    "MSG18": "Please enter the details of the person who actually attends.",
    "MSG19": "A public booking must start at least 60 minutes from now.",
    "MSG20": "The selected time slot is no longer available.",
    "MSG21": "There is no continuous free time long enough for the appointment.",
    "MSG22": "The time overlaps an existing appointment.",
    "MSG23": "The time slot is fully booked.",
    "MSG24": "Booking successful; keep the appointment code for lookup.",
    "MSG25": "This booking request was already processed; the existing appointment is returned.",
    "MSG26": "This appointment cannot be changed or cancelled online; contact the clinic.",
    "MSG27": "Contact the clinic with the appointment code and verify the contact used for booking.",
    "MSG28": "Support request sent; the current appointment stays valid until the clinic handles it.",
    "MSG29": "The appointment cannot be moved to the selected slot; the old appointment is kept.",
    "MSG30": "The appointment was moved to the new time.",
    "MSG31": "The appointment was cancelled.",
    "MSG32": "The change affects confirmed appointments; handle them first.",
    "MSG33": "Working schedule updated.",
    "MSG34": "The appointment is not eligible for check-in today.",
    "MSG35": "The visitor does not match the patient record or the selected appointment.",
    "MSG36": "The appointment or resource is no longer valid for check-in.",
    "MSG37": "You are not allowed to view this information.",
    "MSG38": "The verification code or link is invalid or expired.",
    "MSG39": "The password does not meet the displayed requirements.",
    "MSG40": "Working hours or breaks are invalid (start/end time or overlapping periods).",
    "MSG41": "The data was changed by another operation (stale version); reload and check again.",
    "MSG42": "The requested data was not found.",
    "MSG43": "No matching patient record was found.",
    "MSG44": "Patient administrative details updated.",
    "MSG45": "A reason is required to reopen the treatment dossier.",
    "MSG46": "The account details cannot be used yet.",
    "MSG47": "The account or record already has an active self link.",
    "MSG48": "Not enough evidence to verify the account against the patient record.",
    "MSG49": "The operation could not be completed; check the connection and try again.",
    "MSG50": "The value of {field} already exists; choose another one.",
    "MSG51": "The record is inactive; restore it or choose a valid one.",
    "MSG52": "The catalog item is in use; it can only be deactivated for new transactions.",
    "MSG53": "{field} recorded.",
    "MSG54": "The conditions for this treatment step are not met.",
    "MSG55": "The plan change affects the next session or booked appointments.",
    "MSG56": "Information needs the dentist's review: {field}.",
    "MSG57": "The consent does not match the current treatment option.",
    "MSG58": "Some plan items are unfinished; record their result and reason.",
    "MSG59": "Record the agreed price or discount, the reason and the item it applies to.",
    "MSG60": "The service price must not be negative.",
    "MSG61": "Google sign-in is not configured.",
    "MSG62": "The Google account could not be verified.",
    "MSG63": "This email already has an account; sign in with the registered method.",
    "MSG64": "The Google account could not be created.",
    "MSG65": "Confirm that the patient consented before booking.",
    "MSG66": "The slot of the no-show appointment is no longer valid.",
    "MSG67": "The family member was not found or is no longer available.",
    "MSG68": "{field} is invalid.",
    "MSG69": "The request key was already used for different content.",
    "MSG70": "Please check {field}.",
    "MSG71": "The request or family member was not found.",
    "MSG72": "You may not view this record, or the access has expired.",
    "MSG73": "This visit is for treatment; use the dossier sessions instead of a new examination.",
    "MSG74": "An image/document that is an investigation result cannot be deleted.",
    "MSG75": "The plan data is invalid.",
    "MSG76": "The request key was already used for different content.",
    "MSG77": "A plan item is no longer valid; fix the source plan.",
    "MSG78": "The list of materials used is invalid.",
    "MSG79": "Not enough stock.",
    "MSG80": "The change would make material stock negative because it was already used.",
    "MSG81": "The date is invalid (expiry before import date, or receipt date before import date or in the future).",
    "MSG82": "The paid amount must not exceed the total.",
    "MSG83": "The material was deleted; choose another one.",
    "MSG84": "The supplier is no longer active; choose another one.",
    "MSG85": "The name already exists.",
    "MSG86": "The category still has materials; move or delete them first.",
    "MSG87": "This item is used by linked documents and cannot be deactivated.",
    "MSG88": "There are no setting changes to save.",
    "MSG89": "The patient record has no such field, so it cannot be made required.",
    "MSG93": "The payment details are invalid.",
    "MSG94": "The amount is invalid.",
    "MSG95": "The amount exceeds the limit.",
    "MSG96": "The deposit amount is invalid.",
    "MSG97": "The deposit allocation is invalid.",
    "MSG98": "A no-show reason is required.",
    "MSG99": "A cancellation reason is required.",
    "MSG100": "The request key is invalid.",
    "MSG101": "The date range or branch is invalid.",
    "MSG102": "The service or the amount due changed; reload.",
    "MSG103": "The service or the amount to collect changed.",
    "MSG104": "30% of the lab fee must be paid before other services.",
    "MSG105": "30% of the lab fee must be covered before allocating the deposit to other services.",
    "MSG106": "The deposit balance is not enough; reload.",
    "MSG107": "The sheet changed; reload.",
    "MSG108": "The sheet has treatment or lab work and cannot be removed as a no-show.",
    "MSG109": "The sheet has no deposit balance to forfeit.",
    "MSG110": "The sheet already used the deposit or has service transactions.",
    "MSG111": "The sheet still has an unreconciled transfer QR.",
    "MSG112": "An unexpired QR code exists; reopen the current QR.",
    "MSG113": "The request key was already used.",
    "MSG114": "The request key was already used for another payment.",
    "MSG115": "The request key was already used for another allocation.",
    "MSG116": "The request key was already used for another operation.",
}

GUID_RE = re.compile(r"[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}", re.I)
SECRET_KEYS = {"password", "confirmpassword", "newpassword", "currentpassword", "accesstoken", "refreshtoken",
               "token", "proof", "idtoken", "resetgrant", "otp"}
MAX_FIELDS = 30


def status_text(code: int) -> str:
    return f"{code} {STATUS.get(code, '')}".strip()


def message_text(code: str, field_name: str | None) -> str:
    text = MSG_EN.get(code, "")
    if "{field}" in text:
        text = text.replace("{field}", field_name or "the field")
    return f'{code} "{text}"' if text else code


def humanize(method: str, data: str) -> str:
    words = method.replace("_", " ")
    words = re.sub(r"(?<=[a-z0-9])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])", " ", words)
    words = re.sub(r"\s+", " ", words).strip()
    sentence = words[:1].upper() + words[1:].lower()
    sentence = re.sub(r"\b(api|id|ai|payos|otp|jwt|xlsx|v25|s58|uc|dto|qr|url|nfr)\b", lambda m: m.group(1).upper(), sentence)
    return sentence + (f" {data}" if data else "")


def _path(path: str) -> str:
    return GUID_RE.sub("{id}", path)


def _parse(text: str | None):
    """JSON body, or the top-level scalar fields of a body the log cut short."""
    if not text:
        return None
    try:
        return json.loads(text.rstrip("…"))
    except ValueError:
        pass
    if not text.lstrip().startswith("{"):
        return text
    fields, depth, i, key = {}, 0, 0, None
    token = re.compile(r'"((?:[^"\\]|\\.)*)"|(-?\d+(?:\.\d+)?(?:[eE][+-]?\d+)?)|(true|false|null)|([{}\[\]:,])')
    expecting_value = False
    for m in token.finditer(text):
        s, num, lit, punct = m.groups()
        if punct:
            if punct in "{[":
                if depth == 1 and expecting_value and key:
                    fields[key] = "{…}" if punct == "{" else "[…]"
                depth += 1; expecting_value = False
            elif punct in "}]":
                depth -= 1
            elif punct == ":":
                expecting_value = True
            continue
        if depth != 1:
            continue
        try:
            value = json.loads(m.group(0))
        except ValueError:
            value = m.group(0)
        if expecting_value and key is not None:
            fields[key] = value; expecting_value = False; key = None
        elif s is not None:
            key = value
    fields["…"] = "(response truncated in the log)"
    return fields


def _value(key: str, value) -> str:
    if key.split(".")[-1].split("[")[0].lower() in SECRET_KEYS:
        return "(test password, masked)" if "password" in key.lower() else "*** (masked)"
    if isinstance(value, str):
        if value == "***":
            return "*** (masked)"
        if GUID_RE.fullmatch(value):
            return "(new GUID)" if "requestkey" in key.lower() or "key" in key.lower().split(".")[-1] else "(GUID)"
        if len(value) > 60:
            return f'"{value[:40]}…" ({len(value)} characters)'
        return json.dumps(value, ensure_ascii=False)
    if value is None:
        return "null"
    if isinstance(value, bool):
        return "true" if value else "false"
    return str(value)


def _flatten(value, prefix: str = "", out: list | None = None) -> list[str]:
    out = [] if out is None else out
    if isinstance(value, dict):
        if not value and prefix:
            out.append(f"{prefix} = {{}}")
        for k, v in value.items():
            _flatten(v, f"{prefix}.{k}" if prefix else k, out)
    elif isinstance(value, list):
        if not value:
            out.append(f"{prefix} = [] (empty)")
        elif all(not isinstance(x, (dict, list)) for x in value):
            shown = ", ".join(_value(prefix, x) for x in value[:12])
            out.append(f"{prefix} = [{shown}{', …' if len(value) > 12 else ''}]" + (f" ({len(value)} items)" if len(value) > 3 else ""))
        else:
            for i, item in enumerate(value[:5]):
                _flatten(item, f"{prefix}[{i}]", out)
            if len(value) > 5:
                out.append(f"{prefix}: … {len(value) - 5} more items ({len(value)} in total)")
    else:
        out.append(f"{prefix or 'body'} = {_value(prefix, value)}")
    return out


def request_lines(ex: dict) -> list[str]:
    lines = []
    path, _, query = ex["path"].partition("?")
    if query:
        for part in query.split("&"):
            k, _, v = part.partition("=")
            lines.append(f"query {k} = {_value(k, urllib.parse.unquote_plus(v))}")
    body = _parse(ex.get("request"))
    if isinstance(body, (dict, list)):
        fields = _flatten(body)
        lines += fields[:MAX_FIELDS] + ([f"… {len(fields) - MAX_FIELDS} more fields"] if len(fields) > MAX_FIELDS else [])
    elif isinstance(body, str):
        lines.append(f"body = {_value('body', body)}")
    elif ex["method"] in ("POST", "PUT", "PATCH") and not ex.get("request"):
        lines.append("no JSON body (empty or multipart/form data)")
    return lines


def response_summary(ex: dict) -> str:
    status = ex["status"]
    head = status_text(status)
    body = _parse(ex.get("response"))
    if isinstance(body, dict) and isinstance(body.get("code"), str) and body["code"].startswith("MSG"):
        parts = [message_text(body["code"], body.get("field"))]
        if body.get("field"):
            parts.append(f'field = "{body["field"]}"')
        if body.get("currentVersion") is not None:
            parts.append(f"currentVersion = {body['currentVersion']}")
        if isinstance(body.get("blockers"), list):
            parts.append(f"{len(body['blockers'])} blocking item(s) listed")
        return f"{head}; " + "; ".join(parts)
    if status == 204 or body is None:
        return head + ("; empty body" if status in (200, 204) and ex.get("response") in (None, "") else "")
    if isinstance(body, list):
        text = f"{head}; list of {len(body)} item(s)"
        if body and isinstance(body[0], dict):
            first = [f for f in _flatten({k: v for k, v in body[0].items() if not isinstance(v, (dict, list))})][:6]
            if first:
                text += "; first item: " + ", ".join(first)
        return text
    if isinstance(body, dict):
        if isinstance(body.get("errors"), dict):  # ASP.NET validation problem details
            names = ", ".join(list(body["errors"])[:6])
            return f"{head}; validation errors on: {names}"
        truncated = body.pop("…", None) is not None
        placeholder = ("{…}", "[…]")
        scalars = {k: v for k, v in body.items() if not isinstance(v, (dict, list)) and v not in placeholder}
        if len(scalars) < 4:  # wrapper objects such as { dossier: {...}, notification: {...} }: show their fields
            for k, v in body.items():
                if isinstance(v, dict):
                    scalars.update({f"{k}.{kk}": vv for kk, vv in v.items() if not isinstance(vv, (dict, list))})
            body = {k: v for k, v in body.items() if not isinstance(v, dict)}
        nested = [f"{k}: {len(v)} item(s)" if isinstance(v, list) else f"{k}: {{…}}" if isinstance(v, dict) else f"{k}: {v}"
                  for k, v in body.items() if isinstance(v, (dict, list)) or v in placeholder]
        if truncated:
            nested.append("(rest of the response not shown)")
        fields = _flatten(scalars)[:12] + nested[:4]
        return f"{head}; " + ", ".join(fields) if fields else head
    return f"{head}; body = {_value('body', body)}"


def user_names(exchanges: list[dict]) -> None:
    """Sets ex['caller'] to the user name of the account that made each request (from the latest sign-in before it)."""
    names: dict[str, str] = {}
    for ex in exchanges:
        uid = ex.get("user")
        ex["caller"] = names.get(str(uid), f"account #{uid}") if uid else None
        body = _parse(ex.get("response"))
        if isinstance(body, dict):
            user = body.get("user") if isinstance(body.get("user"), dict) else body if "userName" in body else None
            if isinstance(user, dict) and user.get("id") is not None and user.get("userName"):
                names[str(user["id"])] = user["userName"]


def _users(run: TestRun) -> list[str]:
    users = []
    for ex in run.exchanges:
        name = ex.get("caller")
        if ex["path"].startswith("/api/auth/sign-in") and ex.get("request") and ex["status"] == 200:
            m = re.search(r'"userName"\s*:\s*"([^"]+)"', ex["request"], re.I)
            name = m.group(1).strip() if m else name
        if name and name.strip() and name not in users and not name.startswith("account #"):
            users.append(name)
    return users


def _steps(run: TestRun, limit: int = 40):
    """Groups identical consecutive requests; returns (procedure lines, expected lines)."""
    groups: list[list[dict]] = []
    for ex in run.exchanges:
        key = (ex["method"], ex["path"], ex.get("request"), ex["status"], ex.get("caller"))
        if groups and (groups[-1][0]["method"], groups[-1][0]["path"], groups[-1][0].get("request"),
                       groups[-1][0]["status"], groups[-1][0].get("caller")) == key:
            groups[-1].append(ex)
        else:
            groups.append([ex])
    procedure, expected = [], []
    for n, group in enumerate(groups[:limit], 1):
        ex = group[0]
        repeat = f" (sent {len(group)} times)" if len(group) > 1 else ""
        if ex["path"].startswith("/api/auth/sign-in") and ex["method"] == "POST":
            m = re.search(r'"userName"\s*:\s*"([^"]*)"', ex.get("request") or "")
            who = m.group(1) if m else "?"
            procedure.append(f'Step {n}: Sign in as "{who}" – POST /api/auth/sign-in{repeat}\n'
                             f'   - userName = "{who}"\n   - password = (test password, masked)')
            if ex["status"] == 200:
                expected.append(f'Step {n} → 200 OK; access token and refresh token issued (masked); user.userName = "{who}"')
                continue
        else:
            actor = f"as {ex['caller']}" if ex.get("caller") else "anonymous (no access token)"
            text = f"Step {n}: {ex['method']} {_path(ex['path'].split('?')[0])} – {actor}{repeat}"
            details = request_lines(ex)
            if details:
                text += "\n" + "\n".join("   - " + d for d in details)
            procedure.append(text)
        expected.append(f"Step {n} → {response_summary(ex)}")
    if len(groups) > limit:
        rest = groups[limit:]
        procedure.append(f"… {len(rest)} more requests: " + "; ".join(sorted({f"{g[0]['method']} {_path(g[0]['path'].split('?')[0])}" for g in rest}))[:1500])
        codes = sorted({status_text(g[0]["status"]) for g in rest})
        expected.append(f"Remaining {len(rest)} requests → " + ", ".join(codes))
    return procedure, expected


# ---------- assertions from the test source ----------

_SOURCES: dict[str, str] = {}


def _plain_source(text: str) -> str:
    """C# source with comments removed, raw strings shortened and char literals neutralised, so brace and parenthesis
    matching cannot be thrown off by an apostrophe in a comment or a brace inside a literal."""
    out, i, n = [], 0, len(text)
    while i < n:
        if text.startswith("//", i):
            j = text.find("\n", i)
            i = n if j < 0 else j
            continue
        if text.startswith("/*", i):
            j = text.find("*/", i + 2)
            i = n if j < 0 else j + 2
            continue
        if text.startswith('"""', i):
            j = text.find('"""', i + 3)
            out.append('"(raw text)"')
            i = n if j < 0 else j + 3
            continue
        c = text[i]
        if c == '"':
            verbatim = text[max(0, i - 2):i] in ("$@", "@$") or text[i - 1:i] == "@"
            j = i + 1
            while j < n:
                if verbatim and text[j] == '"':
                    if text[j + 1:j + 2] == '"':
                        j += 2; continue
                    break
                if not verbatim and text[j] == "\\":
                    j += 2; continue
                if not verbatim and text[j] == '"':
                    break
                j += 1
            out.append(text[i:j + 1])
            i = j + 1
            continue
        if c == "'":
            m = re.match(r"'(?:\\.|[^\\'])'", text[i:i + 4])
            if m:
                literal = m.group(0)
                out.append("'_'" if any(x in literal[1:-1] for x in "\"'(){}[]\\") else literal)
                i += len(m.group(0))
                continue
        out.append(c)
        i += 1
    return "".join(out)


def _source_of(cls: str) -> str | None:
    if not _SOURCES:
        for path in glob.glob(os.path.join(TESTS_DIR, "*.cs")):
            text = _plain_source(open(path, encoding="utf-8-sig").read())
            for m in re.finditer(r"\bclass\s+(\w+)", text):
                _SOURCES.setdefault(m.group(1), text)
    return _SOURCES.get(cls)


def _balanced(text: str, start: int, open_c: str = "(", close_c: str = ")") -> int:
    depth, i, in_str = 0, start, None
    while i < len(text):
        c = text[i]
        if in_str:
            if c == "\\" and in_str == '"':
                i += 2; continue
            if c == in_str:
                in_str = None
        elif c in "\"'":
            in_str = c
        elif c == open_c:
            depth += 1
        elif c == close_c:
            depth -= 1
            if depth == 0:
                return i
        i += 1
    return len(text) - 1


def _method_body(cls: str, method: str) -> str:
    text = _source_of(cls)
    if not text:
        return ""
    cls_at = re.search(rf"\bclass\s+{cls}\b", text)
    m = re.search(rf"\b(?:Task|void)\s+{method}\s*\(", text[cls_at.start() if cls_at else 0:])
    if not m:
        return ""
    at = (cls_at.start() if cls_at else 0) + m.end() - 1
    after = _balanced(text, at) + 1
    rest = text[after:after + 4]
    if rest.lstrip().startswith("=>"):
        return text[after:text.index(";", after) + 1]
    brace = text.index("{", after)
    return text[brace:_balanced(text, brace, "{", "}") + 1]


def _split_args(args: str) -> list[str]:
    parts, depth, cur, in_str = [], 0, "", None
    for c in args:
        if in_str:
            cur += c
            if c == in_str and not cur.endswith("\\" + c):
                in_str = None
            continue
        if c in "\"'":
            in_str = c
        elif c in "([{":
            depth += 1
        elif c in ")]}":
            depth -= 1
        elif c == "," and depth == 0:
            parts.append(cur.strip()); cur = ""; continue
        cur += c
    if cur.strip():
        parts.append(cur.strip())
    return parts


def _expr(text: str) -> str:
    text = re.sub(r"\s+", " ", text)
    text = re.sub(r"\(await ([^()]*(?:\([^()]*\))*[^()]*)\)!?", r"\1", text)
    text = text.replace("await ", "").replace(".Content.ReadFromJsonAsync", ".ReadJson").replace("!.", ".").replace(")!", ")")
    text = re.sub(r"HttpStatusCode\.(\w+)", lambda m: m.group(1), text)
    text = re.sub(r"\b\w+\.QueryAsync\(db => db\.(.*)\)$", r"database: \1", text)
    text = re.sub(r"\.(?:Status|StatusCode)$", " (HTTP status)", text)
    return text if len(text) <= 160 else text[:157] + "…"


_STATUS_NAMES = {re.sub(r"\s", "", v).lower(): k for k, v in STATUS.items()}


def _status_value(text: str) -> str | None:
    m = re.fullmatch(r"(?:\(int\))?\s*(?:HttpStatusCode\.)?(\w+)", text.strip())
    if m and m.group(1).lower() in _STATUS_NAMES:
        return status_text(_STATUS_NAMES[m.group(1).lower()])
    if re.fullmatch(r"[1-5]\d\d", text.strip()) and int(text) in STATUS:
        return status_text(int(text))
    m = re.fullmatch(r'"(MSG\d+)"', text.strip())
    if m:
        return message_text(m.group(1), None)
    return None


def _condition(body: str, at: int) -> str:
    """'when <cond>: ' / 'otherwise: ' when the check sits behind an if/else in the same statement."""
    start = max(body.rfind(";", 0, at), body.rfind("{", 0, at), body.rfind("}", 0, at)) + 1
    head = re.sub(r"\s+", " ", body[start:at]).strip()
    head = re.sub(r"^(?:await|var \w+ =|return)\s*", "", head)
    head = re.sub(r"\s*(?:await|var \w+ =|return)$", "", head)
    m = re.match(r"(else\s+)?if\s*\((.*)\)$", head)
    if m:
        return f"{'otherwise, ' if m.group(1) else ''}when {_expr(m.group(2))}: "
    if head == "else":
        return "otherwise: "
    return ""


def assertions(run: TestRun, limit: int = 25) -> list[str]:
    body = _method_body(run.cls, run.method)
    lines = []
    for m in re.finditer(r"Assert\.(\w+)(?:<([^>(]*)>)?\s*\(", body):
        end = _balanced(body, m.end() - 1)
        args = _split_args(body[m.end():end])
        kind, generic = m.group(1), m.group(2)
        a = [_expr(x) for x in args]
        line = None
        if kind in ("Equal", "NotEqual") and len(args) >= 2:
            known = _status_value(args[0])
            op = "=" if kind == "Equal" else "≠"
            line = f"{a[1]} {op} {known or a[0]}"
        elif kind in ("True", "False") and args:
            line = f"{a[0]} is {'true' if kind == 'True' else 'false'}"
        elif kind in ("Null", "NotNull") and args:
            line = f"{a[0]} is {'null' if kind == 'Null' else 'not null'}"
        elif kind in ("Empty", "NotEmpty") and args:
            line = f"{a[0]} is {'empty' if kind == 'Empty' else 'not empty'}"
        elif kind == "Single" and args:
            line = f"exactly one item in {a[0]}" + (f" matches {a[1]}" if len(a) > 1 else "")
        elif kind in ("Contains", "DoesNotContain") and len(args) >= 2:
            verb = "contains" if kind == "Contains" else "does not contain"
            line = f"{a[0]} {verb} an item where {a[1]}" if "=>" in args[1] else f"{a[1]} {verb} {a[0]}"
        elif kind == "All" and len(args) >= 2 and "Assert." not in args[1]:
            line = f"every item in {a[0]}: {a[1]}"
        elif kind in ("ThrowsAsync", "Throws"):
            line = f"{a[0] if a else 'the call'} throws {generic or 'an exception'}"
        elif kind == "InRange" and len(args) >= 3:
            line = f"{a[0]} is between {a[1]} and {a[2]}"
        elif kind in ("StartsWith", "EndsWith") and len(args) >= 2:
            line = f"{a[1]} {'starts' if kind == 'StartsWith' else 'ends'} with {a[0]}"
        if line:
            line = _condition(body, m.start()) + line
            if line not in lines:
                lines.append(line)
    # helper checks such as AssertMessage(response, HttpStatusCode.BadRequest, "MSG39") or Message(call, Conflict, "MSG40")
    for m in re.finditer(r"(?<![\w.])([A-Z]\w*)\s*\(", body):
        if m.group(1) in ("Assert", "Guid", "Task", "Enumerable"):
            continue
        end = _balanced(body, m.end() - 1)
        args = _split_args(body[m.end():end])
        known = [_status_value(a) for a in args[1:]]
        status = next((s for s in known if s and not s.startswith("MSG")), None)
        code = next((s for s in known if s and s.startswith("MSG")), None)
        if not (status or code) or not args or "Assert." in args[0]:
            continue
        line = _condition(body, m.start()) + f"{_expr(args[0])} returns " + " with ".join(x for x in (status, code) if x)
        if line not in lines:
            lines.append(line)
    if len(lines) > limit:
        lines = lines[:limit] + [f"… and {len(lines) - limit} more checks"]
    return lines


def _direct_calls(run: TestRun, limit: int = 12, awaited: bool = True) -> list[str]:
    body = _method_body(run.cls, run.method)
    calls = []
    for statement in re.split(r";\s*", body.strip("{} ")):
        statement = re.sub(r"\s+", " ", statement).strip().removeprefix("=>").strip()
        if ("await" in statement if awaited else "(" in statement) and "Assert." not in statement:
            calls.append(_expr(statement))
    return calls[:limit]


def _branches(checks: list[str], data: str) -> list[str]:
    """Keeps only the if/else branch that applies to this [Theory] row when the condition is a bool parameter."""
    values = dict(re.findall(r"(\w+): (True|False)\b", data or ""))
    kept, last = [], None
    for check in checks:
        m = re.match(r"when (!?)(\w+): (.*)", check)
        if m and m.group(2) in values:
            last = (values[m.group(2)] == "True") != bool(m.group(1))
            if last:
                kept.append(m.group(3))
            continue
        if check.startswith("otherwise: ") and last is not None:
            if not last:
                kept.append(check.removeprefix("otherwise: "))
            continue
        last = None
        kept.append(check)
    return kept


def _with_data(text: str, data: str) -> str:
    """Puts the [Theory] parameter values into a check: 'Rules.Valid(duration) = valid' -> '... (duration = 0) = False'."""
    if not data:
        return text
    for name, value in re.findall(r"(\w+): (\"[^\"]*\"|[^,)]+)", data):
        text = re.sub(rf"(?<![\w.\"]){re.escape(name)}(?![\w\"])", value.strip(), text)
    return text


IN_PROCESS = re.compile(r"\b(?:\w*[Ss]ervice|db\w*|Db|scope|context)\.\w+|SaveChangesAsync|\.QueryAsync\(|\.ExecuteAsync\(")


def describe(run: TestRun, case: CaseInfo | None):
    users = _users(run)
    description = (case.description if case else humanize(run.method, "")) + (f" {run.data}" if run.data else "")
    checks = [_with_data(c, run.data) for c in _branches(assertions(run), run.data)]
    if run.exchanges:
        procedure, per_step = _steps(run)
        in_process = [c for c in _direct_calls(run, 40) if IN_PROCESS.search(c)][:10]
        procedure += [f"Step {len(procedure) + i} (in the test process, no HTTP): {c}" for i, c in enumerate(in_process, 1)]
    else:
        calls = _direct_calls(run) or _direct_calls(run, awaited=False)
        if not calls and checks:  # expression-bodied checks such as Assert.Equal(valid, Rules.Valid(duration, isPublic))
            calls = [f"evaluate {c.split(' = ')[0].split(' is ')[0]}" for c in checks[:5]]
        procedure = ([f"Step {i} (in the test process, no HTTP): {c}" for i, c in enumerate(calls, 1)]
                     or ["Step 1: Call the service / DbContext directly in the test process (no HTTP request)."])
        if run.data:
            procedure.append(f"Test data: {run.data.strip('()')}")
        per_step = []
    expected = []
    if run.outcome == "Failed":
        expected.append("Actual (failed): " + run.message[:600])
    expected += per_step
    if checks:
        expected.append("Verified by the test:\n" + "\n".join("- " + c for c in checks))
    if not expected:
        expected.append("All checks of the test pass (data is saved and read back correctly).")
    pre = case.pre if case else DEFAULT_PRE
    if users:
        pre += "\nAccounts used: " + ", ".join(users) + " (demo accounts seeded with a random password per run)."
    return description, "\n".join(procedure), "\n".join(expected), pre, users


# ---------- workbook ----------

def copy_style(src, dst):
    dst.font = copy.copy(src.font); dst.fill = copy.copy(src.fill); dst.border = copy.copy(src.border)
    dst.alignment = copy.copy(src.alignment); dst.number_format = src.number_format; dst.protection = copy.copy(src.protection)


def clip(text: str, n: int = 32000) -> str:
    return text if len(text) <= n else text[:n - 1] + "…"


def build(template: str, results: str, out: str, project: str, code: str, creator: str) -> dict:
    cases = read_case_attributes()
    runs = read_trx(results)
    attach_exchanges(runs, results)
    summary_path = os.path.join(results, "coverage-report", "Summary.json")
    coverage = json.load(open(summary_path, encoding="utf-8"))["summary"] if os.path.exists(summary_path) else None
    run_day = min(r.start for r in runs).astimezone(LOCAL_TZ).date()

    wb = openpyxl.load_workbook(template)
    proto = wb["Feature Name1"]

    # Cover
    cover = wb["Cover"]
    cover["B4"] = project; cover["B5"] = code; cover["F4"] = creator; cover["F5"] = run_day
    cover["F5"].number_format = "dd/mm/yyyy"
    cover["B6"] = '=B5&"_"&"Integration Test Report"&"_"&"v1.0"'
    cover["F6"] = "1.0"
    cover["A11"] = run_day; cover["A11"].number_format = "dd/mm/yyyy"
    cover["B11"] = "1.0"; cover["C11"] = "Whole backend API"; cover["D11"] = "A"
    cover["E11"] = "Integration test report generated from an automated run (xUnit + PostgreSQL), with code coverage."
    cover["F11"] = "docs/testing/INTEGRATION-TEST-COVERAGE.vi.md; backend/tests/DCMS.IntegrationTests"

    by_class: dict[str, list[TestRun]] = defaultdict(list)
    for r in runs:
        by_class[r.cls].append(r)

    def order(run: TestRun):
        case = cases.get(f"{run.cls}.{run.method}")
        key = case.key if case else "~" + run.method  # documented cases first, by their ID
        return [int(p) if p.isdigit() else p for p in re.split(r"(\d+)", key)] + [run.data]
    for items in by_class.values():
        items.sort(key=order)
    known = {c for _, _, _, classes in MODULES for c in classes}
    extra = sorted(c for c in by_class if c not in known)
    modules = [m for m in MODULES] + ([("OTHER", "Other", "Tests not assigned to a module.", extra)] if extra else [])

    stats_rows, function_rows, security = [], [], []
    used_ids: dict[str, int] = defaultdict(int)
    for sheet_name, title, requirement, classes in modules:
        present = [c for c in classes if by_class.get(c)]
        if not present:
            continue
        ws = wb.copy_worksheet(proto)
        ws.title = sheet_name
        for row in ws.iter_rows(min_row=11, max_row=ws.max_row):
            for cell in row:
                cell.value = None
        ws["B2"] = title; ws["B3"] = requirement
        ws["B4"] = '=COUNTIF($F$12:$F$1000,"?*")'
        for r_, col in ((6, "F"), (7, "I"), (8, "L")):
            for c_, letter in zip("BCDE", "BCDE"):
                ws[f"{c_}{r_}"] = f'=COUNTIF(${col}$12:${col}$1000,{letter}$5)'
        ws.data_validations.dataValidation = []
        dv = DataValidation(type="list", formula1="$S$2:$S$5", allow_blank=True)
        dv.add("F12:F1000"); dv.add("I12:I1000"); dv.add("L12:L1000")
        ws.add_data_validation(dv)
        row = 11
        counter = 0
        for cls in present:
            for col in range(1, 17):
                copy_style(proto.cell(11, col), ws.cell(row, col))
            ws.cell(row, 1, CLASS_TITLES.get(cls, cls))
            ws.merge_cells(start_row=row, start_column=1, end_row=row, end_column=5)
            function_rows.append((CLASS_TITLES.get(cls, cls), sheet_name, cls, len(by_class[cls])))
            row += 1
            for run in by_class[cls]:
                counter += 1
                case = cases.get(f"{run.cls}.{run.method}")
                base = case.key if case else f"{sheet_name[:3]}-{counter:03d}"
                used_ids[base] += 1
                tc_id = base if used_ids[base] == 1 else f"{base}.{used_ids[base]}"
                description, procedure, expected, pre, users = describe(run, case)
                for col in range(1, 17):
                    copy_style(proto.cell(12, col), ws.cell(row, col))
                values = {1: tc_id, 2: clip(description), 3: clip(procedure), 4: clip(expected), 5: clip(pre),
                          6: run.outcome, 7: run.start.astimezone(LOCAL_TZ).date(), 8: "xUnit (automated)",
                          15: f"DEF-{tc_id}" if run.outcome == "Failed" else None,
                          16: f"{run.cls}.{run.method}{run.data}"}
                for col, value in values.items():
                    ws.cell(row, col, value)
                ws.cell(row, 7).number_format = "dd/mm/yyyy"
                statuses = {ex["status"] for ex in run.exchanges}
                if 401 in statuses or 403 in statuses or cls in ("BranchIsolationTests", "AllScreensPermissionApiTests"):
                    security.append((run, description, pre, users, statuses))
                row += 1
        stats_rows.append(sheet_name)

    # Test Cases sheet: one row per function (test class)
    tc = wb["Test Cases"]
    tc["D3"] = "=Cover!B4"; tc["D4"] = "=Cover!B5"
    tc["D5"] = ("1. Server: ASP.NET Core API (.NET 10) hosted in the test process with WebApplicationFactory, Development environment.\n"
                "2. Database: PostgreSQL 17 (Docker, infrastructure/docker/compose.yaml); each run creates its own schema, migrates it and seeds the demo accounts.\n"
                "3. External services (email/SMS, SignalR, PayOS, Google, AI): replaced by in-memory fakes.\n"
                "4. Web browser: not needed (tests call HTTP directly).\n"
                "5. Tools: xUnit 2.9, coverlet.collector, ReportGenerator; run with backend/tests/run-integration-coverage.ps1.")
    for i, (name, sheet, cls, count) in enumerate(function_rows):
        r_ = 9 + i
        for col in range(2, 7):
            copy_style(tc.cell(9, col), tc.cell(r_, col))
        tc.cell(r_, 2, i + 1); tc.cell(r_, 3, name); tc.cell(r_, 4, sheet)
        tc.cell(r_, 5, f"{count} test cases – class {cls}")
        tc.cell(r_, 6, "Test API and PostgreSQL are running; demo accounts are seeded")
    for r_ in range(9 + len(function_rows), 9 + max(len(function_rows), 5) + 1):
        for col in range(2, 7):
            tc.cell(r_, col).value = None

    # Test Statistics
    st = wb["Test Statistics"]
    st["C3"] = "=Cover!B4"; st["C4"] = "=Cover!B5"; st["H3"] = creator; st["H5"] = run_day
    st["H5"].number_format = "dd/mm/yyyy"
    st["C5"] = '=C4&"_"&"Test Report"&"_"&"v1.0"'
    st["C6"] = f"Run of {run_day:%d/%m/%Y} covering {len(stats_rows)} modules: " + ", ".join(stats_rows) + "."
    row_style = [copy.copy(st.cell(11, c)._style) for c in range(1, 10)]
    sub_style = [copy.copy(st.cell(14, c)._style) for c in range(1, 10)]
    cov_style = [copy.copy(st.cell(16, c)._style) for c in range(1, 10)]
    for r_ in range(11, 40):
        for c in range(1, 10):
            st.cell(r_, c).value = None
    for merged in list(st.merged_cells.ranges):
        if merged.min_row >= 11:
            st.unmerge_cells(str(merged))
    first = 11
    for i, sheet in enumerate(stats_rows):
        r_ = first + i
        for c in range(1, 10):
            st.cell(r_, c)._style = copy.copy(row_style[c - 1])
        q = f"'{sheet}'"
        st.cell(r_, 2, i + 1); st.cell(r_, 3, f"={q}!B2")
        st.cell(r_, 4, f"={q}!B6"); st.cell(r_, 5, f"={q}!C6"); st.cell(r_, 6, f"={q}!D6")
        st.cell(r_, 7, f"={q}!E6"); st.cell(r_, 8, f"={q}!B4")
    last = first + len(stats_rows) - 1
    sub = last + 2
    for c in range(1, 10):
        st.cell(sub, c)._style = copy.copy(sub_style[c - 1])
    st.cell(sub, 3, "Sub total")
    for c, letter in zip(range(4, 9), "DEFGH"):
        st.cell(sub, c, f"=SUM({letter}{first}:{letter}{last})")
    lines = [("Test coverage", f"=IF(H{sub}-G{sub}=0,0,(D{sub}+E{sub})*100/(H{sub}-G{sub}))", "%"),
             ("Test successful coverage", f"=IF(H{sub}-G{sub}=0,0,D{sub}*100/(H{sub}-G{sub}))", "%")]
    if coverage:
        lines += [("Code coverage – line", round(float(coverage["linecoverage"]), 2), "%"),
                  ("Code coverage – branch", round(float(coverage.get("branchcoverage") or 0), 2), "%"),
                  ("Code coverage – method", round(float(coverage.get("methodcoverage") or 0), 2), "%")]
    for i, (label, value, unit) in enumerate(lines):
        r_ = sub + 2 + i
        for c in range(1, 10):
            st.cell(r_, c)._style = copy.copy(cov_style[c - 1])
        st.cell(r_, 3, label); st.cell(r_, 5, value); st.cell(r_, 6, unit)
        st.cell(r_, 5).number_format = "0.00"
    if coverage:
        note = sub + 2 + len(lines)
        st.cell(note, 3, (f"Code coverage source: coverlet.collector + ReportGenerator, run of {run_day:%d/%m/%Y}; "
                          f"{coverage['coveredlines']}/{coverage['coverablelines']} lines; measured on DCMS.Api, DCMS.Application, "
                          "DCMS.Domain, DCMS.Infrastructure; Migrations (EF Core generated code) excluded."))

    # Security sheet
    sec = wb["SecurityTest"]
    header_style = [copy.copy(sec.cell(2, c)._style) for c in range(1, 11)]
    for r_ in range(2, sec.max_row + 1):
        for c in range(1, 11):
            sec.cell(r_, c).value = None
    for i, (run, description, pre, users, statuses) in enumerate(security):
        r_ = 2 + i
        for c in range(1, 11):
            sec.cell(r_, c)._style = copy.copy(header_style[c - 1])
        blocked = [ex for ex in run.exchanges if ex["status"] in (401, 403)]
        denied, outcomes = [], []
        for n, ex in enumerate(blocked[:10], 1):
            actor = f"as {ex['caller']}" if ex.get("caller") else "anonymous (no access token)"
            details = request_lines(ex)[:8]
            denied.append(f"Step {n}: {ex['method']} {_path(ex['path'].split('?')[0])} – {actor}"
                          + "".join("\n   - " + d for d in details))
            outcomes.append(f"Step {n} → {response_summary(ex)}; no data is returned or changed")
        technique = ("Equivalence Partitioning (EP)" if run.cls == "BranchIsolationTests"
                     else "Error Guessing" if 401 in statuses and 403 not in statuses
                     else "Decision Table Testing" if run.cls == "AllScreensPermissionApiTests" else "Use Case Testing")
        role = ", ".join(users) if users else "anonymous (no access token)"
        if 401 in statuses and users:
            role += " and anonymous (no access token)"
        then = " / ".join(sorted({f"HTTP {s} {'Unauthorized' if s == 401 else 'Forbidden'}" for s in statuses if s in (401, 403)}))
        values = [f"TC-SEC-{i + 1:03d}", clip(description, 500), role, technique, clip(pre, 1000),
                  clip("\n".join(denied[:10]) or "Call the API with a role / branch that is not allowed", 2000),
                  clip("\n".join(outcomes), 2000) or then or "No data of another branch / role is returned",
                  "Pass" if run.outcome == "Passed" else "Fail" if run.outcome == "Failed" else "Not Run",
                  f"DEF-{run.cls}" if run.outcome == "Failed" else None, f"{run.cls}.{run.method}{run.data}"]
        for c, value in enumerate(values, 1):
            sec.cell(r_, c, value)

    del wb["Feature Name1"]
    del wb["Feature Name2"]
    order = ["Cover", "Test Cases", "Test Statistics"] + stats_rows + ["SecurityTest"]
    wb._sheets = [wb[name] for name in order]
    wb.active = 0
    wb.save(out)
    counts = defaultdict(int)
    for r in runs:
        counts[r.outcome] += 1
    return {"tests": len(runs), **counts, "security": len(security), "modules": stats_rows,
            "coverage": coverage and {k: coverage[k] for k in ("linecoverage", "branchcoverage", "methodcoverage", "coveredlines", "coverablelines")}}


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--template", required=True)
    parser.add_argument("--results", required=True)
    parser.add_argument("--out", required=True)
    parser.add_argument("--project", default="DCMS – Dental Clinic Management System")
    parser.add_argument("--code", default="DCMS")
    parser.add_argument("--creator", default="")
    args = parser.parse_args()
    print(json.dumps(build(args.template, args.results, args.out, args.project, args.code, args.creator), ensure_ascii=False, indent=1))


if __name__ == "__main__":
    main()
