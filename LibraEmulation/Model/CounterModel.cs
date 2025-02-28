using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LibraEmulation.Model
{
    public class CounterModel
    {
        // Примерное свойство для хранения значения счетчика
        public double TotalWeight { get; set; }

        // Метод преобразует значение счетчика в 5-байтовое BCD-представление.
        public byte[] GetCounterBCD(int counterIndex)
        {
            // Для примера реализуем фиксированное значение для счетчиков 4 и 8.
            // Например, если счетчик 4 представляет суммарный вес "С." и равен 51200 кг:
            if (counterIndex == 4)
            {
                int value = 51200; // фиксированное значение для счетчика 4
                string s = value.ToString("D10"); // 10 цифр
                byte[] result = new byte[5];
                for (int i = 0; i < 5; i++)
                {
                    int highIndex = (4 - i) * 2;
                    int lowIndex = highIndex + 1;
                    byte high = (byte)(s[highIndex] - '0');
                    byte low = (byte)(s[lowIndex] - '0');
                    result[i] = (byte)((high << 4) | low);
                }
                return result;
            }
            // Если счетчик 8 представляет, например, текущую производительность "Р." в тоннах/час, зафиксированное значение:
            else if (counterIndex == 8)
            {
                int value = 75; // допустим, 75 тонн/час, преобразуем в 10-значное число
                string s = value.ToString("D10");
                byte[] result = new byte[5];
                for (int i = 0; i < 5; i++)
                {
                    int highIndex = (4 - i) * 2;
                    int lowIndex = highIndex + 1;
                    byte high = (byte)(s[highIndex] - '0');
                    byte low = (byte)(s[lowIndex] - '0');
                    result[i] = (byte)((high << 4) | low);
                }
                return result;
            }
            else
            {
                return new byte[0];
            }
        }
    }
}
