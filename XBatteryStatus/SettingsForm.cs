using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

            var settings = Properties.Settings.Default;
            updateFrequency.Text = settings.UpdateFrequency.ToString();
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

        protected override void OnValidating(CancelEventArgs e)
        {

            if (updateFrequency.TextLength == 0)
            {
                e.Cancel = true;
            } 
            else
            { 
                int val;
                if (!int.TryParse(updateFrequency.Text, System.Globalization.NumberStyles.Any, null, out val))
                {
                    e.Cancel = true;
                }
                else
                {
                    if (val <= 0)
                    {
                        e.Cancel = true;
                    }
                }
            }
            base.OnValidating(e);
        }

        private void cancelButton_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void okButton_Click(object sender, EventArgs e)
        {
            var settings = Properties.Settings.Default;
            int newFreq = 0;
            if (int.TryParse(updateFrequency.Text, out newFreq))
            {
                settings.UpdateFrequency = newFreq;
            }
            settings.EnableLowBatteryNotifications = notificationsEnabled.Checked;
            settings.EnableAudioNotifications = audioEnabled.Checked;
            if (audioFileDropDown.SelectedIndex >= 0 && audioFileDropDown.SelectedIndex < audioOptions.Length)
            {
                settings.LowBatteryAudio = audioOptions[audioFileDropDown.SelectedIndex];
            }
            settings.Save();
            Close();

        }
    }
}
