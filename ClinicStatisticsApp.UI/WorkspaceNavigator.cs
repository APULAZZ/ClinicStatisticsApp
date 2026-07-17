using System;

namespace ClinicStatisticsApp.UI;

public static class WorkspaceNavigator
{
    public static Action<object?>? NavigateAction { get; set; }

    public static void Navigate(object? page) => NavigateAction?.Invoke(page);
}
