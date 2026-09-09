using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using Dert.Core;

namespace Dert.Notes;

public sealed class GoodbyeAuthoringPanel : StackPanel
{
    private readonly AuthoringSession session;
    private readonly Action<string> notify;
    private readonly Action modeChanged;
    private readonly Func<bool> canBegin;
    private readonly TextBlock meaning = NotesWindow.Label("", Palette.Text);
    private readonly TextBlock diff = NotesWindow.Label("", Palette.Text, 13);
    private readonly Button spelling;
    private readonly Button write;
    private readonly WrapPanel actions = new();
    private bool applying;
    public FlorinEditPlan? CurrentPlan { get; private set; }
    public bool IsReviewing => CurrentPlan is not null;
    public bool ShowTypedSpelling { get; private set; }

    public GoodbyeAuthoringPanel(AuthoringSession session, Action<string> notify, Action modeChanged, Func<bool> canBegin)
    {
        this.session = session; this.notify = notify; this.modeChanged = modeChanged; this.canBegin = canBegin;
        Margin = new(0, 12, 0, 0);
        Children.Add(meaning);
        spelling = NotesWindow.Action("Show typed spelling", ToggleSpelling);
        Children.Add(spelling);
        write = NotesWindow.Action("Write ƒ in source…", Preview);
        Children.Add(write);
        diff.FontFamily = new("Consolas"); diff.Margin = new(0, 6, 8, 12);
        Children.Add(diff);
        actions.Children.Add(NotesWindow.Action("Apply", () => Apply(), true));
        actions.Children.Add(NotesWindow.Action("Leave", Leave));
        Children.Add(actions);
        Refresh();
    }

    public void ToggleSpelling()
    {
        ShowTypedSpelling = !ShowTypedSpelling;
        session.Editor.TextArea.TextView.Redraw();
        Refresh(); modeChanged();
    }

    public void Refresh()
    {
        if (applying) return;
        if (CurrentPlan is { } old && old.Source != session.Editor.Text)
        {
            CurrentPlan = null;
            notify("The source changed. The goodbye preview was withdrawn; review its current spelling before applying.");
        }
        var ending = FlorinGoodbye.EndingAt(session.Parsed, CurrentPlan?.LineNumber ?? session.Editor.TextArea.Caret.Line);
        Visibility = ending is null ? Visibility.Collapsed : Visibility.Visible;
        if (ending is null) return;
        spelling.Content = ShowTypedSpelling ? "Show florin appearance" : "Show typed spelling";
        spelling.Visibility = IsReviewing ? Visibility.Collapsed : Visibility.Visible;
        write.Visibility = !IsReviewing && ending.Spelling == "fin." ? Visibility.Visible : Visibility.Collapsed;
        diff.Visibility = actions.Visibility = IsReviewing ? Visibility.Visible : Visibility.Collapsed;
        if (CurrentPlan is { } plan)
        {
            meaning.Text = $"Line {plan.LineNumber} / chain from line {plan.ChainLine}.\n" + plan.Explanation;
            diff.Inlines.Clear();
            diff.Inlines.Add(new Run($"- {plan.LineNumber} | {plan.BeforeLine}\n") { Foreground = Palette.Copper });
            diff.Inlines.Add(new Run($"+ {plan.LineNumber} | {plan.AfterLine}\n") { Foreground = Palette.Good });
            diff.Inlines.Add(new Run("Preview only / not in your note yet") { Foreground = Palette.Muted });
        }
        else meaning.Text = $"Closes the chain from line {ending.ChainLine}.\n\n" +
            (ending.Spelling == "ƒin." ? "Source contains ƒin. Copy and Save keep that spelling." :
                ShowTypedSpelling ? "Typed spelling / fin.\nFlorin appearance is off for this window. Copy and Save keep fin." :
                "Shown as ƒin. / source is fin.\nCopy and Save keep the typed spelling.");
    }

    public void Preview()
    {
        if (!canBegin()) return;
        var plan = FlorinGoodbye.Plan(session.Parsed, session.Editor.TextArea.Caret.Line);
        if (!plan.IsReady) { notify(plan.Explanation); return; }
        CurrentPlan = plan; Refresh(); modeChanged();
        ((Button)actions.Children[0]).Focus();
    }

    public bool Apply()
    {
        if (CurrentPlan is not { } plan) return false;
        bool applied;
        applying = true;
        try { applied = session.ApplyGoodbyePreview(plan); }
        finally { applying = false; }
        if (!applied) { Refresh(); modeChanged(); return false; }
        CurrentPlan = null; Refresh(); modeChanged(); session.Editor.Focus();
        return true;
    }

    public void Leave()
    {
        Reset(); notify("Goodbye preview left unapplied. Your source spelling is unchanged by that choice."); session.Editor.Focus();
    }

    public void Reset() { CurrentPlan = null; Refresh(); modeChanged(); }
}
