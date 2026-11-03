namespace DCMS.Domain.Features.Treatment;

public static class TreatmentItemRules
{
    // An item counts as agreed work either through an explicit decision or a clinician entry made directly on the sheet.
    public static bool IsEffective(TreatmentItem item) =>
        item.Decision == "Agreed" || item.EntrySource == "ClinicianEntry";
}
