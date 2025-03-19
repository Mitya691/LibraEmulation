using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LibraEmulation.Model
{
    public class LibraModel
    {
        // Приватные поля – внутреннее состояние весов
        private byte _fixedAddress;
        private double _cumulativeWeight;
        private double _currentWeight;
        private double _performance;
        private bool _isUspokoenie;
        private bool _isPereg;
        private int _libraNumber;

        // Приватный экземпляр для работы со счетчиками
        private CounterModel _counterModel = new CounterModel();

        // Конструктор для инициализации параметров
        public LibraModel(int libraNumber)
        {
            _libraNumber = libraNumber;
        }
        // Приватные поля для статуса весов:
        private bool _isReweighing;
        private bool _hasError;
        private bool _isStopMode;
        private bool _isCycleComplete;
        private bool _isPaused;
        private bool _isLoading;
        private bool _isUnloading;
        private bool _isOnPass;

        // Публичные свойства для бизнес-логики (если нужно читать/изменять эти статусы извне)
        public int LibraNumber
        {
            get => _libraNumber;
            set => _libraNumber = value;
        }

        public byte FixedAddress
        {
            get => _fixedAddress;
            set => _fixedAddress = value;
        }

        public double CurrentWeight
        {
            get => _currentWeight;
            set => _currentWeight = value;
        }

        public double CumulativeWeight
        {
            get => _cumulativeWeight;
            set => _cumulativeWeight = value;
        }

        public double Performance
        {
            get => _performance;
            set => _performance = value;
        }

        public bool IsUspokoenie
        {
            get => _isUspokoenie;
            set => _isUspokoenie = value;
        }

        public bool IsPereg
        {
            get => _isPereg;
            set => _isPereg = value;
        }

        public bool IsReweighing
        {
            get => _isReweighing;
            set => _isReweighing = value;
        }

        public bool HasError
        {
            get => _hasError;
            set => _hasError = value;
        }

        public bool IsStopMode
        {
            get => _isStopMode;
            set => _isStopMode = value;
        }

        public bool IsCycleComplete
        {
            get => _isCycleComplete;
            set => _isCycleComplete = value;
        }

        public bool IsPaused
        {
            get => _isPaused;
            set => _isPaused = value;
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => _isLoading = value;
        }

        public bool IsUnloading
        {
            get => _isUnloading;
            set => _isUnloading = value;
        }

        public bool IsOnPass
        {
            get => _isOnPass;
            set => _isOnPass = value;
        }

        // Метод формирования ответа для команды A1 (запрос серийного номера)
        public byte[] GetCopA1Response()
        {
            byte cop = 0xA1;
            byte SN2 = 0x12, SN1 = 0x34, SN0 = 0x56;
            byte[] responseData = new byte[] { _fixedAddress, cop, SN2, SN1, SN0, 0x00 };
            byte crc = CalcCRC(responseData);
            return CreateResponse(responseData, crc);
        }

        // Метод формирования ответа для команды C2 (запрос веса НЕТТО)
        public byte[] GetCopC2Response()
        {
            byte cop = 0xC2;
            byte[] bcdWeight = ConvertWeightToBCD(_currentWeight);
            byte con = 0;
            if (_currentWeight < 0)
                con |= 0x80;
            con |= 0x20; // D5 = 1 для режима НЕТТО
            if (_isUspokoenie) con |= 0x10;
            if (_isPereg) con |= 0x08;
            con |= 0x01; // позиция запятой
            byte[] responseData = new byte[] { _fixedAddress, cop, bcdWeight[0], bcdWeight[1], bcdWeight[2], con, 0x00 };
            byte crc = CalcCRC(responseData);
            return CreateResponse(responseData, crc);
        }

        // Метод формирования ответа для команды C3 (запрос веса БРУТТО)
        public byte[] GetCopC3Response()
        {
            byte cop = 0xC3;
            byte[] bcdWeight = ConvertWeightToBCD(_currentWeight);
            byte con = 0;
            if (_currentWeight < 0)
                con |= 0x80;
            if (_isUspokoenie) con |= 0x10;
            if (_isPereg) con |= 0x08;
            con |= 0x01; // позиция запятой
            byte[] responseData = new byte[] { _fixedAddress, cop, bcdWeight[0], bcdWeight[1], bcdWeight[2], con, 0x00 };
            byte crc = CalcCRC(responseData);
            return CreateResponse(responseData, crc);
        }

        // Метод формирования ответа для команды BF (состояние весов)
        public byte[] GetCopBFResponse()
        {
            byte cop = 0xBF;
            byte status = 0;
            if (_isReweighing) status |= 0x80;    // D7
            if (_hasError) status |= 0x40;    // D6
            if (_isStopMode) status |= 0x20;    // D5
            if (_isCycleComplete) status |= 0x10; // D4
            if (_isPaused) status |= 0x08;    // D3
            if (_isLoading) status |= 0x04;    // D2
            if (_isUnloading) status |= 0x02;    // D1
            if (_isOnPass) status |= 0x01;    // D0

            byte[] responseData = new byte[] { _fixedAddress, cop, status, 0x00 };
            byte crc = CalcCRC(responseData);
            return CreateResponse(responseData, crc);
        }

        // Метод формирования ответа для команды C8 (счётчики)
        public byte[] GetCopC8Response(byte nw)
        {
            byte cop = 0xC8;
            byte[] counterData;
            // Поддерживаем только счётчики с индексами 4 и 8
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
                counterData = new byte[0];
            }

            int len = 3 + counterData.Length + 1; // Adr, COP, NW, данные, CRC
            byte[] responseData = new byte[len];
            responseData[0] = _fixedAddress;
            responseData[1] = cop;
            responseData[2] = nw;
            Array.Copy(counterData, 0, responseData, 3, counterData.Length);
            responseData[responseData.Length - 1] = 0x00; // CRC placeholder
            byte crc = CalcCRC(responseData);
            responseData[responseData.Length - 1] = crc;
            return CreateResponse(responseData, crc);
        }

        // Метод формирования ответа для команды DF (управление процессом перевешивания)
        public byte[] GetCopDFResponse(byte cmd)
        {
            // COP = DFh
            byte cop = 0xDF;
            byte[] responseData = new byte[] { _fixedAddress, cop, cmd, 0x00 };
            byte crc = CalcCRC(responseData);
            responseData[responseData.Length - 1] = crc;
            return CreateResponse(responseData, crc);
        }

        // Вспомогательные методы для расчёта CRC, создания ответа и преобразования веса в BCD.
        private byte[] CreateResponse(byte[] data, byte crc)
        {
            byte[] response = new byte[data.Length + 3];
            response[0] = 0xFF;
            Array.Copy(data, 0, response, 1, data.Length);
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