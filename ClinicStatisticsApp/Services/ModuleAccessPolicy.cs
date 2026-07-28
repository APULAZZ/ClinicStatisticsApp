namespace ClinicStatisticsApp.Services;

public static class ModuleAccessPolicy
{
    public const string AdminRole = "Admin";
    public const string ManagerRole = "Manager";
    public const string BranchUserRole = "BranchUser";
    public const string StatisticsRole = "Statistics";
    public const string CallCenterRole = "CallCenter";
    public const string CallCenterAdminRole = "CallCenterAdmin";

    public static bool CanUseGeneralStatistics(string? roleCode)
        => roleCode is AdminRole or ManagerRole or BranchUserRole or StatisticsRole;

    public static bool CanUseSchedule(string? roleCode)
        => roleCode is AdminRole or ManagerRole or BranchUserRole or StatisticsRole;

    public static bool CanUseCallCenter(string? roleCode)
        => roleCode is AdminRole or CallCenterRole or CallCenterAdminRole;

    public static bool CanManageCallCenter(string? roleCode)
        => roleCode is AdminRole or CallCenterAdminRole;
}
