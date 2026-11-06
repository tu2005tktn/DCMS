using System.Text.Json.Serialization;
using DCMS.Domain.Features.Visits;
namespace DCMS.Application.Features.Finance;
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ExaminationFeeChoice(int? ServiceId=null,int? ExpectedPriceVersion=null,long? AmountVnd=null,string? Reason=null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SetExaminationFee(int PatientId,int ExpectedVersion,Guid RequestKey,ExaminationFeeChoice Fee);
public sealed record ExaminationFeeDto(Guid VisitId,int PatientId,int BranchId,string VisitStatus,bool Eligible,bool Legacy,string State,int Version,
 int? ServiceId,string? ServiceName,long? AmountVnd,string? Reason,string? PriceSource,DateTimeOffset? PostedAt,Guid? ChargeId,
 int? DefaultServiceId,ExaminationServiceOption[] Services,string[] AllowedActions);
public sealed record ExaminationServiceOption(int Id,int BranchId,string Name,string Unit,long? PriceVnd,int Version,bool Active);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SaveExaminationService(Guid RequestKey,int BranchId,string Name,long? PriceVnd,int ExpectedVersion=0);
public interface IExaminationFeeService {
 Task<ExaminationFeeDto> GetAsync(int actor,Guid visit,CancellationToken ct);
 Task<ExaminationFeeDto> SetAsync(int actor,Guid visit,SetExaminationFee command,CancellationToken ct);
 Task<ExaminationServiceOption[]> ServicesAsync(int actor,int branch,CancellationToken ct);
 Task<ExaminationServiceOption> SaveServiceAsync(int actor,int? id,SaveExaminationService command,CancellationToken ct);
 Task OnCompletionAsync(int actor,Visit visit,ExaminationFeeChoice? choice,CancellationToken ct);
}
