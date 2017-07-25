using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace DivideArray
{
    class Program
    {

        static void Main(string[] args)
        {
            double[] dbarr = { 100,110,120,50,40,70,50,60,30,108,120,130,50,80,70,125,140,90,
                             100,110,120,50,40,70,50,60,30,108,120,130,50,80,70,125,140,90,
                             100,110,120,50,40,70,50,60,30,108,120,130,50,80,70,125,140,90};

            double[] dbarr1 = { 100,110,120,50,40,70,50,60,30,120,130,50,80,70,140,90,
                             100,120,50,40,70,60,30,108,130,50,80,70,140,90,
                             100,120,50,40,70,50,60,108,120,130,50,80,70,125,140,90};


            double[] dbarr2 = { 100,110,120,50,40,70,50,60,30,120,130,50,80,70,140,90,
                             100,120,50,40,70,60,30,108,130,50,80,70,140,90,
                             100,120,50,40,70,50,60,108,120,130,50};

            double[] dbarr3 = {106.88,104.91,99.89,98.08,123.25,118.37,114.43,95.27,113.41,96.27,207.91,91.13,110.23,109.80,110.19,110.39,110.24,
                                  75.11,82.60,98.06,99.78,99.85,100.31,103.39};


            List<double> arraylist = new List<double>();
            int n = 60;
            Random random = new Random();
            for (int i = 0; i < n; i++)
            {

                double m = 60 + 100 * random.NextDouble();
                arraylist.Add(Math.Round(m, 2));
            }

            List<double> arraylist1 = new List<double>();
            arraylist1.AddRange(dbarr3);

            DivideBigComponent(arraylist1);

            Console.ReadKey();

        }

        static void DivideBigComponent(List<double> arraylist)
        {
            int count = arraylist.Count;
            double total = 0;
            for (int i = 0; i < arraylist.Count; i++)
                total += arraylist[i];

            int step_min = (int)(total / 1800) + 1;
            int step_max = (int)(total / 900);
            int step = step_min;
            bool isSucceeded = false;

            Console.WriteLine("电缆总长度为" + total +"m, 可分段数为" + step_min + "~" + step_max + ";\n");

            while (!isSucceeded && step <= step_max)
            {
                Console.WriteLine("将进行大段数为" + step +"的分组：\n");
                int position = 0;
                for (int i = 0; i < step; i++)
                {
                    List<int> index = null;
                    List<double> subArray = null;
                    //int position = count / step * i;
                    if (i == step - 1)
                    {
                        subArray = arraylist.GetRange(position, count - position);
                    }
                    else
                    {
                        if (position + count / step > count)
                            break;
                        subArray = arraylist.GetRange(position, count / step);
                    }
                    index = DivideArray(subArray);

                    int temp = count / step;
                    while (index.Count != 4 && i != step - 1 && temp < count - position)
                    {
                        subArray = arraylist.GetRange(position, ++temp);
                        index = DivideArray(subArray);
                    }
                    position += temp;

                    if (i == step - 1 && index.Count == 4)
                        isSucceeded = true;

                    Console.WriteLine("第" + (i + 1).ToString() + "段分组结果为：\n");
                    printGroup(index, subArray);
                }

                if (!isSucceeded)
                {
                    Console.WriteLine("分组失败，将调整大段段数重新分组：\n");
                    Console.WriteLine("<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<\n");
                }
                else
                    Console.WriteLine("恭喜你分组成功！撒花撒花:)");


                step++;
            }

        }

        static void printGroup(List<int> index, List<double> arraylist)
        {

            List<double> compare = new List<double>();
            for (int i = 0; i < index.Count - 1; i++)
            {
                string result = "";
                double sum = 0;
                for (int j = index[i]; j < index[i + 1]; j++)
                {
                    result += arraylist[j].ToString() + " ";
                    sum += arraylist[j];
                }
                result += "sum = " + sum.ToString();
                Console.WriteLine(result + "\n");
                compare.Add(sum);

            }
            Console.WriteLine("err = " + getbaifenbi(compare).ToString() + "\n");

        }

        public static double getbaifenbi(List<double> compare)
        {
            double max = compare[0];
            double min = compare[0];
            foreach (double x in compare)
            {
                if (x > max) max = x;
                if (x < min) min = x;
            }

            return (max - min) / min;

        }

        static List<int> DivideArray(List<double> arraylist)
        {

            List<double> sum_array = new List<double>();
            List<int> list_index = new List<int>();
            int count = arraylist.Count;

            double range = 600;

            while (!isValidDivision(sum_array) && range >= 300)
            {
                double sum = 0;
                sum_array.Clear();
                list_index.Clear();
                list_index.Add(0);
                for (int i = 0; i < count; i++)
                {
                    sum += arraylist[i];
                    if (sum > range)
                    {
                        list_index.Add(i);
                        sum_array.Add(sum - arraylist[i]);
                        sum = arraylist[i];
                    }
                    if (i == count - 1)
                        sum_array.Add(sum);
                }
                range -= 10;
            }

            list_index.Add(count);

            return list_index;
        }

        static bool isValidDivision(List<double> sum_array)
        {
            if (sum_array.Count != 3)
                return false;

            double max = sum_array[0], min = sum_array[0];
            for (int i = 1; i < sum_array.Count; i++)
            {
                if (sum_array[i] > max)
                    max = sum_array[i];
                if (sum_array[i] < min)
                    min = sum_array[i];
            }

            if (min * 1.3 < max)
                return false;
            return true;
        }
    }
}