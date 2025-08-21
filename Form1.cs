using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace AlwaysOnTopApp
{
    public partial class Form1 : Form, IDisposable
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint TOPMOST_FLAGS = SWP_NOMOVE | SWP_NOSIZE;

        private ComboBox processComboBox = null!;
        private Button toggleButton = null!;
        private Button refreshButton = null!;
        private Label statusLabel = null!;
        private System.Windows.Forms.Timer refreshTimer = null!;
        private readonly Dictionary<IntPtr, bool> topMostStates = new Dictionary<IntPtr, bool>();

        public Form1()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "Always on Top Manager";
            this.Size = new Size(450, 250);
            this.MinimumSize = new Size(400, 200);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.Sizable;

            var tableLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 4,
                Padding = new Padding(10)
            };

            tableLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70));
            tableLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 15));
            tableLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 15));

            var processLabel = new Label
            {
                Text = "Select Process:",
                Anchor = AnchorStyles.Left | AnchorStyles.Bottom,
                AutoSize = true
            };

            processComboBox = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                Margin = new Padding(0, 5, 5, 5)
            };

            refreshButton = new Button
            {
                Text = "Refresh",
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                Margin = new Padding(0, 5, 5, 5)
            };

            toggleButton = new Button
            {
                Text = "Set Always On Top",
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                Margin = new Padding(0, 5, 0, 5),
                Enabled = false
            };

            statusLabel = new Label
            {
                Text = "Ready",
                Anchor = AnchorStyles.Left | AnchorStyles.Top,
                AutoSize = true,
                ForeColor = Color.DarkGreen
            };

            tableLayout.Controls.Add(processLabel, 0, 0);
            tableLayout.Controls.Add(processComboBox, 0, 1);
            tableLayout.Controls.Add(refreshButton, 1, 1);
            tableLayout.Controls.Add(toggleButton, 2, 1);
            tableLayout.Controls.Add(statusLabel, 0, 2);
            tableLayout.SetColumnSpan(statusLabel, 3);

            this.Controls.Add(tableLayout);

            processComboBox.SelectedIndexChanged += ProcessComboBox_SelectedIndexChanged;
            toggleButton.Click += ToggleButton_Click;
            refreshButton.Click += RefreshButton_Click;

            refreshTimer = new System.Windows.Forms.Timer
            {
                Interval = 5000,
                Enabled = true
            };
            refreshTimer.Tick += RefreshTimer_Tick;

            LoadVisibleProcesses();
        }

        private void LoadVisibleProcesses()
        {
            try
            {
                var selectedProcess = processComboBox.SelectedItem as ProcessItem;
                processComboBox.Items.Clear();

                var processes = Process.GetProcesses()
                    .Where(p => p.MainWindowHandle != IntPtr.Zero && 
                               IsWindowVisible(p.MainWindowHandle) && 
                               !string.IsNullOrEmpty(p.MainWindowTitle))
                    .OrderBy(p => p.ProcessName)
                    .ToList();

                foreach (var process in processes)
                {
                    try
                    {
                        var processItem = new ProcessItem(process);
                        processComboBox.Items.Add(processItem);

                        if (selectedProcess?.Process?.Id == process.Id)
                        {
                            processComboBox.SelectedItem = processItem;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error processing {process.ProcessName}: {ex.Message}");
                    }
                }

                UpdateStatus($"Found {processComboBox.Items.Count} visible windows");
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error loading processes: {ex.Message}", true);
            }
        }

        private void ToggleButton_Click(object? sender, EventArgs e)
        {
            if (!(processComboBox.SelectedItem is ProcessItem selectedProcess))
            {
                UpdateStatus("Please select a process first", true);
                return;
            }

            try
            {
                var process = selectedProcess.Process;
                if (process.HasExited)
                {
                    UpdateStatus("Selected process has exited", true);
                    LoadVisibleProcesses();
                    return;
                }

                IntPtr hWnd = process.MainWindowHandle;
                if (hWnd == IntPtr.Zero)
                {
                    UpdateStatus("No valid window handle found", true);
                    return;
                }

                bool isCurrentlyTopMost = topMostStates.ContainsKey(hWnd) && topMostStates[hWnd];
                bool success;

                if (isCurrentlyTopMost)
                {
                    success = SetWindowPos(hWnd, HWND_NOTOPMOST, 0, 0, 0, 0, TOPMOST_FLAGS);
                    if (success)
                    {
                        topMostStates[hWnd] = false;
                        toggleButton.Text = "Set Always On Top";
                        UpdateStatus($"Removed always on top for {process.ProcessName}");
                    }
                }
                else
                {
                    success = SetWindowPos(hWnd, HWND_TOPMOST, 0, 0, 0, 0, TOPMOST_FLAGS);
                    if (success)
                    {
                        topMostStates[hWnd] = true;
                        toggleButton.Text = "Remove Always On Top";
                        UpdateStatus($"Set always on top for {process.ProcessName}");
                    }
                }

                if (!success)
                {
                    int error = Marshal.GetLastWin32Error();
                    UpdateStatus($"Failed to modify window: Error {error}", true);
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error: {ex.Message}", true);
            }
        }

        private void ProcessComboBox_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (processComboBox.SelectedItem is ProcessItem selectedProcess)
            {
                try
                {
                    var process = selectedProcess.Process;
                    if (!process.HasExited)
                    {
                        IntPtr hWnd = process.MainWindowHandle;
                        bool isTopMost = topMostStates.ContainsKey(hWnd) && topMostStates[hWnd];
                        
                        toggleButton.Text = isTopMost ? "Remove Always On Top" : "Set Always On Top";
                        toggleButton.Enabled = true;
                        
                        UpdateStatus($"Selected: {process.ProcessName}");
                    }
                    else
                    {
                        toggleButton.Enabled = false;
                        UpdateStatus("Selected process has exited", true);
                    }
                }
                catch (Exception ex)
                {
                    toggleButton.Enabled = false;
                    UpdateStatus($"Error accessing process: {ex.Message}", true);
                }
            }
            else
            {
                toggleButton.Enabled = false;
                toggleButton.Text = "Set Always On Top";
                UpdateStatus("No process selected");
            }
        }

        private void RefreshButton_Click(object? sender, EventArgs e)
        {
            LoadVisibleProcesses();
        }

        private void RefreshTimer_Tick(object? sender, EventArgs e)
        {
            LoadVisibleProcesses();
        }

        private void UpdateStatus(string message, bool isError = false)
        {
            if (statusLabel != null)
            {
                statusLabel.Text = message;
                statusLabel.ForeColor = isError ? Color.Red : Color.DarkGreen;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                refreshTimer?.Stop();
                refreshTimer?.Dispose();
                
                topMostStates.Clear();
            }
            base.Dispose(disposing);
        }

        private class ProcessItem : IDisposable
        {
            public Process Process { get; }
            private bool disposed = false;

            public ProcessItem(Process process)
            {
                Process = process ?? throw new ArgumentNullException(nameof(process));
            }

            public override string ToString()
            {
                try
                {
                    if (Process.HasExited)
                        return $"{Process.ProcessName} (Exited)";
                    
                    return string.IsNullOrEmpty(Process.MainWindowTitle) 
                        ? Process.ProcessName 
                        : $"{Process.ProcessName} - {Process.MainWindowTitle}";
                }
                catch
                {
                    return Process.ProcessName ?? "Unknown Process";
                }
            }

            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            protected virtual void Dispose(bool disposing)
            {
                if (!disposed && disposing)
                {
                    Process?.Dispose();
                    disposed = true;
                }
            }
        }
    }
}
