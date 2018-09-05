using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestApplicationIterative
{
    class Program
    {
        public static double alpha = 0.15;
        static void Main(string[] args)
        {
            string[] loc =
            {
                "L1", "L2", "L3", "L4", "L5"
            };

            double[,] ranks = {{1.0, 1.0}, {1.0, 1.0}, {1.0, 1.0}, {1.0, 1.0}, {1.0, 1.0}};

            bool hasBigger = false;
            do
            {
                for (int i = 0; i < loc.Length; i++)
                {
                    ranks[i, 1] = 0.85 / (i+1) + alpha * Sum(ranks, i);
                }

                hasBigger = false;
                for (int i = 0; i < loc.Length; i++)
                {
                    if (Math.Abs(ranks[i, 0] - ranks[i, 1]) >= 0.001)
                    {
                        hasBigger = true;
                    }
                    ranks[i, 0] = ranks[i, 1];
                }

                for (int k = 0; k < ranks.GetLength(0); k++)
                {
                    Console.Write(String.Format("{0:0.000}   " ,ranks[k, 1]));
                }

                Console.WriteLine();
            } while (hasBigger);




            
        }

        public static double Sum(double[,] ranks, int i)
        {
            double sum = 0.0;
            for (int j = 0; j < ranks.GetLength(0); j++)
            {
                if (j == i) continue;

                sum += ranks[j, 1];
            }

            return sum;
        }

    }
}
