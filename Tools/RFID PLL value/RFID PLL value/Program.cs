using System;

namespace RFID_PLL_value
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("CWFrequency = 24MHz * MULTRAT / (DIVRAT * 4)");

            uint MULTRAT = 0;
            uint DIVRAT = 0;

            //for (DIVRAT = 0x1; DIVRAT <= 0xffff; DIVRAT++)
            //for (MULTRAT = 0x0; MULTRAT <= 0xffff; MULTRAT++)

            //DIVRAT = 0x0018;
            //MULTRAT = 0xE4e;

            for (DIVRAT = 1; DIVRAT <= 0xffff; DIVRAT++)
            for (MULTRAT = 1; MULTRAT <= 0xffff; MULTRAT++)
            {
                try
                    {
                        double freq = 24.0 * MULTRAT / (DIVRAT * 4);

                        if (freq >= 921 && freq <= 927)
                            Console.WriteLine("0x{0:X4}{1:X4} , {2:f40}", DIVRAT, MULTRAT, freq);
                    }
                    catch (Exception ex)
                    {

                    }
                }
        }
    }
}
