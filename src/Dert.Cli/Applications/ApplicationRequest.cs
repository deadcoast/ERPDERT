namespace Dert.Cli.Applications;

internal enum ApplicationAction { UpdateDiscovery, UpdateRehearsal, Where, Doctor }

// The CLI and DERT binder produce the same typed request. Neither forwards text
// to a shell or promotes an application's name into update/doctor capability.
internal sealed record ApplicationRequest(ApplicationDefinition Application, ApplicationAction Action,
    string? SelectedPath = null, string? Approval = null, string? SourcePath = null)
{
    public string Command => $"DERT {(Action is ApplicationAction.Where or ApplicationAction.Doctor ? "where" : "update")} " +
        (Application.Id.Any(c => c is ' ' or '(' or ')') ? "'" + Application.Id.Replace("'", "''") + "'" : Application.Id) +
        (Action == ApplicationAction.UpdateRehearsal ? " updateme" : Action == ApplicationAction.Doctor ? " --doctor" : "");

    public static bool TryRead(string[] args, out ApplicationRequest? request, out string reason)
    {
        request = null;
        reason = "Use DERT update codex [updateme], or DERT where <application> [--doctor | -dr].";
        if (args.Length < 2 || !ApplicationCatalog.TryResolve(args[1], out var application, out reason)) return false;
        var action = args[0].Equals("where", StringComparison.OrdinalIgnoreCase) ? ApplicationAction.Where : ApplicationAction.UpdateDiscovery;
        string? selected = null, approval = null, source = null;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 2; index < args.Length; index++)
        {
            var option = args[index] == "-dr" ? "--doctor" : args[index];
            if (!seen.Add(option)) { reason = $"'{option}' was supplied more than once. Choose each option once."; return false; }
            switch (option)
            {
                case "--plain": case "--still": break;
                case "updateme" when action == ApplicationAction.UpdateDiscovery: action = ApplicationAction.UpdateRehearsal; break;
                case "--doctor" when action == ApplicationAction.Where: action = ApplicationAction.Doctor; break;
                case "--path": case "--apply": case "--from-source":
                    if (++index >= args.Length || string.IsNullOrWhiteSpace(args[index]) || args[index].StartsWith('-'))
                    { reason = $"'{option}' needs its value immediately after it."; return false; }
                    if (option == "--path") selected = args[index];
                    else if (option == "--apply") approval = args[index];
                    else source = args[index];
                    break;
                default: reason = $"'{args[index]}' is not an option for this command. Use DERT update codex [updateme] or DERT where <application> [--doctor | -dr]."; return false;
            }
        }
        if (action is ApplicationAction.UpdateDiscovery or ApplicationAction.UpdateRehearsal && application.NpmPackage is null)
        { reason = $"'{application.DisplayName}' has no update adapter in this build. DERT where can still locate it."; return false; }
        if (approval is not null && action != ApplicationAction.Doctor || source is not null && action != ApplicationAction.Doctor)
        { reason = "--apply and --from-source belong to an exact doctor proposal. Add --doctor to review that operation."; return false; }
        if (selected is not null && action is ApplicationAction.UpdateDiscovery or ApplicationAction.Where)
        { reason = "Use updateme with --path to select a copy for update rehearsal, or where with --doctor to inspect a repair."; return false; }
        request = new(application, action, selected, approval, source);
        reason = "";
        return true;
    }
}
