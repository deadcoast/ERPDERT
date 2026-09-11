using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Dert.Cli.Authoring;

namespace Dert.Notes;

public sealed partial class NotesWindow
{
    private DockPanel? operations;
    private TextBox? commandInput;
    private TextBox? commandOutput;
    private TextBlock? commandStatus;
    private Button? runCommand;
    private Button? cancelCommand;
    private CancellationTokenSource? commandCancellation;
    public bool IsOperationRunning { get; private set; }
    private bool sourceOperationRunning;
    public string OperationStatus => commandStatus?.Text ?? "";
    public string OperationOutput => commandOutput?.Text ?? "";
    public void OpenCommandPanel(string? command = null) => OpenOperations(command);

    private readonly string cliPath;
    private string CliPath => cliPath;

    private void OpenOperations(string? command = null)
    {
        if (!IsCompact) { OpenHelp(true); return; }
        if (operations is null)
        {
            operations = new DockPanel();
            commandInput = new TextBox { Background = Palette.Background, Foreground = Palette.Text,
                BorderBrush = Palette.Brush("#4C465B"), BorderThickness = new(1), Padding = new(9, 7, 9, 7), FontSize = 13,
                FontFamily = FontFamily, ToolTip = "DERT arguments. Quoted paths remain literal; this is not a shell." };
            System.Windows.Automation.AutomationProperties.SetName(commandInput, "DERT command");
            var prompt = new DockPanel { Margin = new(0, 0, 0, 7) };
            prompt.Children.Add(new TextBlock { Text = "> ", Foreground = Palette.Signal, FontSize = 16, VerticalAlignment = VerticalAlignment.Center });
            prompt.Children.Add(commandInput); DockPanel.SetDock(prompt, Dock.Top); operations.Children.Add(prompt);
            commandStatus = new TextBlock { Text = "Review the command, then choose Run.", Foreground = Palette.Muted,
                TextWrapping = TextWrapping.Wrap, FontSize = 11, Margin = new(0, 0, 0, 8) };
            DockPanel.SetDock(commandStatus, Dock.Top); operations.Children.Add(commandStatus);
            var controls = new WrapPanel { Margin = new(0, 5, 0, 0) };
            runCommand = Action("Run", async () => await ExecuteDesktopCommand(), small: true);
            cancelCommand = Action("Cancel check", () => commandCancellation?.Cancel(), small: true); cancelCommand.IsEnabled = false;
            controls.Children.Add(runCommand); controls.Children.Add(cancelCommand);
            controls.Children.Add(Action("Copy result", () =>
            {
                try { Clipboard.SetText(commandOutput?.Text ?? ""); }
                catch (System.Runtime.InteropServices.ExternalException error) { ShowOperationNotice("Windows could not copy the result. " + error.Message); }
            }, small: true));
            controls.Children.Add(Action("Help", () => OpenHelp(true), small: true));
            DockPanel.SetDock(controls, Dock.Bottom); operations.Children.Add(controls);
            commandOutput = new TextBox { Background = Palette.Background, Foreground = Palette.Text, FontFamily = FontFamily,
                FontSize = 12, IsReadOnly = true, AcceptsReturn = true, AcceptsTab = true, TextWrapping = TextWrapping.NoWrap,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Padding = new(9), BorderThickness = new(0), SelectionBrush = Palette.Brush("#52416A") };
            System.Windows.Automation.AutomationProperties.SetName(commandOutput, "DERT command result");
            operations.Children.Add(commandOutput);
            commandInput.PreviewKeyDown += async (_, e) =>
            { if (e.Key == Key.Enter && !IsOperationRunning) { e.Handled = true; await ExecuteDesktopCommand(); } };
        }
        if (command is not null && !IsOperationRunning) commandInput!.Text = command;
        ShowWorkingPanel("DERT / operations", operations);
        commandInput!.Focus(); commandInput.CaretIndex = commandInput.Text.Length;
    }

    private void OpenFileOperation(string verb, string? option = null)
    {
        if (opened is null || Session.IsDirty || ContextPending())
        { OpenOperations(); ShowOperationNotice("Save this document and finish any pending edit before running its file operations. Your buffer is still available in the note."); return; }
        OpenOperations(verb + " '" + opened.Path.Replace("'", "''") + "'" + (option is null ? "" : " " + option));
    }

    public async Task ExecuteDesktopCommand()
    {
        if (IsOperationRunning || commandInput is null || commandOutput is null || runCommand is null || cancelCommand is null) return;
        try
        {
            var args = DesktopCommandRunner.ParseArguments(commandInput.Text, CliPath);
            var boundToDocument = opened is not null && DesktopSourceBinding.UsesDocument(args, opened.Path);
            if (boundToDocument && (Session.IsDirty || ContextPending()))
            { ShowOperationNotice("This document has unsaved changes or a pending edit. Save and review it before running the file command."); return; }
            if (boundToDocument && !files.IsCurrent(opened!))
            { ShowOperationNotice("This document changed on disk. Reopen the current file and review its command before running it. Your buffer is retained."); return; }
            if (!File.Exists(CliPath))
            { ShowOperationNotice("The bundled DERT command line is missing. Keep the notes folder beside DERT.exe, or publish both from this workspace."); return; }
            IsOperationRunning = true; runCommand.IsEnabled = false; commandInput.IsReadOnly = true;
            sourceOperationRunning = boundToDocument;
            if (sourceOperationRunning) { Editor.IsReadOnly = true; contextScroll.IsEnabled = false; Session.HoldAutomaticBoundary = true; }
            commandCancellation = new(); cancelCommand.IsEnabled = DesktopCommandRunner.SupportsCancellation(args);
            commandOutput.Clear();
            commandStatus!.Text = cancelCommand.IsEnabled ? "Running the DERT check…" : "Running DERT. This operation will finish its own guarded work; keep this window open.";
            commandStatus.Foreground = Palette.Muted;
            UpdateMinimalStatus();
            var result = await DesktopCommandRunner.RunAsync(CliPath, args, commandCancellation.Token, chunk =>
                Dispatcher.BeginInvoke(() => { commandOutput.AppendText(chunk); commandOutput.ScrollToEnd(); }));
            commandOutput.Text = result.Output;
            commandStatus.Text = result.Cancelled ? "Check cancelled. The output above is incomplete." : $"DERT returned {result.ExitCode}.";
            commandStatus.Foreground = result.ExitCode == 0 && !result.Cancelled ? Palette.Good : Palette.Copper;
            if (sourceOperationRunning && opened is not null && !files.IsCurrent(opened))
            {
                needsAttention = true;
                commandStatus.Text += " The saved document changed. Reopen it to read the result; your previous buffer is retained.";
                commandStatus.Foreground = Palette.Copper;
            }
            notice.Text = commandStatus.Text; notice.Foreground = commandStatus.Foreground; UpdateMinimalStatus();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or System.ComponentModel.Win32Exception)
        { ShowOperationNotice(error.Message); }
        finally
        {
            IsOperationRunning = false; runCommand.IsEnabled = true; commandInput.IsReadOnly = false; cancelCommand.IsEnabled = false;
            sourceOperationRunning = false; Editor.IsReadOnly = false; contextScroll.IsEnabled = true;
            Session.HoldAutomaticBoundary = Result is not null;
            commandCancellation?.Dispose(); commandCancellation = null;
            UpdateMinimalStatus();
        }
    }

    private bool DocumentBusy()
    {
        if (!sourceOperationRunning) return false;
        ShowOperationNotice("DERT is using this saved document. Editing and file changes resume when the command finishes.");
        return true;
    }

    private void ShowOperationNotice(string message)
    {
        if (commandStatus is not null) { commandStatus.Text = message; commandStatus.Foreground = Palette.Copper; }
        notice.Text = message; notice.Foreground = Palette.Copper;
    }
}
