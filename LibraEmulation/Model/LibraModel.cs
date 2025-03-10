using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LibraEmulation.Model
{
    class LibraModel
    {
        // Сохраним фиксированный адрес, переданный из ViewModel
        private byte _fixedAddress;
        // Параметры, которые могут использоваться при формировании ответов (например, вес)
        private double _currentWeight;
        private bool _isUspokoenie;
        private bool _isPereg;
        private CounterModel _counterModel = new CounterModel();

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

        public byte[] CopA1Response()
        {
            byte cop = 0xA1;
            byte SN2 = 0x12, SN1 = 0x34, SN0 = 0x56;
            byte[] responseData = new byte[] { _fixedAddress, cop, SN2, SN1, SN0, 0x00 };
            byte crc = CalcCRC(responseData);
            byte[] response = CreateResponse(responseData, crc);

            return response;
        }

        public byte[] CopC2Response() {
            byte cop = 0xC2;
            byte[] bcdWeight = ConvertWeightToBCD(_currentWeight);
            byte con = 0;
            if (_currentWeight < 0)
                con |= 0x80;
            con |= 0x20; // D5=1 для режима НЕТТО
            if (_isUspokoenie) con |= 0x10;
            if (_isPereg) con |= 0x08;
            con |= 0x01; // позиция запятой
            byte[] responseData = new byte[] { _fixedAddress, cop, bcdWeight[0], bcdWeight[1], bcdWeight[2], con, 0x00 };
            byte crc = CalcCRC(responseData);
            byte[] response = CreateResponse(responseData, crc);
            return response;
        }

        public byte[] CopC3Response()
        {
            byte cop = 0xC3;
            byte[] bcdWeight = ConvertWeightToBCD(CurrentWeight);
            byte con = 0;
            if (CurrentWeight < 0)
                con |= 0x80;
            if (IsUspokoenie) con |= 0x10;
            if (IsPereg) con |= 0x08;
            con |= 0x01; // позиция запятой
            byte[] responseData = new byte[] { _fixedAddress, cop, bcdWeight[0], bcdWeight[1], bcdWeight[2], con, 0x00 };
            byte crc = CalcCRC(responseData);
            byte[] response = CreateResponse(responseData, crc);
            return response;
        }

        public byte[] CopBFResponse()
        {
            byte cop = 0xBF;
            byte status = 0;
            if (IsReweighing) status |= 0x80; // D7 = 1: режим перевешивания
            if (HasError) status |= 0x40; // D6 = 1: сообщение об ошибке
            if (IsStopMode) status |= 0x20; // D5 = 1: режим "СТОП"
            if (IsCycleComplete) status |= 0x10; // D4 = 1: завершён цикл набора отвеса
            if (IsPaused) status |= 0x08; // D3 = 1: режим ПАУЗА/БЛОКИРОВКА
            if (IsLoading) status |= 0x04; // D2 = 1: идет загрузка весового бункера
            if (IsUnloading) status |= 0x02; // D1 = 1: идет разгрузка весового бункера
            if (IsOnPass) status |= 0x01; // D0 = 1: включен режим "на проход"

            byte[] responseData = new byte[] { _fixedAddress, cop, status, 0x00 };
            byte crc = CalcCRC(responseData);
            byte[] response = CreateResponse(responseData, crc);
            return response;
        }

        public byte[] CopC8Response(byte nw)
        {
            byte cop = 0xC8;
            byte[] counterData;

            // Реализуем только счётчики с индексами 4 и 8:
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
                // Если NW не равен 4 или 8, возвращаем пустой массив (или можно вернуть ошибку)
                counterData = new byte[0];
            }

            // Формируем ответные данные: Adr, COP, NW, затем данные счётчика, затем CRC placeholder
            int len = 3 + counterData.Length + 1; // 3 байта: _fixedAddress, cop, nw; плюс данные; плюс 1 байт для CRC
            byte[] responseData = new byte[len];
            responseData[0] = _fixedAddress;
            responseData[1] = cop;
            responseData[2] = nw;
            Array.Copy(counterData, 0, responseData, 3, counterData.Length);
            responseData[responseData.Length - 1] = 0x00; // placeholder для CRC

            // Вычисляем CRC для сформированного массива данных
            byte crc = CalcCRC(responseData);
            responseData[responseData.Length - 1] = crc;

            // Оборачиваем ответ в разделители: начинаем с 0xFF и заканчиваем 0xFF, 0xFF
            byte[] response = new byte[responseData.Length + 3];
            response[0] = 0xFF;
            Array.Copy(responseData, 0, response, 1, responseData.Length);
            response[response.Length - 2] = 0xFF;
            response[response.Length - 1] = 0xFF;

            return response;
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
