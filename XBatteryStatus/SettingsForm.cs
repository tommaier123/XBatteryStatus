using Microsoft.Toolkit.Uwp.Notifications;
using System;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;

namespace XBatteryStatus
{

    public partial class SettingsForm : Form
    {

        private string[] audioOptions = {
            "ms-winsoundevent:Notification.Default",
            "ms-winsoundevent:Notification.IM",
            "ms-winsoundevent:Notification.Mail",
            "ms-winsoundevent:Notification.Reminder",
            "ms-winsoundevent:Notification.SMS",
            "ms-winsoundevent:Notification.Looping.Alarm",
            "ms-winsoundevent:Notification.Looping.Alarm2",
            "ms-winsoundevent:Notification.Looping.Alarm3",
            "ms-winsoundevent:Notification.Looping.Alarm4",
            "ms-winsoundevent:Notification.Looping.Alarm5",
            "ms-winsoundevent:Notification.Looping.Alarm6",
            "ms-winsoundevent:Notification.Looping.Alarm7",
            "ms-winsoundevent:Notification.Looping.Alarm8",
            "ms-winsoundevent:Notification.Looping.Alarm9",
            "ms-winsoundevent:Notification.Looping.Alarm10",

        };

        public SettingsForm()
        {
            InitializeComponent();

            this.CancelButton = cancelButton;

            var settings = Properties.Settings.Default;
            updateFrequency.Text = (settings.UpdateFrequency / 1000).ToString();
            notificationsEnabled.Checked = settings.EnableLowBatteryNotifications;
            audioEnabled.Checked = settings.EnableAudioNotifications;
            audioFileDropDown.Items.Clear();
            foreach (string opt in audioOptions)
            {
                string lastPart = opt.Split('.').Last();
                audioFileDropDown.Items.Add(lastPart);
            }
            audioFileDropDown.SelectedIndex = Array.FindIndex(audioOptions, item => item == settings.LowBatteryAudio);
        }

        private void cancelButton_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private void okButton_Click(object sender, EventArgs e)
        {
            if (ValidateChildren())
            {
                var settings = Properties.Settings.Default;
                int newFreq = 0;
                if (int.TryParse(updateFrequency.Text, out newFreq))
                {
                    settings.UpdateFrequency = newFreq * 1000;
                }
                settings.EnableLowBatteryNotifications = notificationsEnabled.Checked;
                settings.EnableAudioNotifications = audioEnabled.Checked;
                if (audioFileDropDown.SelectedIndex >= 0 && audioFileDropDown.SelectedIndex < audioOptions.Length)
                {
                    settings.LowBatteryAudio = audioOptions[audioFileDropDown.SelectedIndex];
                }
                settings.Save();
                DialogResult = DialogResult.OK;
                Close();
            }
        }

        private void testAudio_Click(object sender, EventArgs e)
        {
            PlaySelectedAudio();
        }

        private void PlaySelectedAudio()
        {
            if (audioFileDropDown.SelectedIndex >= 0 && audioFileDropDown.SelectedIndex < audioOptions.Length)
            {
                // There's not a way to play toast audio directly here, so do a preview notification
                new ToastContentBuilder()
                    .AddText("Test Low Battery Notification")
                    .AddAudio(new ToastAudio()
                    {
                        Src = new Uri(audioOptions[audioFileDropDown.SelectedIndex]),
                        Loop = false
                    })
                    .Show();
            }
        }

        private void updateFrequency_Validating(object sender, CancelEventArgs e)
        {
            errorProvider.SetError(updateFrequency, "");

            if (updateFrequency.TextLength == 0)
            {
                e.Cancel = true;
                updateFrequency.Focus();
                errorProvider.SetError(updateFrequency, "Update Frequency cannot be blank");
            } 
            else
            { 
                int val;
                if (!int.TryParse(updateFrequency.Text, System.Globalization.NumberStyles.Any, null, out val))
                {
                    e.Cancel = true;
                    updateFrequency.Focus();
                    errorProvider.SetError(updateFrequency, "Update Frequency is not a number");
                }
                else
                {
                    if (val <= 0)
                    {
                        e.Cancel = true;
                        updateFrequency.Focus();
                        errorProvider.SetError(updateFrequency, "Update Frequency must be greater than zero");
                    }
                }
            }
        }
    }
}

