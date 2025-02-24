using System;
using System.IO.Ports;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace LibraEmulation
{
    public partial class MainWindow : Window
    {
        SerialPort _serialPort;
        Thread _readThread;
        bool _keepReading;

        // Фиксированный адрес устройства, теперь вводится в десятичном формате.
        byte FixedAddress => byte.Parse(txtFixedAddress.Text);

        public MainWindow()
        {
            InitializeComponent();
            // Заполняем список доступных портов и настройки
            comboPort.ItemsSource = SerialPort.GetPortNames();
            if (comboPort.Items.Count > 0)
                comboPort.SelectedIndex = 0;

            comboBaud.ItemsSource = new int[] { 9600, 19200, 38400, 57600, 115200 };
            comboBaud.SelectedItem = 9600;

            comboParity.ItemsSource = Enum.GetNames(typeof(Parity));
            comboParity.SelectedItem = Parity.None.ToString();
        }

        private void btnStart_Click(object sender, RoutedEventArgs e)
        {
            if (_serialPort == null || !_serialPort.IsOpen)
            {
                try
                {
                    _serialPort = new SerialPort(comboPort.SelectedItem.ToString(),
                        int.Parse(comboBaud.SelectedItem.ToString()),
                        (Parity)Enum.Parse(typeof(Parity), comboParity.SelectedItem.ToString()),
                        8,
                        StopBits.One);
                    _serialPort.Open();
                    Log("Порт открыт.");
                    _keepReading = true;
                    _readThread = new Thread(ReadSerial);
                    _readThread.Start();
                    btnStart.Content = "Стоп";
                }
                catch (Exception ex)
                {
                    Log("Ошибка открытия порта: " + ex.Message);
                }
            }
            else
            {
                _keepReading = false;
                _serialPort.Close();
                btnStart.Content = "Старт";
                Log("Порт закрыт.");
            }
        }

        // Фоновый поток для чтения данных из порта
        private void ReadSerial()
        {
            while (_keepReading)
            {
                try
                {
                    if (_serialPort.BytesToRead > 0)
                    {
                        int bytes = _serialPort.BytesToRead;
                        byte[] buffer = new byte[bytes];
                        _serialPort.Read(buffer, 0, bytes);
                        Dispatcher.Invoke(() => Log("Получено: " + BitConverter.ToString(buffer)));

                        // Предполагаем, что кадр начинается с 0xFF и заканчивается двумя 0xFF
                        if (buffer.Length >= 6 &&
                            buffer[0] == 0xFF &&
                            buffer[buffer.Length - 2] == 0xFF &&
                            buffer[buffer.Length - 1] == 0xFF)
                        {
                            // Вместо извлечения адреса из запроса, используем фиксированный адрес из текстового поля
                            byte adr = FixedAddress;
                            byte cop = buffer[2];

                            if (cop == 0xA1)
                            {
                                // Ответ на запрос серийного номера
                                byte SN2 = 0x12, SN1 = 0x34, SN0 = 0x56;
                                byte[] responseData = new byte[] { adr, cop, SN2, SN1, SN0, 0x00 };
                                byte crc = CalcCRC(responseData);
                                byte[] response = new byte[] { 0xFF, adr, cop, SN2, SN1, SN0, crc, 0xFF, 0xFF };
                                _serialPort.Write(response, 0, response.Length);
                                Dispatcher.Invoke(() => Log("Отправлено (Серийный номер): " + BitConverter.ToString(response)));
                            }
                            else if (cop == 0xC2)
                            {
                                // Ответ на запрос веса
                                if (double.TryParse(txtWeight.Text, out double weightKg))
                                {
                                    byte[] bcdWeight = ConvertWeightToBCD(weightKg);
                                    // Формирование байта CON
                                    byte con = 0;
                                    if (weightKg < 0)
                                        con |= 0x80; // Знак минус
                                    // Режим взвешивания: всегда БРУТТО (D5 = 0)
                                    if (chkUspokoenie.IsChecked == true)
                                        con |= 0x10; // D4: успокоение
                                    if (chkPereg.IsChecked == true)
                                        con |= 0x08; // D3: перегруз
                                    // Позиция запятой – по умолчанию 1 (D2-D0 = 001)
                                    con |= 0x01;

                                    byte[] responseData = new byte[] { adr, cop, bcdWeight[0], bcdWeight[1], bcdWeight[2], con, 0x00 };
                                    byte crc = CalcCRC(responseData);
                                    byte[] response = new byte[] { 0xFF, adr, cop, bcdWeight[0], bcdWeight[1], bcdWeight[2], con, crc, 0xFF, 0xFF };
                                    _serialPort.Write(response, 0, response.Length);
                                    Dispatcher.Invoke(() => Log("Отправлено (Вес): " + BitConverter.ToString(response)));
                                }
                                else
                                {
                                    Dispatcher.Invoke(() => Log("Некорректный вес в поле ввода."));
                                }
                            }
                            // Здесь можно добавить обработку других команд, например, B4h
                        }
                    }
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() => Log("Ошибка: " + ex.Message));
                }
                Thread.Sleep(100);
            }
        }

        // Функция вычисления CRC для одного байта с учетом предыдущего значения CRC
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

        // Вычисление CRC для массива данных
        private byte CalcCRC(byte[] data)
        {
            byte crc = 0;
            foreach (var b in data)
                crc = CRCMaker(b, crc);
            return crc;
        }

        // Преобразование веса в BCD представление (3 байта, младшие байты)
        private byte[] ConvertWeightToBCD(double weight)
        {
            int intVal = (int)Math.Round(Math.Abs(weight) * 10); // умножаем на 10 для одного знака после запятой
            string s = intVal.ToString("D6");
            byte[] result = new byte[3];
            result[0] = (byte)(((s[4] - '0') << 4) | (s[5] - '0'));
            result[1] = (byte)(((s[2] - '0') << 4) | (s[3] - '0'));
            result[2] = (byte)(((s[0] - '0') << 4) | (s[1] - '0'));
            return result;
        }

        // Функция для логирования событий в текстовое поле
        private void Log(string message)
        {
            txtLog.AppendText(DateTime.Now.ToString("HH:mm:ss.fff") + " " + message + Environment.NewLine);
            txtLog.ScrollToEnd();
        }
    }
}
