using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Connect4
{
    public class  Communicator: IDisposable
    {
        private readonly int _incomingPort;
        private readonly string _outgoingIp;
        private readonly int _outgoingPort;
        private readonly UdpClient? _udpClient;
        private readonly IPEndPoint? _remoteEndPoint;
        private CancellationTokenSource _cancellationTokenSource = new();

        public Communicator(int incomingPort, string outgoingIp, int outgoingPort)
        {
            _incomingPort = incomingPort;
            _outgoingIp = outgoingIp;
            _outgoingPort = outgoingPort;

            _udpClient = new UdpClient(_incomingPort);
            _remoteEndPoint = new IPEndPoint(IPAddress.Parse(_outgoingIp), _outgoingPort);
        }

        public void Dispose()
        {
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource.Dispose();

            GC.SuppressFinalize(this);
            _udpClient?.Dispose();
        }

        public async Task<string> ListenAsync()
        {
            _ = _udpClient ?? throw new InvalidOperationException("UDP client is not initialized.");

            try
            {
                var receivedResults = await _udpClient.ReceiveAsync(_cancellationTokenSource.Token);
                return Encoding.UTF8.GetString(receivedResults.Buffer);
            }
            catch (OperationCanceledException)
            {
                return "-1";
            }
        }

        public async Task SendAsync(string message)
        {
            _ = _udpClient ?? throw new InvalidOperationException("UDP client is not initialized.");
            
            byte[] data = Encoding.UTF8.GetBytes(message);
           
            await _udpClient.SendAsync(data, data.Length, _remoteEndPoint!);
        }
    }
}