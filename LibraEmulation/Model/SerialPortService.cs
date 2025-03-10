using LibraEmulation.Model;
using System;
using System.IO.Ports;
using System.Threading;
using System.Windows.Threading;

namespace ScaleEmulator
{
    public class SerialPortService
    {
        private SerialPort _serialPort;
        private Thread _readThread;
        private bool _keepReading;
        private bool _handshakeCompleted = false;
        private DateTime _lastDataReceived;
        private DispatcherTimer _timeoutTimer;

        // Сохраним фиксированный адрес, переданный из ViewModel
        private byte _fixedAddress;

        // Параметры, которые могут использоваться при формировании ответов (например, вес)
        private double _currentWeight;
        private bool _isUspokoenie;
        private bool _isPereg;

        public event Action<string> LogMessage;
        public event Action<string> ErrorMessage;
        public event Action HandshakeCompleted;

        public double CurrentWeight { get; set; }
        public bool IsUspokoenie { get; set; }
        public bool IsPereg { get; set; }
        public bool IsReweighing { get; set; }
        public bool HasError { get; set; }
        public bool IsStopMode { get; set; }
        public bool IsCycleComplete { get; set; }
        public bool IsPaused { get; set; }
        public bool IsLoading { get; set; }
        public bool IsUnloading { get; set; }
        public bool IsOnPass { get; set; }

        private CounterModel _counterModel = new CounterModel();
        private LibraModel _libraOne = new LibraModel();
        private LibraModel _libraTwo = new LibraModel();

        public SerialPortService()
        {
            _timeoutTimer = new DispatcherTimer();
            _timeoutTimer.Interval = TimeSpan.FromSeconds(10);
            _timeoutTimer.Tick += TimeoutTimer_Tick;
        }

        private void TimeoutTimer_Tick(object sender, EventArgs e)
        {
            if ((DateTime.Now - _lastDataReceived).TotalSeconds > 10)
            {
                _timeoutTimer.Stop();
                if (!_handshakeCompleted)
                {
                    LogMessage?.Invoke("Соединение не установлено: таймаут ожидания данных.");
                }
            }
        }

        public void Start(string portName, int baudRate, Parity parity, byte fixedAddress, double weight, bool uspokoenie, bool pereg, double initialWeight)
        {
            _fixedAddress = fixedAddress;
            _currentWeight = weight;
            _isUspokoenie = uspokoenie;
            _isPereg = pereg;
            _serialPort = new SerialPort(portName, baudRate, parity, 8, StopBits.One);
            _serialPort.Open();
            _keepReading = true;
            _handshakeCompleted = false;
            _lastDataReceived = DateTime.Now;
            _timeoutTimer.Start();
            _readThread = new Thread(ReadSerial);
            _readThread.Start();
        }

        public void Stop()
        {
            _keepReading = false;
            if (_serialPort != null && _serialPort.IsOpen)
                _serialPort.Close();
            _timeoutTimer.Stop();
        }

        private void ReadSerial()
        {
            while (_keepReading)
            {
                try
                {
                    if (_serialPort.BytesToRead > 0)
                    {
                        _lastDataReceived = DateTime.Now;
                        int bytes = _serialPort.BytesToRead;
                        byte[] buffer = new byte[bytes];
                        _serialPort.Read(buffer, 0, bytes);
                        LogMessage?.Invoke("Получено: " + BitConverter.ToString(buffer));

                        if (buffer.Length >= 6 &&
                            buffer[0] == 0xFF &&
                            buffer[buffer.Length - 2] == 0xFF &&
                            buffer[buffer.Length - 1] == 0xFF)
                        {
                            if (!_handshakeCompleted)
                            {
                                _handshakeCompleted = true;
                                HandshakeCompleted?.Invoke();
                            }
                            ProcessBuffer(buffer);
                        }
                    }
                }
                catch (Exception ex)
                {
                    ErrorMessage?.Invoke("Ошибка при обмене: " + ex.Message);
                }
                Thread.Sleep(100);
            }
        }

        private void ProcessBuffer(byte[] buffer)
        {
            // Обработка входящего кадра по протоколу.
            // Извлекаем код операции из третьего байта.
            byte cop = buffer[2];
            // Для формирования ответов всегда используем фиксированный адрес
            byte adr = buffer[1];

            // Обработка команды A1h – запрос серийного номера
            if (cop == 0xA1)
            {
                if(adr == 0x01)
                {
                    byte[] response = _libraOne.CopA1Response();
                    _serialPort.Write(response, 0, response.Length);
                    LogMessage?.Invoke("Отправлено (Серийный номер) весы 1: " + BitConverter.ToString(response));
                }
                else if (adr == 0x02)
                {
                    byte[] response = _libraTwo.CopA1Response();
                    _serialPort.Write(response, 0, response.Length);
                    LogMessage?.Invoke("Отправлено (Серийный номер) весы 2: " + BitConverter.ToString(response));
                }
                
            }
            // Обработка команды C2h – запрос веса НЕТТО (D5=1)
            else if (cop == 0xC2)
            {
                if (adr == 0x01)
                {
                    byte[] response = _libraOne.CopC2Response();
                    _serialPort.Write(response, 0, response.Length);
                    LogMessage?.Invoke("Отправлено (Вес НЕТТО) весы 1: " + BitConverter.ToString(response));
                }
                else if (adr == 0x02)
                {
                    byte[] response = _libraTwo.CopC2Response();
                    _serialPort.Write(response, 0, response.Length);
                    LogMessage?.Invoke("Отправлено (Вес НЕТТО) весы 2: " + BitConverter.ToString(response));
                }
                
            }
            // Обработка команды C3h – запрос веса БРУТТО (D5=0)
            else if (cop == 0xC3)
            {
                if (adr == 0x01)
                {
                    byte[] response = _libraOne.CopC3Response();
                    _serialPort.Write(response, 0, response.Length);
                    LogMessage?.Invoke("Отправлено (Вес БРУТТО) весы 1: " + BitConverter.ToString(response));
                }
                else if (adr == 0x02)
                {
                    byte[] response = _libraTwo.CopC3Response();
                    _serialPort.Write(response, 0, response.Length);
                    LogMessage?.Invoke("Отправлено (Вес БРУТТО) весы 2: " + BitConverter.ToString(response));
                }
            }
            // Обработка команды BFh – передать состояние весоизмерительной системы
            else if (cop == 0xBF)
            {
                if (adr == 0x01)
                {
                    byte[] response = _libraOne.CopBFResponse();
                    _serialPort.Write(response, 0, response.Length);
                    LogMessage?.Invoke("Отправлено (Состояние BFh): " + BitConverter.ToString(response));
                }
                else if (adr == 0x02)
                {
                    byte[] response = _libraTwo.CopBFResponse();
                    _serialPort.Write(response, 0, response.Length);
                    LogMessage?.Invoke("Отправлено (Состояние BFh): " + BitConverter.ToString(response));
                }
                
            }
            else if (cop == 0xC8)
            {
                if (buffer.Length >= 7)
                {
                    if (buffer.Length >= 7)
                    {
                        if (adr == 0x01)
                        {
                            byte nw = buffer[3]; // NW – номер запрашиваемого счётчика
                            byte[] response = _libraOne.CopC8Response(nw);
                            _serialPort.Write(response, 0, response.Length);
                            LogMessage?.Invoke("Отправлено (Счетчик C8h): " + BitConverter.ToString(response));
                        }
                        else if (adr == 0x02)
                        {
                            byte nw = buffer[3]; // NW – номер запрашиваемого счётчика
                            byte[] response = _libraTwo.CopC8Response(nw);
                            _serialPort.Write(response, 0, response.Length);
                            LogMessage?.Invoke("Отправлено (Счетчик C8h): " + BitConverter.ToString(response));
                        }
                    }
                }
            }
        }
    }
}
