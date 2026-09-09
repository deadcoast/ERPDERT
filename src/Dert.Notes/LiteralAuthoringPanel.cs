using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using Dert.Core;

namespace Dert.Notes;

/// <summary>A selected source value, its quote-only proposal and the verified surrounding reading.</summary>
public sealed class LiteralAuthoringPanel : StackPanel
{
    private readonly AuthoringSession session;
    private readonly Action<string> notify;
    private readonly Action changed;
    private readonly Func<bool> canBegin;
    private readonly TextBlock meaning = NotesWindow.Label("", Palette.Text);
    private readonly TextBlock source = NotesWindow.Label("", Palette.Muted, 15);
    private readonly TextBlock proposed = NotesWindow.Label("", Palette.Text, 15);
    private readonly TextBlock reading = NotesWindow.Label("", Palette.Signal, 13);
    private readonly Button apply;
    private bool reverseSelection;
    public LiteralEditPlan? CurrentPlan { get; private set; }
    public bool IsReviewing => CurrentPlan is not null;
    public bool IsStale => CurrentPlan is { } plan && plan.Source != session.Editor.Text;

    public LiteralAuthoringPanel(AuthoringSession session, Action<string> notify, Action changed, Func<bool> canBegin)
    {
        this.session = session; this.notify = notify; this.changed = changed; this.canBegin = canBegin;
        Children.Add(meaning);
        source.FontFamily = proposed.FontFamily = new("Consolas");
        source.Margin = new(0, 6, 0, 10); proposed.Margin = new(0, 4, 0, 12);
        Children.Add(source); Children.Add(proposed);
        var actions = new WrapPanel();
        apply = NotesWindow.Action("Apply", () => Apply(), true);
        actions.Children.Add(apply);
        actions.Children.Add(NotesWindow.Action("Leave", Leave));
        Children.Add(actions); Children.Add(reading);
        Refresh();
    }

    public void Begin()
    {
        if (IsReviewing) { notify("Apply or leave this literal preview before choosing another selection."); return; }
        if (!canBegin()) return;
        if (session.Editor.TextArea.Selection is ICSharpCode.AvalonEdit.Editing.RectangleSelection)
        { notify("Select one continuous value; a rectangular selection can cover separate fields."); return; }
        var selection = session.Editor.TextArea.Selection;
        reverseSelection = selection.StartPosition.Location.CompareTo(selection.EndPosition.Location) > 0;
        CurrentPlan = LiteralText.Plan(session.Parsed, new(session.Editor.SelectionStart, session.Editor.SelectionLength));
        changed(); Refresh();
        if (apply.IsEnabled) apply.Focus();
    }

    public void Refresh()
    {
        Visibility = IsReviewing ? Visibility.Visible : Visibility.Collapsed;
        if (CurrentPlan is not { } plan) return;
        meaning.Text = IsStale ? "Your source changed. Leave this preview, select the intended words and open Literal text again." : plan.Explanation;
        apply.IsEnabled = plan.IsReady && !IsStale;
        source.Text = plan.Edit is { } edit ? $"AS WRITTEN / line {plan.LineNumber}\n" + edit.Before : "";
        proposed.Inlines.Clear(); reading.Text = "";
        if (plan.Edit is not { } proposedEdit || IsStale) return;
        proposed.Inlines.Add(new Run("WITH QUOTES\n") { Foreground = Palette.Muted, FontSize = 12 });
        AppendQuoted(proposed, proposedEdit.After, plan.InsertedQuotes);
        reading.Text = plan.BeforeReading == plan.AfterReading ? plan.AfterReading : "Before / " + plan.BeforeReading + "\nAfter / " + plan.AfterReading;
    }

    public bool Apply()
    {
        if (CurrentPlan is not { IsReady: true } plan || IsStale || !session.ApplyLiteralPreview(plan, reverseSelection))
        { Refresh(); notify("Review the selected source before applying quote marks."); return false; }
        Reset(); session.Editor.Focus(); session.Editor.ScrollToLine(session.Editor.TextArea.Caret.Line);
        return true;
    }

    public void Leave() { Reset(); notify("Literal preview left unapplied."); session.Editor.Focus(); }
    public void Reset() { CurrentPlan = null; Refresh(); changed(); }

    internal static void AppendQuoted(TextBlock block, string text, IReadOnlyList<int> quotes)
    {
        var start = 0;
        foreach (var index in quotes)
        {
            block.Inlines.Add(new Run(text[start..index]));
            block.Inlines.Add(new Run("\"") { Foreground = Palette.Good, FontWeight = FontWeights.Bold, TextDecorations = TextDecorations.Underline });
            start = index + 1;
        }
        block.Inlines.Add(new Run(text[start..]));
    }
}
