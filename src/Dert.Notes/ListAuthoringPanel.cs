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
    private readonly Button literal;
    private readonly Button asSource;
    private readonly TextBlock punctuation = NotesWindow.Label("", Palette.Signal, 13);
    private readonly Func<bool> canBegin;
    private bool literalWords;
    public LiteralQuote? LiteralPreview { get; private set; }
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
        TextWrapping = TextWrapping.Wrap, SelectionBrush = Palette.Brush("#39334F")
    };
    public ListEditPlan? CurrentPlan { get; private set; }
    public bool IsReviewing => action is not null;
    public bool HasDraft => action == ListAction.Add && ItemEditor.Text.Length > 0;
    public bool IsStale => IsReviewing && revision != session.Editor.Text;

    public ListAuthoringPanel(AuthoringSession session, Action<string> notify, Action modeChanged, Func<bool>? canBegin = null)
    {
        this.session = session; this.notify = notify; this.modeChanged = modeChanged;
        this.canBegin = canBegin ?? (() => true);
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
        literal = NotesWindow.Action("Keep punctuation as text", KeepPunctuationAsText, small: true);
        asSource = NotesWindow.Action("Use DERT source", UseDertSource, small: true);
        proposal.Children.Add(literal); proposal.Children.Add(asSource);
        diff.FontFamily = new("Consolas"); diff.Margin = new(0, 4, 8, 12);
        proposal.Children.Add(diff);
        var actions = new WrapPanel();
        apply = NotesWindow.Action("Apply", () => Apply(), true);
        refresh = NotesWindow.Action("Review again", ReviewAgain);
        actions.Children.Add(apply); actions.Children.Add(refresh);
        actions.Children.Add(NotesWindow.Action("Leave", Leave));
        proposal.Children.Add(actions);
        proposal.Children.Add(punctuation);
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
    public void KeepPunctuationAsText() { if (action != ListAction.Add) return; literalWords = true; RenderProposal(); }
    public void UseDertSource() { literalWords = false; RenderProposal(); }

    private void Begin(ListAction requested)
    {
        if (!canBegin()) return;
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
        owner.Text = (action == ListAction.Add ? literalWords ? "New literal item" : "New item" : "Finish here") + $" / list on line {statementLine}";
        ItemEditor.ToolTip = literalWords ? "Every character belongs to this item, including //. Only the preview adds quote marks."
            : "One item, with an optional // annotation. These words enter your note only after Apply.";
        System.Windows.Automation.AutomationProperties.SetName(ItemEditor, literalWords ? "Literal words for new list item, preview only" : "New list item, preview only");
        LiteralPreview = action == ListAction.Add && literalWords ? LiteralText.Protect(ItemEditor.Text) : null;
        CurrentPlan = IsStale ? null : LiteralPreview is not null
            ? ListCompletion.PlanLiteral(session.Parsed, statementLine, ItemEditor.Text)
            : ListCompletion.Plan(session.Parsed, statementLine, action!.Value, ItemEditor.Text);
        literal.Visibility = action == ListAction.Add && !literalWords ? Visibility.Visible : Visibility.Collapsed;
        asSource.Visibility = action == ListAction.Add && literalWords ? Visibility.Visible : Visibility.Collapsed;
        meaning.Text = IsStale ? "The source changed. Select its list and review again; your proposed words are retained." : CurrentPlan!.Explanation;
        refresh.Visibility = IsStale ? Visibility.Visible : Visibility.Collapsed;
        apply.IsEnabled = CurrentPlan?.IsReady == true;
        diff.Inlines.Clear();
        punctuation.Text = "";
        if (CurrentPlan is { IsReady: true } plan)
        {
            var firstChangedLine = plan.Action == ListAction.Add && plan.AfterLines[0] == plan.BeforeLine ? 1 : 0;
            if (firstChangedLine == 0)
                diff.Inlines.Add(new Run($"- {plan.LastLine} | {plan.BeforeLine}\n") { Foreground = Palette.Copper });
            for (var index = firstChangedLine; index < plan.AfterLines.Count; index++)
            {
                diff.Inlines.Add(new Run($"+ {plan.LastLine + index} | ") { Foreground = Palette.Good });
                if (index == 1 && LiteralPreview is { } quoted) LiteralAuthoringPanel.AppendQuoted(diff, quoted.Text, quoted.InsertedQuotes);
                else diff.Inlines.Add(new Run(plan.AfterLines[index]) { Foreground = Palette.Good });
                diff.Inlines.Add(new Run("\n"));
            }
            diff.Inlines.Add(new Run("Preview only / not in your note yet") { Foreground = Palette.Muted });
            if (LiteralPreview is { } literalPlan)
                punctuation.Text = literalPlan.Explanation + "\n" + DertPunctuation.Reading(new(DertParser.Parse(plan.ProposedText)), plan.LastLine + 1);
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
        action = null; revision = null; CurrentPlan = null; literalWords = false; LiteralPreview = null; ItemEditor.Clear();
        Refresh(); modeChanged();
    }
}
