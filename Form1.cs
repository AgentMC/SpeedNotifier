using SpeedNotifier.Properties;
using System.Management;
using System.Windows.Forms;

namespace SpeedNotifier
{
    public partial class Form1 : Form
    {
        private readonly ManagementObjectSearcher managementObjectSearcher;

        private AdapterState? _lastState = null;

        public Form1()
        {
            InitializeComponent();
            Log("Startup.");
            var mac = Settings.Default.Mac;
            Log($"MAC configured: {mac}");
            SelectQuery selectQuery = new("Win32_NetworkAdapter", $"MacAddress = '{mac}'");
            managementObjectSearcher = new(selectQuery);
            notifyIcon1.Icon = this.Icon;
            Timer1_Tick(null, null);
        }

        private string Convert(ulong speed)
        {
            string[] measures = ["", "k", "M", "G", "T", "P"];
            int i = 0;
            double spd = (double)speed;
            while (spd >= 1000)
            {
                i++;
                spd /= 1000;
            }
            return $"{spd:F1} {measures[i]}bps";
        }

        private void Timer1_Tick(object sender, EventArgs e)
        {
            var adapter = managementObjectSearcher.Get().Cast<ManagementObject>().FirstOrDefault();
            if (adapter != null)
            {
                object spd_raw = adapter["Speed"];
                if (spd_raw != null)
                {
                    var spd = (ulong)spd_raw;
                    HandleUpdate(new AdapterState(true, true, spd));
                }
                else
                {
                    notifyIcon1.Text = "Waiting for connection...";
                }
            }
            else
            {
                notifyIcon1.Text = "Cannot find adapter.";
            }
        }

        private void HandleUpdate(AdapterState newState)
        {
            if (newState != _lastState)
            {
                string statusText;
                Icon statusIcon;
                bool notify = false;
                if (!newState.Found) //initial scan, wrong setting, adapter disconnected...
                {
                    statusText = "Cannot find adapter.";
                    statusIcon = Resources.ConnectedNot;
                }
                else if (!newState.Connected) //DHCP recolution, link down...
                {
                    statusText = "Waiting for connection...";
                    statusIcon = Resources.ConnectedNot;
                }
                else //connected, speed changed
                {
                    const int GIGABIT = 1000000000;
                    var humanSpeed = Convert(newState.Speed);
                    statusText = $"Speed: {humanSpeed}";
                    statusIcon = newState.Speed >= GIGABIT ? Resources.ConnectedGood : Resources.ConnectedBad;
                    notifyIcon1.BalloonTipText = $"Adapter speed: {humanSpeed}";
                    notify = true;
                }
                notifyIcon1.Text = statusText;
                notifyIcon1.Icon = statusIcon;
                Icon = statusIcon;
                if (notify) notifyIcon1.ShowBalloonTip(5000);
                Log(statusText);
                _lastState = newState;
            }
        }

        private void Log(string text) => listBox1.Items.Add($"{DateTime.Now:s}\t{text}");

        private void NotifyIcon1_DoubleClick(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void NotifyIcon1_MouseClick(object sender, MouseEventArgs e)
        {
            Show();
            ShowInTaskbar = true;
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if(e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                ShowInTaskbar = false;
                WindowState = FormWindowState.Minimized;
            }
        }

        record AdapterState(bool Found, bool Connected, ulong Speed);
    }
}
