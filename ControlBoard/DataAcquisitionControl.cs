using DAQSystem.Common.Model;
using DAQSystem.Common.Utility;
using NLog;
using System.Diagnostics;
using System.Globalization;
using System.IO.Ports;

namespace DAQSystem.DataAcquisition
{
    public class DataAcquisitionControl : SyncContextAwareObject
    {
        public event EventHandler<List<int>> FilteredDataReceived;

        private const string SUCCESS_RESPOND = "444F4E45";
        private const string DATA_HEAD = "AABB00";
        private const string DATA_TAIL = "EEFF";
        private const int EVENT_WAIT_TIME = 300;  // ms

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

        public async Task Uninitialize()
        {
            if (!IsInitialized) return;
            await Task.Run(() =>
            {
                serialPort_.DataReceived -= OnDataReceived;
                IsInitialized = false;
                serialPort_.Close();
            });
        }

        public async Task WriteCommand(CommandTypes cmd, int parameter = 0)
        {
            logger_.Info($"Command writing: CommandType:{cmd} Value:{parameter}");
            try
            {
                await commandLock_.WaitAsync();
                switch (cmd)
                {
                    case CommandTypes.StopAndReset:
                        WriteStopAndResetCommand();
                        break;
                    case CommandTypes.StartToCollect:
                        await WriteCollectCommand(parameter);
                        break;
                    default:
                        if (await WriteSettingCommand(cmd, parameter))
                            logger_.Info($"{cmd} successfully.");
                        else
                            logger_.Warn($"{cmd} failed.");
                        break;
                }
            }
            finally
            {
                commandLock_.Release();
            }
        }

        private async Task WriteCollectCommand(int timeout = 10000)
        {
            var command = BuildCommand(CommandTypes.StartToCollect, 0);
            serialPort_.Write(command, 0, command.Length);

            logger_.Info("Data collection started.");
            await Task.Run(() =>
            {
                var sw = Stopwatch.StartNew();
                do
                {
                    replyReceived_.WaitOne(EVENT_WAIT_TIME);
                    
                    lock (readBuffer_)
                    {
                        if (readBuffer_.Count != 0) 
                        {
                            ParseBytesToIntList(readBuffer_.ToArray());
                            readBuffer_.Clear();
                        }
                    }

                } while (sw.ElapsedMilliseconds < (timeout + EVENT_WAIT_TIME));
                sw.Stop();
                logger_.Info("Data collection stopped.");
            });
        }

        private void WriteStopAndResetCommand()
        {
            var command = BuildCommand(CommandTypes.StopAndReset, 0);
            serialPort_.Write(command, 0, command.Length);
            logger_.Info("Reset command is sent.");
        }

        private async Task<bool> WriteSettingCommand(CommandTypes cmd, int parameter)
        {
            var command = BuildCommand(cmd, parameter);
            serialPort_.Write(command, 0, command.Length);
            return await GetSettingResponse();
        }

        private static byte[] BuildCommand(CommandTypes cmd, int parameter) => BitConverter.GetBytes((short)cmd)
                                                                            .Reverse()
                                                                            .Concat(BitConverter.GetBytes(parameter).Reverse())
                                                                            .ToArray();

        private async Task<bool> GetSettingResponse()
        {
            return await Task.Run(() =>
            {
                replyReceived_.WaitOne(EVENT_WAIT_TIME);
                lock (readBuffer_)
                {
                    var response = new List<byte>(readBuffer_);
                    readBuffer_.Clear();
                    return IsSettingResponseValid(response);
                }
            });
        }

        private static bool IsSettingResponseValid(List<byte> response) => BitConverter.ToString(response.ToArray()).Replace("-", "") == SUCCESS_RESPOND;

        private void ParseBytesToIntList(byte[] bytes)
        {
            var dataList = new List<int>();
            string hexString = BitConverter.ToString(bytes).Replace("-", "").Replace(DATA_HEAD, "").Replace(DATA_TAIL, " ");
            string[] parts = hexString.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string part in parts)
            {
                if (int.TryParse(part, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var temp))
                    dataList.Add(temp);
                else
                    logger_.Warn($"Failed to parse hex value {part} to int.");
            }
            FilteredDataReceived?.Invoke(this, dataList);
        }

        private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            lock (readBuffer_)
            {
                int bytesToRead = serialPort_.BytesToRead;
                byte[] buffer = new byte[bytesToRead];
                serialPort_.Read(buffer, 0, bytesToRead);
                if (buffer.Length != 0) 
                {
                    readBuffer_.AddRange(buffer);
                    replyReceived_.Set();
                }
            }
        }

        private static readonly Logger logger_ = LogManager.GetCurrentClassLogger();
        private readonly List<byte> readBuffer_ = new();
        private readonly AutoResetEvent replyReceived_ = new(false);
        private static readonly SemaphoreSlim commandLock_ = new(1);

        private SerialPort serialPort_;
        private string comPort_;
    }
}
