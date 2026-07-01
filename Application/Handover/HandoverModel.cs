namespace AssetMgmt.Application.Handover;

/// <summary>Flat view model passed to the QuestPDF handover template.</summary>
public record HandoverModel(
    string DocumentNumber,
    DateTime GeneratedAt,
    string AssetCode,
    string ModelName,
    string Serial,
    string? Location,
    decimal AcquisitionCost,
    string EmployeeName,
    string EmployeeCode,
    string? EmployeeDepartment,
    string ApproverName,
    DateTime HandoverDate,
    string? Notes);
