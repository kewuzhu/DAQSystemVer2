using DAQSystem.Common.Model;
using DAQSystem.Common.Utility;
using NLog;
using System.IO.Ports;
using System.Text;

namespace DAQSystem.DataAcquisition
{
    public class DataAcquisitionControl : SyncContextAwareObject
    {
        public event EventHandler<List<byte>> MsgReceived;

        private readonly byte[] RECV_HEAD = new byte[] { 0xAA, 0xBB, 0x00, 0x00 };
        private readonly byte[] RECV_TAIL = new byte[]{ 0xEE, 0xFF };

        public bool IsInitialized { get; private set; }

        public void Initialize(SerialConfiguration serialconfig)
        {
            var comPort = serialconfig.SerialPort;
            var baudrate = serialconfig.Baudrate;

            if (IsInitialized)
            {
                if (comPort != comPort_)
                    throw new InvalidOperationException("Already initialized with a different port.");

                return;
            }

            comPort_ = comPort;
            serialPort_ = new SerialPort(comPort_, baudrate, Parity.None, 8, StopBits.One);
            serialPort_.Open();
            serialPort_.DiscardInBuffer();
            serialPort_.DiscardOutBuffer();
            serialPort_.DataReceived += OnDataReceived;

            IsInitialized = true;
        }

        public void Uninitialize()
        {
            if (!IsInitialized) return;

            serialPort_.DataReceived -= OnDataReceived;
            IsInitialized = false;
            serialPort_.Close();
        }

        public void WriteCommand(CommandTypes cmd, string data)
        {
            var command = BuildCommand(cmd, data);
            serialPort_.Write(command.ToArray(), 0, command.Count);
        }

        private List<byte> BuildCommand(CommandTypes cmd, string data)
        {
            const int dataLength = 6;
            if (data.Length > dataLength)
                throw new ArgumentException($"Data length must be less or equal to {dataLength}.");

            var command = new List<byte>(commandHeaderLut_[cmd]);
            var bytes = Encoding.ASCII.GetBytes(data).ToList();
            bytes.AddRange(new byte[dataLength - data.Length]);
            command.AddRange(bytes);
            return command;
        }

        private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            int bytesToRead = serialPort_.BytesToRead;
            byte[] buffer = new byte[bytesToRead];
            serialPort_.Read(buffer, 0, bytesToRead);

            readBuffer_.AddRange(buffer);
            if (readBuffer_.TakeLast(2) == RECV_TAIL)
            {
                MsgReceived?.Invoke(this, buffer.ToList());
                replyReceived_.Set();
            }
        }

        private static readonly Logger logger_ = LogManager.GetCurrentClassLogger();
        private readonly Dictionary<CommandTypes, byte[]> commandHeaderLut_ = new()
        {
            { CommandTypes.StopAndReset, new byte[]{0x00, 0x00} },
            { CommandTypes.StartToCollect, new byte[]{0x00, 0x01} },
            { CommandTypes.SetCollectDuration, new byte[]{0x00, 0x02} },
            { CommandTypes.SetInitialThreshold, new byte[]{0x11, 0x10} },
            { CommandTypes.SetSignalSign, new byte[]{0x11, 0x12} },
            { CommandTypes.SetSignalBaseline, new byte[]{0x11, 0x13} },
            { CommandTypes.SetTimeInterval, new byte[]{0x11, 0x14} },
            { CommandTypes.SetGain, new byte[]{0x11, 0x16} },
        };

        private readonly List<byte> readBuffer_ = new();
        private readonly AutoResetEvent replyReceived_ = new(false);

        private SerialPort serialPort_;
        private string comPort_;
    }
}
