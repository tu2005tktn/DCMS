namespace DCMS.Domain.Features.Finance;
public sealed class ExaminationFee {
 public Guid VisitId {get;set;}
 public string State {get;set;}="Pending";
 public int Version {get;set;}=1;
 public int? ServiceId {get;set;}
 public string? ServiceName {get;set;}
 public string? Unit {get;set;}
 public long? AmountVnd {get;set;}
 public int? PriceVersion {get;set;}
 public string? Reason {get;set;}
 public string? PriceSource {get;set;}
 public Guid? ChargeId {get;set;}
 public int CreatedBy {get;set;}
 public DateTimeOffset CreatedAt {get;set;}
 public int? PostedBy {get;set;}
 public DateTimeOffset? PostedAt {get;set;}
}
