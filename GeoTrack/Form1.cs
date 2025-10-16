using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GeoTrack
{
    public partial class Form1 : Form
    {
        private TcpListener? _listener;
        private CancellationTokenSource? _cancellationTokenSource;

        public Form1()
        {
            InitializeComponent();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            StartServer();
        }

        private void StartServer()
        {
            _cancellationTokenSource = new CancellationTokenSource();
            _listener = new TcpListener(IPAddress.Loopback, 9000);
            _ = Task.Run(() => RunServerAsync(_cancellationTokenSource.Token));
        }

        private async Task RunServerAsync(CancellationToken cancellationToken)
        {
            if (_listener is null)
            {
                return;
            }

            try
            {
                _listener.Start();
                AppendMessage("Server started on localhost:9000");

                while (!cancellationToken.IsCancellationRequested)
                {
                    TcpClient client;

                    try
                    {
                        client = await _listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }

                    _ = Task.Run(() => HandleClientAsync(client, cancellationToken), cancellationToken);
                }
            }
            catch (Exception ex)
            {
                AppendMessage($"Server error: {ex.Message}");
            }
        }

        private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
        {
            using (client)
            {
                try
                {
                    using var stream = client.GetStream();
                    using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
                    using var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true)
                    {
                        AutoFlush = true
                    };

                    var message = await reader.ReadLineAsync().ConfigureAwait(false);

                    if (message is null)
                    {
                        return;
                    }

                    AppendMessage($"Client: {message}");

                    var response = $"Server đã nhận: {message}";
                    await writer.WriteLineAsync(response).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    AppendMessage($"Client handling error: {ex.Message}");
                }
            }
        }

        private void AppendMessage(string message)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<string>(AppendMessage), message);
                return;
            }

            listBoxMessages.Items.Add(message);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _cancellationTokenSource?.Cancel();
            _listener?.Stop();
            base.OnFormClosing(e);
        }

        private async void buttonSend_Click(object sender, EventArgs e)
        {
            var message = textBoxMessage.Text.Trim();

            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            try
            {
                using var client = new TcpClient();
                await client.ConnectAsync(IPAddress.Loopback, 9000).ConfigureAwait(false);

                using var stream = client.GetStream();
                using var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true)
                {
                    AutoFlush = true
                };
                using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);

                await writer.WriteLineAsync(message).ConfigureAwait(false);
                var response = await reader.ReadLineAsync().ConfigureAwait(false);

                if (!string.IsNullOrEmpty(response))
                {
                    AppendMessage(response);
                }
            }
            catch (Exception ex)
            {
                AppendMessage($"Client error: {ex.Message}");
            }
            finally
            {
                if (InvokeRequired)
                {
                    BeginInvoke(new Action(() => textBoxMessage.Clear()));
                }
                else
                {
                    textBoxMessage.Clear();
                }
            }
        }
    }
}
