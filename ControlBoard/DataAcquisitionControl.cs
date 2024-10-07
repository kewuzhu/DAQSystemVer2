using DAQSystem.Common.Model;
using DAQSystem.Common.Utility;
using NLog;
using System.Diagnostics;
using System.IO.Ports;
using System.Reflection.Metadata;

namespace DAQSystem.DataAcquisition
{
    public class DataAcquisitionControl : SyncContextAwareObject
    {
        public event EventHandler<int> DataReceived;

        private const string SUCCESS_RESPOND = "DONE";
        private const string DATA_HEAD = "AABB00";
        private const string DATA_TAIL = "EEFF";

        public bool IsInitialized { get; private set; }

        public async Task Initialize(SerialConfiguration serialconfig)
        {
            if (IsInitialized)
            {
                if (serialconfig.SerialPort != comPort_)
                    throw new InvalidOperationException("Already initialized with a different port.");

                return;
            }

            await EnableSerialPort(serialconfig);

            IsInitialized = true;
        }

        private async Task EnableSerialPort(SerialConfiguration serialconfig)
        {
            comPort_ = serialconfig.SerialPort;
            serialPort_ = new SerialPort(comPort_, serialconfig.Baudrate, Parity.None, 8, StopBits.One);

            await Task.Run(() =>
            {
                serialPort_.Open();
                serialPort_.DiscardInBuffer();
                serialPort_.DiscardOutBuffer();
                serialPort_.DataReceived += OnDataReceived;
            });
        }

        public void Uninitialize()
        {
            if (!IsInitialized) return;

            serialPort_.DataReceived -= OnDataReceived;
            IsInitialized = false;
            serialPort_.Close();
        }

        public async Task<List<int>> WriteCollectCommand(int timeout)
        {
            var command = BuildCommand(CommandTypes.StartToCollect, 0);
            serialPort_.Write(command, 0, command.Length);

            List<int> data = new();
            const int eventWaitTime = 100;  // ms

            await Task.Run(() =>
            {
                var sw = Stopwatch.StartNew();
                do
                {
                    replyReceived_.WaitOne();
                    lock (readBuffer_) 
                    {
                        data.AddRange(ParseBytes(readBuffer_.ToArray()));
                        readBuffer_.Clear();
                    }

                } while (sw.ElapsedMilliseconds < (timeout + eventWaitTime));
                sw.Stop();
                logger_.Debug($"Collect Time: {sw.ElapsedMilliseconds} ms)");
            });
            return data;
        }

        public void WriteStopAndResetCommand()
        {
            var command = BuildCommand(CommandTypes.StopAndReset, 0);
            serialPort_.Write(command, 0, command.Length);
        }

        public async Task<bool> WriteSettingCommand(CommandTypes cmd, int parameter)
        {
            var command = BuildCommand(cmd, parameter);
            serialPort_.Write(command, 0, command.Length);
            return await GetSettingResponse();
        }

        private byte[] BuildCommand(CommandTypes cmd, int parameter) => BitConverter.GetBytes((short)cmd)
                                                                            .Reverse()
                                                                            .Concat(BitConverter.GetBytes(parameter).Reverse())
                                                                            .ToArray();

        private async Task<bool> GetSettingResponse()
        {
            return await Task.Run(() =>
            {
                replyReceived_.WaitOne();
                lock (readBuffer_)
                {
                    var response = new List<byte>(readBuffer_);
                    readBuffer_.Clear();
                    return IsSettingResponseValid(response);
                }
            });
        }

        private bool IsSettingResponseValid(List<byte> response) => BitConverter.ToString(response.ToArray()).Replace("-", "") == SUCCESS_RESPOND;

        private List<int> ParseBytes(byte[] bytes)
        {
            List<int> data = new();

            string hexString = BitConverter.ToString(bytes).Replace("-", "").Replace(DATA_HEAD, "").Replace(DATA_TAIL, "");
            for (int i = 0; i < hexString.Length; i += 4)
            {
                if (i + 1 < hexString.Length)
                {
                    string byteString = hexString.Substring(i, 4);
                    data.Add(Convert.ToInt32(byteString, 16));
                }
            }
            return data;
        }

        private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            lock (readBuffer_)
            {
                int bytesToRead = serialPort_.BytesToRead;
                byte[] buffer = new byte[bytesToRead];
                serialPort_.Read(buffer, 0, bytesToRead);
                readBuffer_.AddRange(buffer);
                replyReceived_.Set();
            }
        }

        private static readonly Logger logger_ = LogManager.GetCurrentClassLogger();

        private readonly List<byte> readBuffer_ = new();
        private readonly AutoResetEvent replyReceived_ = new(false);

        private SerialPort serialPort_;
        private string comPort_;
    }
}
