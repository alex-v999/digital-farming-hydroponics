using System;
using System.Drawing;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using MQTTnet;
using MQTTnet.Protocol;
using Newtonsoft.Json.Linq;
using Guna.UI2.WinForms;
using MQTTnet.Formatter;
using Digital_Farming.Functii;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Window;
using System.Diagnostics;

namespace Digital_Farming
{
    public partial class Form1 : Form
    {
        // HiveMQ Cloud settings
        const string Broker = "1234";
        const int Port = 8883;
        const string User = "desktop";
        const string Pass = "1234";

        // Topics
        // write-only topics
        const string TOPIC_CMD_TIMERS = "home/device1/command/timers";
        const string TOPIC_CMD_PARAMS = "home/device1/command/params";

        // read-only topics
        const string TOPIC_CMD_PUMP = "home/device1/command/pump";
        const string TOPIC_CMD_LIGHT = "home/device1/command/light";
        const string TOPIC_CMD_EXHAUST = "home/device1/command/exhaust";
        const string TOPIC_STATUS = "home/device1/status";

        private string _pumpState = "OFF";
        private string _lightState = "OFF";
        private string _exhaustState = "OFF";

        private IMqttClient _mqtt;
        private MqttClientOptions _opts;

        enum CtrlState { OFF = 0, ON = 1, AUTO = 2 }

        private Profil _profile;

        private readonly LoggingService _logger;

        private NotificationService _notifier;
        private const float UptakeRatePerPlantPerDay = 1.5f; 

        public Form1()
        {
            InitializeComponent();

            lbl_PHVal.Text = "0";
            lbl_EC.Text = "0";
            lbl_TDSVal.Text = "0 PPM";
            lbl_waterTemp.Text = "0 °C";
            lbl_AmbientTemp.Text = "0 °C";
            lbl_Humidity.Text = "0 %";
             
            tb_fanActive.Text = "60";
            tb_fanOFF.Text = "60";
            tb_growActive.Text = "60";
            tb_growOff.Text = "60";
            tb_pumpActive.Text = "60";
            tb_pumpOFF.Text = "60";

            lblConnection.Text = "MQTT – Disconnected"; 
            lbl_hardstatus.Text = "PUMP:OFF, LIGHT:OFF, EXHAUST:OFF";   

            SetButtonState(btnWaterPump, CtrlState.OFF);
            SetButtonState(btnGrowLight, CtrlState.OFF);
            SetButtonState(btnExhaust, CtrlState.OFF);

            InitializeMqttClient();

            this.Load += Form1_Load;

            btnWaterPump.Click += ControlButton_Click;
            btnGrowLight.Click += ControlButton_Click;
            btnExhaust.Click += ControlButton_Click;

            tb_pumpActive.KeyDown += TimerTextBox_KeyDown;
            tb_pumpOFF.KeyDown += TimerTextBox_KeyDown;
            tb_growActive.KeyDown += TimerTextBox_KeyDown;
            tb_growOff.KeyDown += TimerTextBox_KeyDown;
            tb_fanActive.KeyDown += TimerTextBox_KeyDown;
            tb_fanOFF.KeyDown += TimerTextBox_KeyDown;


            _profile = new Profil
            {
                Culture = "Tomatoes",
                PlantCount = 5,
                ContainerSizeL = 10.0f,
                SubstrateType = "Coco Coir"
            };

            _logger = new LoggingService();
            _notifier = new NotificationService(_profile);
        }

        private void InitializeMqttClient()
        {
            var factory = new MqttClientFactory();
            _mqtt = factory.CreateMqttClient();

            _opts = new MqttClientOptionsBuilder()
                .WithClientId(Guid.NewGuid().ToString())
                .WithTcpServer(Broker, Port)
                .WithCredentials(User, Pass)
                .WithCleanSession(false)
                .WithTlsOptions(new MqttClientTlsOptions()
                {
                    UseTls = true,
                    AllowUntrustedCertificates = true,
                    SslProtocol = System.Security.Authentication.SslProtocols.Tls12
                })
                .WithProtocolVersion(MqttProtocolVersion.V500)
                .Build();


            _mqtt.ConnectedAsync += async args =>
            {
                BeginInvoke(() => lblConnection.Text = "MQTT – Connected");

                await _mqtt.SubscribeAsync(new MqttTopicFilterBuilder()
                    .WithTopic(TOPIC_STATUS)
                    .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtMostOnce)
                    .Build());

                foreach (var t in new[] { TOPIC_CMD_PUMP, TOPIC_CMD_LIGHT, TOPIC_CMD_EXHAUST })
                {
                    await _mqtt.SubscribeAsync(new MqttTopicFilterBuilder()
                        .WithTopic(t)
                        .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                        .Build());
                }
            };

            _mqtt.DisconnectedAsync += async args =>
            {
                BeginInvoke(() => lblConnection.Text = "MQTT – Disconnected");
                await Task.Delay(5000);
                try
                {
                    await _mqtt.ConnectAsync(_opts, CancellationToken.None);
                }
                catch { }
            };

            _mqtt.ApplicationMessageReceivedAsync += args =>
            {
                if (args.ApplicationMessage.Topic != TOPIC_STATUS)
                    return Task.CompletedTask;

                string topic = args.ApplicationMessage.Topic;
                var payload = Encoding.UTF8.GetString(args.ApplicationMessage.Payload);
                var j = JObject.Parse(payload);

                if (topic == TOPIC_STATUS)
                {
                    float ph = j["pH"]?.Value<float>() ?? 0f;
                    float tds = j["tds"]?.Value<float>() ?? 0f;
                    float ec = j["ec"]?.Value<float>() ?? 0f;
                    float ambT = j["ambientTemp"]?.Value<float>() ?? 0f;
                    float hum = j["humidity"]?.Value<float>() ?? 0f;
                    float waterT = j["waterTemp"]?.Value<float>() ?? 0f;

                    BeginInvoke((Action)(() =>
                    {
                        lbl_PHVal.Text = ph.ToString("F2");
                        lbl_TDSVal.Text = $"{tds:F0} PPM";
                        lbl_EC.Text = $"{ec:F0} µS";
                        lbl_AmbientTemp.Text = $"{ambT:F1} °C";
                        lbl_Humidity.Text = $"{hum:F0} %";
                        lbl_waterTemp.Text = $"{waterT:F1} °C";

                        _profile.PH = ph;
                        _profile.TDS = (int)tds;
                        _profile.EC = ec;
                        _profile.AmbientTempC = ambT;
                        _profile.HumidityPct = hum;
                        _profile.WaterTempC = waterT;
                    }));
                }

                else if (topic == TOPIC_CMD_PUMP)
                {
                    _pumpState = payload.Equals("ON", StringComparison.OrdinalIgnoreCase) ? "ON" : "OFF";
                    BeginInvoke((Action)(() =>
                    {
                        lbl_hardstatus.Text = $"PUMP:{_pumpState}, LIGHT:{_lightState}, EXHAUST:{_exhaustState}";
                    }));
                }

                else if (topic == TOPIC_CMD_LIGHT)
                {
                    _lightState = payload.Equals("ON", StringComparison.OrdinalIgnoreCase) ? "ON" : "OFF";
                    BeginInvoke((Action)(() =>
                    {
                        lbl_hardstatus.Text = $"PUMP:{_pumpState}, LIGHT:{_lightState}, EXHAUST:{_exhaustState}";
                    }));
                }

                else if (topic == TOPIC_CMD_EXHAUST)
                {
                    _exhaustState = payload.Equals("ON", StringComparison.OrdinalIgnoreCase) ? "ON" : "OFF";
                    BeginInvoke((Action)(() =>
                    {
                        lbl_hardstatus.Text = $"PUMP:{_pumpState}, LIGHT:{_lightState}, EXHAUST:{_exhaustState}";
                    }));
                }

                return Task.CompletedTask;
            };
        }

        private async void Form1_Load(object sender, EventArgs e)
        {
            lblConnection.Text = "MQTT – Connecting…";
            try
            {
                await _mqtt.ConnectAsync(_opts, CancellationToken.None);
            }
            catch (Exception ex)
            {
                lblConnection.Text = "MQTT – Error";
                MessageBox.Show($"Could not connect to broker:\n{ex.Message}",
                                "MQTT Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            SetButtonState(btnWaterPump, CtrlState.OFF);
            SetButtonState(btnGrowLight, CtrlState.OFF);
            SetButtonState(btnExhaust, CtrlState.OFF);
        }

        private void btnWaterPump_Click(object s, EventArgs e)
            => CycleControl(btnWaterPump, TOPIC_CMD_PUMP, tb_pumpActive, tb_pumpOFF);
        private void btnGrowLight_Click(object s, EventArgs e)
            => CycleControl(btnGrowLight, TOPIC_CMD_LIGHT, tb_growActive, tb_growOff);
        private void btnExhaust_Click(object s, EventArgs e)
            => CycleControl(btnExhaust, TOPIC_CMD_EXHAUST, tb_fanActive, tb_fanOFF);

        private void CycleControl(Guna2Button btn, string topic, Guna2TextBox tbA, Guna2TextBox tbB)
        {
            var state = (CtrlState)(btn.Tag ?? CtrlState.OFF);
            switch (state)
            {
                case CtrlState.OFF:
                    PublishCommand(topic, "ON");
                    SetButtonState(btn, CtrlState.ON);
                    break;
                case CtrlState.ON:
                    PublishCommand(topic, "OFF");
                    SetButtonState(btn, CtrlState.OFF);
                    break;
                case CtrlState.AUTO:
                    var csv = $"{tbA.Text},{tbB.Text}";
                    PublishCommand(TOPIC_CMD_PARAMS, csv);
                    break;
            }
            btn.Tag = (CtrlState)(((int)state + 1) % 3);
        }

        private void SetButtonState(Guna2Button btn, CtrlState st)
        {
            btn.Tag = st;
            var name = btn.Name.Replace("btn", "");
            switch (st)
            {
                case CtrlState.ON:
                    btn.FillColor = Color.FromArgb(40, 40, 40);
                    btn.Text = $"{name} ON";
                    break;
                case CtrlState.OFF:
                    btn.FillColor = Color.FromArgb(255, 100, 100);
                    btn.Text = $"{name} OFF";
                    break;
                case CtrlState.AUTO:
                    btn.FillColor = Color.SkyBlue;
                    btn.Text = $"{name} AUTO";
                    break;
            }
        }

        private void PublishCommand(string topic, string payload)
        {
            if (!_mqtt.IsConnected) return;
            var msg = new MqttApplicationMessageBuilder()
                          .WithTopic(topic)
                          .WithPayload(payload)
                          .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                          .WithRetainFlag(true)
                          .Build();
            _mqtt.PublishAsync(msg, CancellationToken.None);
        }


        private void ControlButton_Click(object sender, EventArgs e)
        {
            var btn = (Guna2Button)sender;
            var current = (CtrlState)(btn.Tag ?? CtrlState.OFF);
            var next = (CtrlState)(((int)current + 1) % 3);
            SetButtonState(btn, next);

            if (next == CtrlState.AUTO)
            {
                (var tbA, var tbB) = (btn == btnWaterPump)
                    ? (tb_pumpActive, tb_pumpOFF)
                    : (btn == btnGrowLight)
                        ? (tb_growActive, tb_growOff)
                        : (tb_fanActive, tb_fanOFF);

                var timersCsv = $"{tbA.Text},{tbB.Text}";
                PublishCommand(TOPIC_CMD_TIMERS, timersCsv);
            }

            btn.Tag = next;

            SendParamsToEsp32();
        }
        private void btnProfile_click(object sender, EventArgs e)
        {
            using var dlg = new Profil_View(_profile);
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                CultureSettings.ApplyToProfile(_profile);

                txtContainerSize.Text = _profile.ContainerSizeL.ToString("0.##");
                txtPlantCount.Text = _profile.PlantCount.ToString();
                txtCulture.Text = _profile.Culture;
                txtSubstrate.Text = _profile.SubstrateType;

                CheckThresholds();
            }
        }
        private void CheckThresholds()
        {
            // pH
            if (_profile.PH < _profile.PHMin || _profile.PH > _profile.PHMax)
                lbl_PHVal.ForeColor = Color.Red;
            else
                lbl_PHVal.ForeColor = Color.White;

            // TDS
            if (_profile.EC < _profile.ECMin || _profile.EC > _profile.ECMax)
                lbl_EC.ForeColor = Color.Red;
            else
                lbl_EC.ForeColor = Color.White;

            // TDS
            if (_profile.TDS < _profile.TDSMin || _profile.TDS > _profile.TDSMax)
                lbl_TDSVal.ForeColor = Color.Red;
            else
                lbl_TDSVal.ForeColor = Color.White;

            // Water Temp
            if (_profile.WaterTempC < _profile.WaterTempMin || _profile.WaterTempC > _profile.WaterTempMax)
                lbl_waterTemp.ForeColor = Color.Red;
            else
                lbl_waterTemp.ForeColor = Color.White;

            // Ambient Temp
            if (_profile.AmbientTempC < _profile.AmbientTempMin || _profile.AmbientTempC > _profile.AmbientTempMax)
                lbl_AmbientTemp.ForeColor = Color.Red;
            else
                lbl_AmbientTemp.ForeColor = Color.White;

            // Humidity
            if (_profile.HumidityPct < _profile.HumidityMin|| _profile.HumidityPct> _profile.HumidityMax)
                lbl_Humidity.ForeColor = Color.Red;
            else
                lbl_Humidity.ForeColor = Color.White;


            string snapshot =
            $"pH={_profile.PH:0.##}, TDS={_profile.TDS}, EC={_profile.EC}, " + 
            $"H2O={_profile.WaterTempC:0.##}°C, Amb={_profile.AmbientTempC:0.##}°C, " +
            $"Hum={_profile.HumidityPct:0.##}%";

            // 2) Log it:
            _logger.Log(snapshot);

            // Generate news items:
            var news = _notifier.Evaluate(UptakeRatePerPlantPerDay);

            dgvLog.Rows.Clear();
            foreach (var msg in news)
            {
                dgvLog.Rows.Add(DateTime.Now.ToString("HH:mm:ss"), msg);

                if (dgvLog.Rows.Count > 100)
                    dgvLog.Rows.RemoveAt(0);

            }

        }

        private void TimerTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                SendTimersToEsp32();
            }
        }
        private void SendTimersToEsp32()
        {
            var csv = $"{tb_pumpActive.Text.Trim()},{tb_pumpOFF.Text.Trim()}," +
                      $"{tb_growActive.Text.Trim()},{tb_growOff.Text.Trim()}," +
                      $"{tb_fanActive.Text.Trim()},{tb_fanOFF.Text.Trim()}";

            PublishCommand(TOPIC_CMD_TIMERS, csv);
            SetButtonState(btnWaterPump, CtrlState.AUTO);
            SetButtonState(btnGrowLight, CtrlState.AUTO);
            SetButtonState(btnExhaust, CtrlState.AUTO);

            SendParamsToEsp32();
        }
    
        private void SendParamsToEsp32()
        {
            CtrlState pumpTag = (CtrlState)(btnWaterPump.Tag ?? CtrlState.OFF);
            CtrlState lightTag = (CtrlState)(btnGrowLight.Tag ?? CtrlState.OFF);
            CtrlState exhaustTag = (CtrlState)(btnExhaust.Tag ?? CtrlState.OFF);

            string pumpPart = pumpTag == CtrlState.AUTO
                ? "PUMP:AUTO"
                : $"PUMP:{pumpTag}:MANUAL";

            string lightPart = lightTag == CtrlState.AUTO
                ? "LIGHT:AUTO"
                : $"LIGHT:{lightTag}:MANUAL";

            string exhaustPart = exhaustTag == CtrlState.AUTO
                ? "EXHAUST:AUTO"
                : $"EXHAUST:{exhaustTag}:MANUAL";

            string csv = $"{pumpPart},{lightPart},{exhaustPart}";
            PublishCommand(TOPIC_CMD_PARAMS, csv);
            Debug.WriteLine($"[MQTT ▶ params] {csv}");
        }

    }
}
