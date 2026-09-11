using Dert.Core;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Folding;

namespace Dert.Notes;

public sealed class AuthoringSession
{
    public TextEditor Editor { get; }
    public DertDocument Parsed { get; private set; } = DertParser.Parse("");
    public FoldingManager Folds { get; }
    public ReasoningInspection Reasoning { get; }
    public event Action? Changed;
    public event Action? TitleAligned;
    public event Action<string>? TitleAlignmentUnavailable;
    public event Action<string>? ListEdited;
    public event Action<string>? GoodbyeEdited;
    public event Action<string>? LiteralEdited;
    private bool applying;
    private bool autoTitle;
    private bool titleChanged;
    private bool boundaryQueued;
    private int boundaryEpoch;
    private bool holdAutomaticBoundary;
    internal bool HoldAutomaticBoundary
    {
        get => holdAutomaticBoundary;
        set { if (holdAutomaticBoundary == value) return; holdAutomaticBoundary = value; boundaryEpoch++; }
    }
    private string savedText = "";
    public bool IsDirty => Editor.Text != savedText;
    public string NewLine { get; private set; } = "\r\n";

    public AuthoringSession(TextEditor editor)
    {
        Editor = editor;
        Folds = FoldingManager.Install(editor.TextArea);
        Reasoning = new(this, () => Changed?.Invoke());
        editor.TextArea.SelectionChanged += (_, _) => { if (!applying) Reasoning.QueueNavigation(); };
        editor.TextChanged += (_, _) => Refresh();
        editor.TextArea.Caret.PositionChanged += (_, _) =>
        {
            if (!applying) Reasoning.QueueNavigation();
            if (!applying && titleChanged && !boundaryQueued)
            {
                boundaryQueued = true;
                var epoch = boundaryEpoch;
                Editor.Dispatcher.BeginInvoke(new Action(() =>
                {
                    boundaryQueued = false;
                    if (epoch != boundaryEpoch) return;
                    if (titleChanged && Editor.TextArea.Caret.Line != TitleLine()) CompleteBoundary();
                }), System.Windows.Threading.DispatcherPriority.Background);
            }
            Changed?.Invoke();
        };
        Load("", true);
    }

    public void Load(string text, bool newDocument = false)
    {
        Reasoning.Reset();
        applying = true;
        try
        {
            foreach (var fold in Folds.AllFoldings.ToArray()) Folds.RemoveFolding(fold);
            Editor.Text = text;
            Editor.Document.UndoStack.ClearAll();
            Editor.CaretOffset = 0;
            savedText = text;
            NewLine = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : text.Contains('\n') ? "\n" : text.Contains('\r') ? "\r" : "\r\n";
            autoTitle = newDocument || TitleRuleRepair.Plan(DertParser.Parse(text)).State == TitleRepairState.Aligned;
            titleChanged = false;
        }
        finally { applying = false; }
        Refresh();
        titleChanged = false;
    }

    private int TitleLine() => Parsed.Nodes.OfType<NoteNode>().FirstOrDefault(note => note.IsTitle)?.LineNumber ?? 0;

    private void Refresh()
    {
        var previous = Parsed;
        Parsed = DertParser.Parse(Editor.Text);
        if (!applying)
        {
            var oldTitle = previous.Nodes.OfType<NoteNode>().FirstOrDefault(note => note.IsTitle)?.Content.Text;
            var newTitle = Parsed.Nodes.OfType<NoteNode>().FirstOrDefault(note => note.IsTitle)?.Content.Text;
            if (oldTitle != newTitle) titleChanged = true;
            if (oldTitle == newTitle && previous.Nodes.OfType<RuleNode>().Any() && !Parsed.Nodes.OfType<RuleNode>().Any())
            { autoTitle = false; titleChanged = false; }
        }
        var foldings = new List<NewFolding>();
        foreach (var location in new DertStructureIndex(Parsed).Locations)
        {
            var (start, end, label) = location.Node switch
            {
                ReasoningNoteNode note when note.Header.Payload is { Text.Length: > 0 } payload =>
                    (payload.Span.Start, payload.Span.End, "reason" + (note.Closing is null ? " / missing //" : "")),
                ReasoningBlockNode block => (Parsed.Source.Lines[block.LineNumber - 1].ContentEnd,
                    block.Closing?.End ?? block.Span.End, $"  {block.Entries.Count} entries" + (block.Closing is null ? " / missing //" : "  //")),
                _ => (0, 0, "")
            };
            if (end > start) foldings.Add(new(start, end) { Name = label, DefaultClosed = true });
        }
        var previousFolds = Folds.AllFoldings.ToHashSet();
        Folds.UpdateFoldings(foldings.OrderBy(folding => folding.StartOffset), -1);
        foreach (var fold in Folds.AllFoldings)
            if (!previousFolds.Contains(fold)) fold.IsFolded = true;
        // A buffer being authored stays open at its caret. Other new reasoning
        // folds receive their resting presentation without altering the text.
        foreach (var fold in Folds.GetFoldingsContaining(Editor.CaretOffset))
            if (Editor.IsKeyboardFocusWithin) fold.IsFolded = false;
        Reasoning.RefreshSource();
        Editor.TextArea.TextView.Redraw();
        Changed?.Invoke();
    }

    public bool CompleteTitleLine()
    {
        if (applying || !autoTitle || Editor.SelectionLength > 0) return false;
        var titleLine = TitleLine();
        if (titleLine == 0 || Editor.CaretOffset != Parsed.Source.Lines[titleLine - 1].ContentEnd) return false;
        var existingRule = Parsed.Nodes.OfType<RuleNode>().Any(rule => rule.LineNumber == titleLine + 1);
        if (!titleChanged && !existingRule) return false;
        applying = true;
        try
        {
            using (Editor.Document.RunUpdate())
            {
                if (existingRule)
                {
                    // Enter advances past the owned rule. Inserting a line above
                    // it would detach the original and invite a second rule.
                    ApplyTitle(TitleRuleRepair.Plan(DertParser.Parse(Editor.Text)));
                    var rule = Editor.Document.GetLineByNumber(titleLine + 1);
                    var after = rule.Offset + rule.TotalLength;
                    if (rule.DelimiterLength == 0) { Editor.Document.Insert(after, NewLine); after += NewLine.Length; }
                    Editor.CaretOffset = after;
                }
                else
                {
                    var offset = Editor.CaretOffset;
                    Editor.Document.Insert(offset, NewLine);
                    Editor.CaretOffset = offset + NewLine.Length;
                    ApplyTitle(TitleRuleRepair.Plan(DertParser.Parse(Editor.Text)));
                }
            }
            titleChanged = false;
        }
        finally { applying = false; }
        Refresh();
        return true;
    }

    public void CompleteBoundary()
    {
        if (applying || HoldAutomaticBoundary || !autoTitle || !titleChanged) return;
        applying = true;
        try { using (Editor.Document.RunUpdate()) ApplyTitle(TitleRuleRepair.Plan(DertParser.Parse(Editor.Text))); titleChanged = false; }
        finally { applying = false; }
        Refresh();
    }

    public bool ApplyPreview(TitleRepairPlan preview)
    {
        if (!preview.IsReady || preview.Source != Editor.Text) return false;
        applying = true;
        try { using (Editor.Document.RunUpdate()) ApplyTitle(preview); autoTitle = true; titleChanged = false; }
        finally { applying = false; }
        Refresh();
        return true;
    }

    public bool ApplyListPreview(ListEditPlan preview)
    {
        if (applying || !preview.IsReady || preview.Source != Editor.Text) return false;
        var caret = Editor.CaretOffset;
        var start = Editor.SelectionStart;
        var end = start + Editor.SelectionLength;
        applying = true;
        try
        {
            using (Editor.Document.RunUpdate())
                foreach (var edit in preview.Edits.Reverse())
                {
                    Editor.Document.Replace(edit.Span.Start, edit.Span.Length, edit.After);
                    caret = Map(caret, edit); start = Map(start, edit); end = Map(end, edit);
                }
            if (preview.NewItemCaret is int added) Editor.Select(added, 0);
            else if (end > start) Editor.Select(start, end - start);
            else Editor.CaretOffset = caret;
        }
        finally { applying = false; }
        Refresh();
        ListEdited?.Invoke(preview.Action == ListAction.Add ? "Item added to your note. Undo is available; Save keeps it on disk." : "List finished in your note. Undo is available; Save keeps it on disk.");
        return true;

        static int Map(int offset, SourceEdit edit) => offset <= edit.Span.Start ? offset : offset >= edit.Span.End
            ? offset + edit.After.Length - edit.Span.Length : edit.Span.Start;
    }

    public bool ApplyGoodbyePreview(FlorinEditPlan preview)
    {
        if (applying || preview.Source != Editor.Text || preview.Edit is not { } edit) return false;
        var caret = Editor.CaretOffset;
        var selection = Editor.TextArea.Selection;
        applying = true;
        try
        {
            using (Editor.Document.RunUpdate()) Editor.Document.Replace(edit.Span.Start, edit.Span.Length, edit.After);
            // f and ƒ are each one UTF-16 code unit. Native source positions stay exact.
            Editor.TextArea.Selection = selection;
            Editor.CaretOffset = caret;
        }
        finally { applying = false; }
        Refresh();
        GoodbyeEdited?.Invoke("ƒ is now in your source. Undo restores f; Save keeps this spelling on disk.");
        return true;
    }

    public bool ApplyLiteralPreview(LiteralEditPlan preview, bool reverseSelection = false)
    {
        if (applying || preview.Source != Editor.Text || preview.Edit is not { } edit) return false;
        applying = true;
        try
        {
            using (Editor.Document.RunUpdate()) Editor.Document.Replace(edit.Span.Start, edit.Span.Length, edit.After);
            Editor.Select(edit.Span.Start, edit.After.Length);
            if (reverseSelection)
            {
                Editor.CaretOffset = edit.Span.Start;
                Editor.TextArea.Selection = ICSharpCode.AvalonEdit.Editing.Selection.Create(Editor.TextArea, edit.Span.Start + edit.After.Length, edit.Span.Start);
            }
        }
        finally { applying = false; }
        Refresh();
        LiteralEdited?.Invoke("Quote marks added to your source. Undo restores the original source; Save keeps this spelling on disk.");
        return true;
    }

    public bool ApplyResultPreview(ResultNotePlan preview)
    {
        if (applying || preview.Source != Editor.Text || preview.Edit is not { } edit) return false;
        applying = true;
        try
        {
            using (Editor.Document.RunUpdate()) Editor.Document.Replace(edit.Span.Start, edit.Span.Length, edit.After);
            Reasoning.Reset();
            Editor.Select(preview.AddedSpan.Start, preview.AddedSpan.Length);
            Editor.CaretOffset = preview.AddedSpan.End;
        }
        finally { applying = false; }
        Refresh();
        foreach (var fold in Folds.AllFoldings.Where(fold => fold.StartOffset < preview.AddedSpan.End && fold.EndOffset > preview.AddedSpan.Start))
            fold.IsFolded = false;
        Editor.ScrollToLine(Editor.Document.GetLineByOffset(preview.AddedSpan.Start).LineNumber);
        return true;
    }

    private void ApplyTitle(TitleRepairPlan plan)
    {
        if (plan.State == TitleRepairState.Unavailable) TitleAlignmentUnavailable?.Invoke(plan.Explanation);
        if (plan.Edit is not { } edit) return;
        var caret = Editor.CaretOffset;
        var start = Editor.SelectionStart;
        var end = start + Editor.SelectionLength;
        Editor.Document.Replace(edit.Span.Start, edit.Span.Length, edit.After);
        if (end > start) Editor.Select(Map(start), Map(end) - Map(start));
        else Editor.CaretOffset = Map(caret);
        TitleAligned?.Invoke();

        int Map(int offset) => offset < edit.Span.Start ? offset : offset >= edit.Span.End
            ? offset + edit.After.Length - edit.Span.Length : edit.Span.Start + Math.Min(offset - edit.Span.Start, edit.After.Length);
    }

    public void Undo()
    {
        applying = true;
        try
        {
            Editor.Undo();
            if (TitleRuleRepair.Plan(DertParser.Parse(Editor.Text)).State == TitleRepairState.Aligned) autoTitle = true;
            titleChanged = false;
        }
        finally { applying = false; }
        Refresh();
    }

    public void Redo()
    {
        applying = true;
        try { Editor.Redo(); titleChanged = false; }
        finally { applying = false; }
        Refresh();
    }

    public void MarkSaved() { savedText = Editor.Text; Editor.Document.UndoStack.MarkAsOriginalFile(); Changed?.Invoke(); }
    public void ToggleReasoning()
    {
        var line = Editor.Document.GetLineByOffset(Editor.CaretOffset);
        var fold = Folds.AllFoldings.FirstOrDefault(folding => folding.StartOffset >= line.Offset && folding.StartOffset <= line.EndOffset)
            ?? Folds.GetFoldingsContaining(Editor.CaretOffset).FirstOrDefault();
        if (fold is not null) { fold.IsFolded = !fold.IsFolded; Changed?.Invoke(); }
    }
}
