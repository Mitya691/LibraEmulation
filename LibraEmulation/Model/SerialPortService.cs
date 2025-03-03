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
            byte adr = _fixedAddress;

            // Обработка команды A1h – запрос серийного номера
            if (cop == 0xA1)
            {
                byte SN2 = 0x12, SN1 = 0x34, SN0 = 0x56;
                byte[] responseData = new byte[] { adr, cop, SN2, SN1, SN0, 0x00 };
                byte crc = CalcCRC(responseData);
                byte[] response = CreateResponse(responseData, crc);
                _serialPort.Write(response, 0, response.Length);
                LogMessage?.Invoke("Отправлено (Серийный номер): " + BitConverter.ToString(response));
            }
            // Обработка команды C2h – запрос веса НЕТТО (D5=1)
            else if (cop == 0xC2)
            {
                byte[] bcdWeight = ConvertWeightToBCD(_currentWeight);
                byte con = 0;
                if (_currentWeight < 0)
                    con |= 0x80;
                con |= 0x20; // D5=1 для режима НЕТТО
                if (_isUspokoenie) con |= 0x10;
                if (_isPereg) con |= 0x08;
                con |= 0x01; // позиция запятой
                byte[] responseData = new byte[] { adr, cop, bcdWeight[0], bcdWeight[1], bcdWeight[2], con, 0x00 };
                byte crc = CalcCRC(responseData);
                byte[] response = CreateResponse(responseData, crc);
                _serialPort.Write(response, 0, response.Length);
                LogMessage?.Invoke("Отправлено (Вес НЕТТО): " + BitConverter.ToString(response));
            }
            // Обработка команды C3h – запрос веса БРУТТО (D5=0)
            else if (cop == 0xC3)
            {
                // Используем актуальное значение CurrentWeight, которое обновляется через привязку
                byte[] bcdWeight = ConvertWeightToBCD(CurrentWeight);
                byte con = 0;
                if (CurrentWeight < 0)
                    con |= 0x80;
                if (IsUspokoenie) con |= 0x10;
                if (IsPereg) con |= 0x08;
                con |= 0x01; // позиция запятой
                byte[] responseData = new byte[] { adr, cop, bcdWeight[0], bcdWeight[1], bcdWeight[2], con, 0x00 };
                byte crc = CalcCRC(responseData);
                byte[] response = CreateResponse(responseData, crc);
                _serialPort.Write(response, 0, response.Length);
                LogMessage?.Invoke("Отправлено (Вес БРУТТО): " + BitConverter.ToString(response));
            }
            // Обработка команды BFh – передать состояние весоизмерительной системы
            else if (cop == 0xBF)
            {
                byte status = 0;
                if (IsReweighing) status |= 0x80; // D7 = 1: режим перевешивания
                if (HasError) status |= 0x40; // D6 = 1: сообщение об ошибке
                if (IsStopMode) status |= 0x20; // D5 = 1: режим "СТОП"
                if (IsCycleComplete) status |= 0x10; // D4 = 1: завершён цикл набора отвеса
                if (IsPaused) status |= 0x08; // D3 = 1: режим ПАУЗА/БЛОКИРОВКА
                if (IsLoading) status |= 0x04; // D2 = 1: идет загрузка весового бункера
                if (IsUnloading) status |= 0x02; // D1 = 1: идет разгрузка весового бункера
                if (IsOnPass) status |= 0x01; // D0 = 1: включен режим "на проход"

                byte[] responseData = new byte[] { adr, cop, status, 0x00 };
                byte crc = CalcCRC(responseData);
                byte[] response = CreateResponse(responseData, crc);
                _serialPort.Write(response, 0, response.Length);
                LogMessage?.Invoke("Отправлено (Состояние BFh): " + BitConverter.ToString(response));
            }
            else if (cop == 0xC8)
            {
                if (buffer.Length >= 7)
                {
                    byte nw = buffer[3]; // NW – номер запрашиваемого счётчика (0..9)
                    byte[] counterData;
                    // Реализуем только счётчики с индексами 4 и 8
                    if (nw == 0x04)
                    {
                        counterData = _counterModel.GetCounterBCD(4);
                    }
                    else if (nw == 0x08)
                    {
                        counterData = _counterModel.GetCounterBCD(8);
                    }
                    else
                    {
                        counterData = new byte[0]; // Для других значений ничего не передаём
                    }

                    int len = 3 + counterData.Length + 1; // Adr, COP, NW, счетчик(и), CRC
                    byte[] responseData = new byte[len];
                    responseData[0] = adr;
                    responseData[1] = cop;
                    responseData[2] = nw;
                    Array.Copy(counterData, 0, responseData, 3, counterData.Length);
                    responseData[responseData.Length - 1] = 0x00; // CRC placeholder
                    byte crc = CalcCRC(responseData);
                    responseData[responseData.Length - 1] = crc;

                    byte[] response = new byte[responseData.Length + 3];
                    response[0] = 0xFF;
                    Array.Copy(responseData, 0, response, 1, responseData.Length);
                    response[response.Length - 2] = 0xFF;
                    response[response.Length - 1] = 0xFF;

                    _serialPort.Write(response, 0, response.Length);
                    LogMessage?.Invoke("Отправлено (Счетчик C8h): " + BitConverter.ToString(response));
                }
            }
        }

        // Вспомогательный метод для формирования полного ответа с разделителями
        private byte[] CreateResponse(byte[] data, byte crc)
        {
            byte[] response = new byte[data.Length + 3];
            response[0] = 0xFF;
            Array.Copy(data, 0, response, 1, data.Length);
            // Предполагаем, что последний байт data является CRC-плейсхолдером – заменяем его
            response[data.Length] = crc;
            response[response.Length - 2] = 0xFF;
            response[response.Length - 1] = 0xFF;
            return response;
        }

        private byte CRCMaker(byte bInput, byte bCRC)
        {
            for (int i = 0; i < 8; i++)
            {
                int cfInput = ((bInput & 0x80) != 0) ? 1 : 0;
                bInput = (byte)(((bInput << 1) & 0xFF) | cfInput);
                int cfCRC = ((bCRC & 0x80) != 0) ? 1 : 0;
                bCRC = (byte)(((bCRC << 1) & 0xFF) | cfInput);
                if (cfCRC == 1)
                    bCRC ^= 0x69;
            }
            return bCRC;
        }

        private byte CalcCRC(byte[] data)
        {
            byte crc = 0;
            foreach (var b in data)
                crc = CRCMaker(b, crc);
            return crc;
        }

        private byte[] ConvertWeightToBCD(double weight)
        {
            int intVal = (int)Math.Round(Math.Abs(weight) * 10); // учитываем один знак после запятой
            string s = intVal.ToString("D6");
            byte[] result = new byte[3];
            result[0] = (byte)(((s[4] - '0') << 4) | (s[5] - '0'));
            result[1] = (byte)(((s[2] - '0') << 4) | (s[3] - '0'));
            result[2] = (byte)(((s[0] - '0') << 4) | (s[1] - '0'));
            return result;
        }
    }
}
