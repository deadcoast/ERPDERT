using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using Dert.Core;

namespace Dert.Notes;

/// <summary>One explicit source-bound proposal, separate from the saved buffer.</summary>
public sealed class ListAuthoringPanel : StackPanel
{
    private readonly AuthoringSession session;
    private readonly Action<string> notify;
    private readonly Action modeChanged;
    private readonly TextBlock meaning = NotesWindow.Label("", Palette.Text);
    private readonly StackPanel choices = new();
    private readonly StackPanel proposal = new();
    private readonly TextBlock owner = NotesWindow.Label("", Palette.Signal);
    private readonly TextBlock diff = NotesWindow.Label("", Palette.Text, 13);
    private readonly Button finish;
    private readonly Button apply;
    private readonly Button refresh;
    private string? revision;
    private int statementLine;
    public string OwnerName { get; private set; } = "";
    private ListAction? action;
    private bool applying;
    public TextBox ItemEditor { get; } = new()
    {
        Foreground = Palette.Text, Background = Palette.Panel, BorderBrush = Palette.Signal,
        BorderThickness = new(1), FontFamily = new("Consolas"), FontSize = 15,
        Padding = new(8), Margin = new(0, 4, 8, 10), AcceptsReturn = false,
        TextWrapping = TextWrapping.Wrap, SelectionBrush = Palette.Brush("#31504E")
    };
    public ListEditPlan? CurrentPlan { get; private set; }
    public bool IsReviewing => action is not null;
    public bool HasDraft => action == ListAction.Add && ItemEditor.Text.Length > 0;
    public bool IsStale => IsReviewing && revision != session.Editor.Text;

    public ListAuthoringPanel(AuthoringSession session, Action<string> notify, Action modeChanged)
    {
        this.session = session; this.notify = notify; this.modeChanged = modeChanged;
        Children.Add(meaning);
        choices.Children.Add(NotesWindow.Action("Add another item…", BeginAdd));
        finish = NotesWindow.Action("Finish this list…", PreviewFinish);
        choices.Children.Add(finish);
        choices.Children.Add(NotesWindow.Label("Ctrl+Enter add / Ctrl+Shift+Enter finish", Palette.Muted, 12));
        Children.Add(choices);
        proposal.Children.Add(owner);
        System.Windows.Automation.AutomationProperties.SetName(ItemEditor, "New list item, preview only");
        ItemEditor.ToolTip = "One item, with an optional // annotation. These words enter your note only after Apply.";
        ItemEditor.TextChanged += (_, _) => RenderProposal();
        proposal.Children.Add(ItemEditor);
        diff.FontFamily = new("Consolas"); diff.Margin = new(0, 4, 8, 12);
        proposal.Children.Add(diff);
        var actions = new WrapPanel();
        apply = NotesWindow.Action("Apply", () => Apply(), true);
        refresh = NotesWindow.Action("Review again", ReviewAgain);
        actions.Children.Add(apply); actions.Children.Add(refresh);
        actions.Children.Add(NotesWindow.Action("Leave", Leave));
        proposal.Children.Add(actions);
        Children.Add(proposal);
        Refresh();
    }

    public void Refresh()
    {
        if (applying) return;
        if (IsReviewing) { Visibility = Visibility.Visible; RenderProposal(); return; }
        var statement = ListCompletion.StatementAt(session.Parsed, session.Editor.TextArea.Caret.Line);
        Visibility = statement is null ? Visibility.Collapsed : Visibility.Visible;
        proposal.Visibility = Visibility.Collapsed; choices.Visibility = Visibility.Visible;
        if (statement is null) return;
        var comma = statement.Values[^1].HasComma;
        meaning.Text = $"{statement.Values.Count} {(statement.Values.Count == 1 ? "item" : "items")} / list on line {statement.LineNumber}. " +
            (comma ? "The last comma leaves room for another item." : "The last item ends this list.");
        finish.Visibility = comma ? Visibility.Visible : Visibility.Collapsed;
    }

    public void BeginAdd() => Begin(ListAction.Add);
    public void PreviewFinish() => Begin(ListAction.Finish);

    private void Begin(ListAction requested)
    {
        if (HasDraft) { notify("Your new item is still a proposal. Apply it or choose Leave before opening another choice."); return; }
        var statement = ListCompletion.StatementAt(session.Parsed, session.Editor.TextArea.Caret.Line);
        if (statement is null) { notify("Select an expanded list's header or active item to open its choices."); return; }
        action = requested; statementLine = statement.LineNumber; revision = session.Editor.Text;
        OwnerName = statement.Header.Head.Text;
        CurrentPlan = null;
        Refresh(); modeChanged();
        if (requested == ListAction.Add) ItemEditor.Focus();
        else apply.Focus();
    }

    private void RenderProposal()
    {
        if (!IsReviewing || applying || apply is null) return;
        choices.Visibility = Visibility.Collapsed; proposal.Visibility = Visibility.Visible;
        ItemEditor.Visibility = action == ListAction.Add ? Visibility.Visible : Visibility.Collapsed;
        owner.Text = (action == ListAction.Add ? "New item" : "Finish here") + $" / list on line {statementLine}";
        CurrentPlan = IsStale ? null : ListCompletion.Plan(session.Parsed, statementLine, action!.Value, ItemEditor.Text);
        meaning.Text = IsStale ? "The source changed. Select its list and review again; your proposed words are retained." : CurrentPlan!.Explanation;
        refresh.Visibility = IsStale ? Visibility.Visible : Visibility.Collapsed;
        apply.IsEnabled = CurrentPlan?.IsReady == true;
        diff.Inlines.Clear();
        if (CurrentPlan is { IsReady: true } plan)
        {
            diff.Inlines.Add(new Run($"- {plan.LastLine} | {plan.BeforeLine}\n") { Foreground = Palette.Copper });
            for (var index = 0; index < plan.AfterLines.Count; index++)
                diff.Inlines.Add(new Run($"+ {plan.LastLine + index} | {plan.AfterLines[index]}\n") { Foreground = Palette.Good });
            diff.Inlines.Add(new Run("Preview only / not in your note yet") { Foreground = Palette.Muted });
        }
    }

    public void ReviewAgain()
    {
        var statement = ListCompletion.StatementAt(session.Parsed, session.Editor.TextArea.Caret.Line);
        if (statement is null) { notify("Select the intended list in your source, then choose Review again."); return; }
        statementLine = statement.LineNumber; revision = session.Editor.Text;
        OwnerName = statement.Header.Head.Text;
        Refresh(); modeChanged();
    }

    public bool Apply()
    {
        if (CurrentPlan is not { IsReady: true } plan || IsStale)
        { Refresh(); notify("Review the current list and its proposed words before applying."); return false; }
        applying = true;
        bool applied;
        try { applied = session.ApplyListPreview(plan); }
        finally { applying = false; }
        if (!applied) { Refresh(); return false; }
        Reset(); session.Editor.Focus(); session.Editor.ScrollToLine(session.Editor.TextArea.Caret.Line);
        return true;
    }

    public void Leave()
    {
        Reset(); notify("List proposal left unapplied. Your note is unchanged by that choice."); session.Editor.Focus();
    }

    public void Reset()
    {
        action = null; revision = null; CurrentPlan = null; ItemEditor.Clear();
        Refresh(); modeChanged();
    }
}
