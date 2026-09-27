using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace XBatteryStatus
{
    public class OnboardingForm : Form
    {
        public OnboardingForm(bool lightMode, bool isStoreInstall, IEnumerable<(string Title, string Description)> settingTips)
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            ClientSize = new Size(420, 1); // height is recalculated below once content is laid out
            KeyPreview = true;

            Color backColor = lightMode ? Color.White : Color.FromArgb(32, 32, 32);
            Color foreColor = lightMode ? Color.FromArgb(20, 20, 20) : Color.White;
            Color subtleColor = lightMode ? Color.FromArgb(90, 90, 90) : Color.FromArgb(190, 190, 190);
            Color borderColor = lightMode ? Color.FromArgb(210, 210, 210) : Color.FromArgb(60, 60, 60);

            BackColor = backColor;
            ForeColor = foreColor;
            Padding = new Padding(1); // room for the border drawn in OnPaint

            Paint += (s, e) =>
            {
                using (var pen = new Pen(borderColor))
                {
                    e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
                }
            };

            FlowLayoutPanel content = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = false,
                Padding = new Padding(16, 14, 16, 6),
                BackColor = backColor
            };

            int textWidth = ClientSize.Width - 16 - 16; // minus the content panel's left/right padding

            Label header = MakeLabel("XBatteryStatus", foreColor, new Font("Segoe UI", 13f, FontStyle.Bold), textWidth, new Padding(0, 0, 0, 8));
            content.Controls.Add(header);

            content.Controls.Add(MakeLabel(
                "XBatteryStatus now runs quietly in your system tray and shows your controller's battery level there.",
                foreColor, new Font("Segoe UI", 9f), textWidth, new Padding(0, 0, 0, 10)));

            content.Controls.Add(MakeLabel(
                "Can't see the icon? Windows sometimes hides new tray icons in the \"^\" overflow area next to the clock. Just drag it out onto the tray.",
                foreColor, new Font("Segoe UI", 9f), textWidth, new Padding(0, 0, 0, 14)));

            content.Controls.Add(MakeLabel("Right-click the icon any time for these options:", foreColor, new Font("Segoe UI", 9f, FontStyle.Bold), textWidth, new Padding(0, 0, 0, 6)));

            foreach ((string title, string description) in settingTips)
            {
                content.Controls.Add(MakeLabel(title, foreColor, new Font("Segoe UI", 9f, FontStyle.Bold), textWidth, new Padding(0, 4, 0, 0)));
                content.Controls.Add(MakeLabel(description, subtleColor, new Font("Segoe UI", 9f), textWidth, new Padding(0, 0, 0, 6)));
            }

            Panel buttonPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 48,
                BackColor = backColor,
                Padding = new Padding(16, 0, 16, 12)
            };

            Button gotItButton = new Button
            {
                Text = "Got it",
                AutoSize = false,
                Size = new Size(90, 30),
                Anchor = AnchorStyles.Right | AnchorStyles.Bottom,
                FlatStyle = FlatStyle.System
            };
            gotItButton.Left = buttonPanel.Width - gotItButton.Width - buttonPanel.Padding.Right;
            gotItButton.Top = buttonPanel.Height - gotItButton.Height - buttonPanel.Padding.Bottom;
            gotItButton.Click += (s, e) => Close();
            buttonPanel.Controls.Add(gotItButton);
            AcceptButton = gotItButton;

            Controls.Add(buttonPanel);
            Controls.Add(content);

            KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) Close(); };

            int desiredHeight = content.GetPreferredSize(new Size(ClientSize.Width, 0)).Height + buttonPanel.Height + 10;
            Rectangle workingArea = Screen.PrimaryScreen.WorkingArea;
            ClientSize = new Size(420, Math.Min(desiredHeight, workingArea.Height - 80));

            Location = new Point(
                workingArea.Right - Width - 12,
                workingArea.Bottom - Height - 12);
        }

        private static Label MakeLabel(string text, Color foreColor, Font font, int width, Padding margin)
        {
            return new Label
            {
                Text = text,
                ForeColor = foreColor,
                Font = font,
                AutoSize = true,
                MaximumSize = new Size(width, 0),
                Margin = margin
            };
        }
    }
}
