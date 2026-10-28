using System.Text.Json;
using DCMS.Application.Common;
using DCMS.Application.Features.Finance;
using DCMS.Application.Features.Settings;
using DCMS.Domain.Features.Examinations;
using DCMS.Domain.Features.Finance;
using DCMS.Domain.Features.Scheduling;
using DCMS.Domain.Features.Visits;
using DCMS.Infrastructure.Features.Examinations;
using DCMS.Infrastructure.Features.Scheduling;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using static DCMS.Infrastructure.Features.Settings.SettingsAccess;
namespace DCMS.Infrastructure.Features.Finance;

public sealed class ExaminationFeeService(DcmsDbContext db,ISettingsAccess access,FinanceService ledger,TimeProvider clock):IExaminationFeeService
{
 static BusinessException Invalid(string text,int status=400)=>new(status,new SystemMessage("EXAMINATION_FEE",text){Field="examinationFee"});
 static void Money(long? n){if(n is <0 or >FinanceService.MaxAmount)throw Invalid("Phí khám phải từ 0 đến 1.000.000.000.000 đồng.");}
 async Task<Visit> Visit(int actor,Guid id,CancellationToken ct){var v=await db.Visits.SingleOrDefaultAsync(x=>x.Id==id,ct)??throw Error("MSG113",404);await access.OperationalAsync(actor,"finance.read",v.BranchId,ct);return v;}
 async Task<bool> Own(int actor,Visit v,PermissionState s,CancellationToken ct)=>s.Owner||await db.Employees.AnyAsync(x=>x.Id==v.DentistId&&x.AccountId==actor,ct);
 async Task<bool> Eligible(Visit v,CancellationToken ct)=>v.Purpose!="TreatmentContinuation"&&await db.Set<VisitExamination>().AnyAsync(x=>x.VisitId==v.Id&&x.Confirmed,ct);
 static ExaminationServiceOption Option(SchedulingService s)=>new(s.Id,s.BranchId,s.Name,s.Unit,s.ReferenceUnitPriceVnd,s.PriceVersion,s.Active);
 async Task<SchedulingService[]> Catalog(int branch,CancellationToken ct)=>await db.Set<SchedulingService>().AsNoTracking().Where(x=>x.BranchId==branch&&x.IsInitialExamination&&x.Active&&!x.IsInvestigationService).OrderBy(x=>x.Name).ToArrayAsync(ct);
 async Task<int?> DefaultService(Visit v,SchedulingService[] services,CancellationToken ct){var booked=await db.Appointments.Where(x=>x.Id==v.AppointmentId).Select(x=>x.ServiceId).SingleAsync(ct);return services.Any(x=>x.Id==booked)?booked:services.Length==1?services[0].Id:null;}
 public async Task<ExaminationServiceOption[]> ServicesAsync(int actor,int branch,CancellationToken ct){var s=await access.ResolveAsync(actor,ct);if(!s.BranchIds.Contains(branch)||!s.Permissions.Any(x=>x is "finance.read" or "services.read"))throw Error("MSG06",403);return (await Catalog(branch,ct)).Select(Option).ToArray();}
 public async Task<ExaminationFeeDto> GetAsync(int actor,Guid visit,CancellationToken ct){
  var v=await Visit(actor,visit,ct);var s=await access.ResolveAsync(actor,ct);var fee=await db.Set<ExaminationFee>().AsNoTracking().SingleOrDefaultAsync(x=>x.VisitId==visit,ct);var services=await Catalog(v.BranchId,ct);var eligible=await Eligible(v,ct);var own=await Own(actor,v,s,ct);var actions=new List<string>();
  if(eligible&&fee?.State!="Posted"){
   if(s.Permissions.Contains("finance.quotes.manage")&&own)actions.Add("override");
   if(v.Status=="Completed"&&(s.Permissions.Contains("finance.payments.create")||actions.Contains("override")))actions.Add("set");
  }
  return new(v.Id,v.PatientId,v.BranchId,v.Status,eligible,fee==null&&v.Status=="Completed",fee?.State??(v.Status=="Completed"?"Unspecified":"Preview"),fee?.Version??0,fee?.ServiceId,fee?.ServiceName,fee?.AmountVnd,fee?.Reason,fee?.PriceSource,fee?.PostedAt,fee?.ChargeId,await DefaultService(v,services,ct),services.Select(Option).ToArray(),actions.ToArray());
 }
 public async Task OnCompletionAsync(int actor,Visit visit,ExaminationFeeChoice? choice,CancellationToken ct){
  if(db.Database.CurrentTransaction==null)throw new InvalidOperationException("Examination fee must share completion transaction.");
  var existing=await db.Set<ExaminationFee>().SingleOrDefaultAsync(x=>x.VisitId==visit.Id,ct);if(existing!=null)throw Invalid("Lượt khám đã có thông tin phí. Hãy tải lại để kiểm tra.",409);
  var services=await Catalog(visit.BranchId,ct);var id=choice?.ServiceId??await DefaultService(visit,services,ct);
  var fee=new ExaminationFee{VisitId=visit.Id,CreatedBy=actor,CreatedAt=clock.GetUtcNow()};db.Add(fee);
  if(choice!=null&&choice.AmountVnd==null){fee.Reason=CleanReason(choice.Reason);Audit(actor,visit,"DeferExaminationFee",new{fee.VisitId},ct);return;}
  if(id==null){if(choice?.AmountVnd!=null)throw Invalid("Chọn dịch vụ khám hợp lệ trước khi ghi phí.");Audit(actor,visit,"PendingExaminationFee",new{fee.VisitId},ct);return;}
  var service=await LockedService(visit.BranchId,id.Value,ct);
  if(choice!=null&&service.PriceVersion!=choice.ExpectedPriceVersion)throw Error("MSG111",409);
  var amount=choice?.AmountVnd??service.ReferenceUnitPriceVnd;
  if(amount==null){fee.ServiceId=service.Id;fee.ServiceName=service.Name;Audit(actor,visit,"PendingExaminationFee",new{fee.VisitId,service.Id},ct);return;}
  var state=await access.ResolveAsync(actor,ct);var isOverride=amount!=service.ReferenceUnitPriceVnd;
  if(isOverride&&(!state.Permissions.Contains("finance.quotes.manage")||!await Own(actor,visit,state,ct)))throw Error("MSG06",403);
  await Post(actor,visit,fee,service,amount.Value,choice?.Reason,isOverride,false,ct);
 }
 static string? CleanReason(string? reason){if(reason?.Length>1000)throw Invalid("Lý do phí khám tối đa 1.000 ký tự.");return string.IsNullOrWhiteSpace(reason)?null:reason.Trim();}
 async Task<SchedulingService> LockedService(int branch,int id,CancellationToken ct){await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({(long)id+905000L})",ct);return await db.Set<SchedulingService>().SingleOrDefaultAsync(x=>x.Id==id&&x.BranchId==branch&&x.Active&&x.IsInitialExamination&&!x.IsInvestigationService,ct)??throw Invalid("Dịch vụ khám không còn hoạt động hoặc không thuộc chi nhánh này.",409);}
 async Task Post(int actor,Visit v,ExaminationFee fee,SchedulingService service,long amount,string? reason,bool isOverride,bool legacy,CancellationToken ct){
  Money(amount);reason=CleanReason(reason);if((isOverride||legacy)&&reason==null)throw Invalid(legacy?"Ghi lý do bổ sung phí cho lượt khám cũ.":"Ghi căn cứ giá thỏa thuận hoặc miễn phí khác bảng giá.");
  var charge=new FinanceCharge{VisitId=v.Id,PatientId=v.PatientId,BranchId=v.BranchId,SourceKind="Examination",SourceId=v.Id,ServiceId=service.Id,Name=service.Name,Unit=string.IsNullOrWhiteSpace(service.Unit)?"Lần":service.Unit,Quantity=1,UnitPriceVnd=amount,AmountVnd=amount,PriceVersion=service.PriceVersion,PostedBy=actor,PostedAt=clock.GetUtcNow()};await ledger.AddCharge(charge,ct);
  fee.State="Posted";fee.ServiceId=service.Id;fee.ServiceName=service.Name;fee.Unit=charge.Unit;fee.AmountVnd=amount;fee.PriceVersion=service.PriceVersion;fee.Reason=reason??(amount==0?"Miễn phí theo bảng giá dịch vụ khám":null);fee.PriceSource=isOverride?"Override":"Catalog";fee.ChargeId=charge.Id;fee.PostedBy=actor;fee.PostedAt=clock.GetUtcNow();
  Audit(actor,v,"PostExaminationFee",new{fee.VisitId,chargeId=charge.Id,serviceId=service.Id,amount,fee.PriceSource,fee.Reason},ct);
 }
 void Audit(int actor,Visit visit,string action,object payload,CancellationToken ct)=>db.Add(new FinanceAudit{ActorId=actor,BranchId=visit.BranchId,VisitId=visit.Id,Action=action,Target=visit.Id.ToString(),PayloadJson=JsonSerializer.Serialize(payload,ExaminationService.Json),At=clock.GetUtcNow()});
 public async Task<ExaminationFeeDto> SetAsync(int actor,Guid visit,SetExaminationFee c,CancellationToken ct){
  var v=await Visit(actor,visit,ct);var state=await access.ResolveAsync(actor,ct);if(!state.Permissions.Contains("finance.payments.create")&&!state.Permissions.Contains("finance.quotes.manage"))throw Error("MSG06",403);if(c.RequestKey==Guid.Empty)throw Error("MSG04");
  await using var tx=await db.Database.BeginTransactionAsync(ct);await db.Database.ExecuteSqlRawAsync($"SELECT pg_advisory_xact_lock({WorkingScheduleService.TransactionLock})",ct);
  await access.OperationalAsync(actor,"finance.read",v.BranchId,ct);state=await access.ResolveAsync(actor,ct);if(!state.Permissions.Contains("finance.payments.create")&&!state.Permissions.Contains("finance.quotes.manage"))throw Error("MSG06",403);
  await db.Entry(v).ReloadAsync(ct);if(v.PatientId!=c.PatientId)throw Error("MSG113",404);if(v.Status!="Completed"||!await Eligible(v,ct))throw Invalid("Chỉ ghi phí sau khi lượt khám đã kết thúc và phiếu khám đã xác nhận.",409);
  var fingerprint=ExaminationService.Hash(new{action="SetExaminationFee",visit,c});var old=await db.Set<FinanceCommandReceipt>().SingleOrDefaultAsync(x=>x.ActorId==actor&&x.RequestKey==c.RequestKey,ct);if(old!=null){if(old.Fingerprint!=fingerprint)throw Error("MSG111",409);await tx.CommitAsync(ct);return await GetAsync(actor,visit,ct);}
  var fee=await db.Set<ExaminationFee>().SingleOrDefaultAsync(x=>x.VisitId==visit,ct);if((fee?.Version??0)!=c.ExpectedVersion)throw Error("MSG111",409);if(fee?.State=="Posted")throw Invalid("Phí khám đã ghi sổ; không thể đổi hoặc ghi thêm lần nữa.",409);
  if(c.Fee.ServiceId==null||c.Fee.AmountVnd==null)throw Invalid("Chọn dịch vụ và nhập phí khám hoặc số 0 nếu miễn phí.");
  var service=await LockedService(v.BranchId,c.Fee.ServiceId.Value,ct);if(service.PriceVersion!=c.Fee.ExpectedPriceVersion)throw Error("MSG111",409);
  var isOverride=c.Fee.AmountVnd!=service.ReferenceUnitPriceVnd;if(isOverride&&(!state.Permissions.Contains("finance.quotes.manage")||!await Own(actor,v,state,ct)))throw Error("MSG06",403);
  var legacy=fee==null;if(fee==null){fee=new(){VisitId=visit,CreatedBy=actor,CreatedAt=clock.GetUtcNow()};db.Add(fee);}else fee.Version++;
  await Post(actor,v,fee,service,c.Fee.AmountVnd.Value,c.Fee.Reason,isOverride,legacy,ct);db.Add(new FinanceCommandReceipt{ActorId=actor,RequestKey=c.RequestKey,Fingerprint=fingerprint,ResultJson="{}"});await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return await GetAsync(actor,visit,ct);
 }
 public async Task<ExaminationServiceOption> SaveServiceAsync(int actor,int? id,SaveExaminationService c,CancellationToken ct){
  await access.OperationalAsync(actor,"services.manage",c.BranchId,ct);Money(c.PriceVnd);if(c.RequestKey==Guid.Empty||string.IsNullOrWhiteSpace(c.Name)||c.Name.Trim().Length>160)throw Error("MSG04");
  await using var tx=await db.Database.BeginTransactionAsync(ct);await db.Database.ExecuteSqlRawAsync($"SELECT pg_advisory_xact_lock({WorkingScheduleService.TransactionLock})",ct);await access.OperationalAsync(actor,"services.manage",c.BranchId,ct);
  var hash=ExaminationService.Hash(new{action="SaveExaminationService",id,c});var receipt=await db.Set<FinanceCommandReceipt>().SingleOrDefaultAsync(x=>x.ActorId==actor&&x.RequestKey==c.RequestKey,ct);if(receipt!=null){if(receipt.Fingerprint!=hash)throw Error("MSG111",409);await tx.CommitAsync(ct);return JsonSerializer.Deserialize<ExaminationServiceOption>(receipt.ResultJson,ExaminationService.Json)!;}
  SchedulingService service;
  if(id.HasValue){service=await LockedService(c.BranchId,id.Value,ct);if(service.PriceVersion!=c.ExpectedVersion)throw Error("MSG111",409);service.PriceVersion++;}
  else {if(c.ExpectedVersion!=0)throw Error("MSG111",409);if(await db.Set<SchedulingService>().AnyAsync(x=>x.BranchId==c.BranchId&&x.Active&&x.IsInitialExamination,ct))throw Invalid("Chi nhánh đã có dịch vụ khám. Hãy sửa giá dịch vụ hiện có.",409);service=new(){BranchId=c.BranchId,IsInitialExamination=true,PublicBookable=true,Unit="Lần",BillingUnit="Lần"};db.Add(service);}
  if(await db.Set<SchedulingService>().AnyAsync(x=>x.BranchId==c.BranchId&&x.Id!=service.Id&&x.Name.ToUpper()==c.Name.Trim().ToUpper(),ct))throw Invalid("Tên dịch vụ đã tồn tại; cần kiểm tra danh mục hiện có.",409);
  service.Name=c.Name.Trim();service.ReferenceUnitPriceVnd=c.PriceVnd;await db.SaveChangesAsync(ct);var result=Option(service);db.Add(new FinanceAudit{ActorId=actor,BranchId=c.BranchId,Action="SaveExaminationService",Target=service.Id.ToString(),PayloadJson=JsonSerializer.Serialize(c,ExaminationService.Json),At=clock.GetUtcNow()});db.Add(new FinanceCommandReceipt{ActorId=actor,RequestKey=c.RequestKey,Fingerprint=hash,ResultJson=JsonSerializer.Serialize(result,ExaminationService.Json)});await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return result;
 }
}
