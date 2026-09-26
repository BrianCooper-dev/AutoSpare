using MudBlazor;

namespace AutoSpare.Web.Components.Common;

public static class SnackbarExtensions
{
    public static void ShowSuccess(this ISnackbar snackbar, string message = "عملیات با موفقیت انجام شد.")
    {
        snackbar.Add(message, Severity.Success);
    }

    public static void ShowError(this ISnackbar snackbar, string message = "خطایی در انجام عملیات رخ داد.")
    {
        snackbar.Add(message, Severity.Error);
    }

    public static void ShowWarning(this ISnackbar snackbar, string message)
    {
        snackbar.Add(message, Severity.Warning);
    }

    public static void ShowInfo(this ISnackbar snackbar, string message)
    {
        snackbar.Add(message, Severity.Info);
    }
}
