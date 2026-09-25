namespace ContainerControl.Modules.Applications.Workloads;

public static class DatabaseImageOverride
{
    // Turning the flag on needs platform.settings.manage.
    // Leaving it false, keeping it true, or turning it off needs only apps.write.
    public static bool MayEnable(bool requested, bool currentlyAllowed, bool callerMayManageSettings) =>
        !requested || currentlyAllowed || callerMayManageSettings;
}
