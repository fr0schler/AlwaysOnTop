using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace AlwaysOnTopApp
{
    public class Form1 : Form
    {
        // Windows-API-Funktionen
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        // Konstanten für "Always on Top"
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint TOPMOST_FLAGS = SWP_NOMOVE | SWP_NOSIZE;

        private ComboBox processComboBox;
        private Button toggleButton;

        public Form1()
        {
            // Initialisierung der Benutzeroberfläche
            this.Text = "Always on Top App";
            this.Width = 350;
            this.Height = 200;

            // ComboBox für die Auswahl des Prozesses
            processComboBox = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 300,
                Top = 10,
                Left = 10
            };
            LoadVisibleProcesses();

            // Button für Always-on-Top-Option
            toggleButton = new Button
            {
                Text = "AlwaysOnTop",
                AutoSize = true,
                Top = 50,
                Left = 10
            };
            toggleButton.Click += ToggleButton_Click;

            Controls.Add(processComboBox);
            Controls.Add(toggleButton);
        }

        private void LoadVisibleProcesses()
        {
            processComboBox.Items.Clear();
            foreach (Process process in Process.GetProcesses())
            {
                if (process.MainWindowHandle != IntPtr.Zero && IsWindowVisible(process.MainWindowHandle))
                {
                    processComboBox.Items.Add(new ProcessItem { Process = process });
                }
            }
        }

        private void ToggleButton_Click(object sender, EventArgs e)
        {
            if (processComboBox.SelectedItem is ProcessItem selectedProcess)
            {
                IntPtr hWnd = selectedProcess.Process.MainWindowHandle;
                if (hWnd != IntPtr.Zero)
                {
                    SetWindowPos(hWnd, HWND_TOPMOST, 0, 0, 0, 0, TOPMOST_FLAGS);
                    MessageBox.Show($"Fenster von '{selectedProcess.Process.ProcessName}' wurde auf 'Always on Top' gesetzt.");
                }
                else
                {
                    MessageBox.Show("Kein gültiges Fenster gefunden.");
                }
            }
            else
            {
                MessageBox.Show("Bitte wählen Sie einen Prozess aus.");
            }
        }

        // Hilfsklasse für die ComboBox zur Anzeige von Prozessnamen
        private class ProcessItem
        {
            public Process Process { get; set; }
            public override string ToString()
            {
                return $"{Process.ProcessName} - {Process.MainWindowTitle}";
            }
        }
    }
}
