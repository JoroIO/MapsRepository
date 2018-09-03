using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MoreLinq;

namespace WeeplaceMAPS
{
    public class Program
    {
        public static double alpha = 0.85;
        public static double thrDist = 3.0;
        public static double gammaOne = 0.75;
        public static double gammaTwo = 0.25;
        public static double sigmaOne = 0.75;
        public static double sigmaTwo = 0.25;
        public static double epsOne = 0.25;
        public static double epsTwo = 0.75;

        public static List<string> GetDiffUsers(List<CheckIn> checkInInfo)
        {
            return checkInInfo.Select(c => c.UserId.Trim().Replace("\"", "")).Distinct().ToList();
        }

        public static List<string> GetDiffLocations(List<CheckIn> checkInInfo)
        {
            //return checkInInfo.Select(c => c.Latitude).Distinct().Select(c => c.Longitude).ToList();
            var test = checkInInfo.DistinctBy(c => new {c.Latitude, c.Longitude}).ToList();
            return new List<string>();
        }

        //Get the sum of the number of visits by the users in the dataset to the location within the time interval (t1, t2)
        public static int VisitsByUsers(List<CheckIn> checkInInfo, string placeId, DateTime t1, DateTime t2)
        {
            var sum = checkInInfo.Count(c =>    c.PlaceId.Equals(placeId) &&
                                                c.DateTime >= t1 &&
                                                c.DateTime <= t2);

            return sum;
        }

        //Get the sum of the number of visits by the users in the dataset to the locations with the same category within the time interval (t1, t2)
        public static int VisitsByUsersAndCategory(List<CheckIn> checkInInfo, string placeId,
            DateTime t1, DateTime t2)
        {
            var checkIn = checkInInfo.First(c => c.PlaceId.Equals(placeId));
            var category = checkIn.Category;
            var sum = checkInInfo.Count(c =>    c.Category.Equals(category) &&
                                                c.DateTime >= t1 &&
                                                c.DateTime <= t2);

            return sum;
        }

        //Get the sum of the number of visits by the users in the dataset to all the locations within the time interval (t1, t2)
        public static int VisitsByUsersAndLocation(List<CheckIn> checkInInfo, DateTime t1, DateTime t2)
        {
            var sum = checkInInfo.Count(c => c.DateTime >= t1 &&
                                                c.DateTime <= t2);

            return sum;
        }

        //Get the sum of the number of visits by the users in the dataset to the locations within the threshold distance and within the time interval(t1, t2)
        public static int VisitsByUsersAndDistance(List<CheckIn> checkInInfo, string placeId,
            DateTime t1, DateTime t2)
        {
            var checkIn = checkInInfo.First(c => c.PlaceId.Equals(placeId));
            var latitude = checkIn.Latitude;
            var longitude = checkIn.Longitude;
            var sum = checkInInfo.Count(c => GeoCodeCalc.CalcDistance(latitude, longitude,
                                            c.Latitude, c.Longitude, GeoCodeCalcMeasurement.Kilometers) < thrDist &&
                                        c.DateTime >= t1 &&
                                        c.DateTime <= t2);

            return sum;
        }

        //Calculate the category sensitive rank
        public static double CategorySensitiveFactor(List<CheckIn> checkInInfo, string placeId, DateTime t1, DateTime t2)
        {
            var vu = VisitsByUsers(checkInInfo, placeId, t1, t2);
            var vuc = VisitsByUsersAndCategory(checkInInfo, placeId, t1, t2);

            if (vuc == 0)
            {
                vuc = 1;
            }

            var vul = VisitsByUsersAndLocation(checkInInfo, t1, t2);

            if (vul == 0)
            {
                vul = 1;
            }

            var beta = gammaOne*((double)vu / (double)vuc) + gammaTwo*((double)vuc / (double)vul);

            return beta;
        }

        public static double FindCategoryRank(List<CheckIn> checkInInfo, string placeId, DateTime t1, DateTime t2)
        {
            var error = 0.001;
            var checkIn = checkInInfo.First(c => c.PlaceId.Equals(placeId));
            var category = checkIn.Category;
            var locationsSameCat = checkInInfo.Where(c => c.Category.Equals(category) &&
                                                        c.DateTime >= t1 &&
                                                        c.DateTime <= t2).ToList();

            List<CheckIn> distinctLocs = locationsSameCat.GroupBy(c => c.PlaceId).Select(c => c.First()).ToList();

            double[] betas = new double[distinctLocs.Count];
            int position = 0;
            for (int i = 0; i < distinctLocs.Count; i++)
            {
                betas[i] = CategorySensitiveFactor(checkInInfo, distinctLocs[i].PlaceId, t1, t2);
                if (distinctLocs[i].PlaceId.Equals(placeId))
                {
                    position = i;
                }
            }

            return GetRanks(distinctLocs, betas)[position, 1];
        }

        public static double[,] GetRanks(List<CheckIn> distinctLocs, double[] betas)
        {
            double[,] ranks = new double[distinctLocs.Count, 2];
            for (int i = 0; i < ranks.Length; i++)
            {
                ranks[i, 0] = 1.0;
                ranks[i, 1] = 1.0;
            }

            bool hasBigger = false;
            do
            {
                for (int i = 0; i < distinctLocs.Count; i++)
                {
                    ranks[i, 1] = alpha * betas[i] + (1 - alpha) * Sum(ranks, i);
                }

                hasBigger = false;
                for (int i = 0; i < distinctLocs.Count; i++)
                {
                    if (Math.Abs(ranks[i, 0] - ranks[i, 1]) >= 0.01)
                    {
                        hasBigger = true;
                    }
                    ranks[i, 0] = ranks[i, 1];
                }

                for (int k = 0; k < ranks.GetLength(0); k++)
                {
                    Console.Write(String.Format("{0:0.000}   ", ranks[k, 1]));
                }

                Console.WriteLine();
            } while (hasBigger);

            return ranks;
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

        //Calculate the distance sensitive rank
        public static double DistanceSensitiveFactor(List<CheckIn> checkInInfo, string placeId, DateTime t1, DateTime t2)
        {
            var vu = (double)VisitsByUsers(checkInInfo, placeId, t1, t2);
            var vud = (double)VisitsByUsersAndDistance(checkInInfo, placeId, t1, t2);

            if (vud.Equals(0.0))
            {
                return 0;
            }

            var vul = (double)VisitsByUsersAndLocation(checkInInfo, t1, t2);

            if (vul.Equals(0))
            {
                return 0;
            }

            var theta = sigmaOne * (vu / vud) + sigmaTwo * (vud / vul);

            return theta;
        }

        public static double FindDistanceRank(List<CheckIn> checkInInfo, string placeId, DateTime t1, DateTime t2)
        {
            return 0.0;
        }

        public static double FindUnifiedRank(List<CheckIn> checkInInfo, string placeId, DateTime t1, DateTime t2,
            bool catRankncluded, bool distRankIncluded)
        {
            var catRank = catRankncluded ? FindCategoryRank(checkInInfo, placeId, t1, t2) : 0.0;
            var distRank = distRankIncluded ? FindDistanceRank(checkInInfo, placeId, t1, t2) : 0.0;

            return epsOne*catRank + epsTwo*distRank;
        }

        public static double CalculatePsiD(List<CheckIn> checkInInfo, string userId, string placeId)
        {
            var checkIn = checkInInfo.First(c => c.PlaceId.Equals(placeId));
            var latitude = checkIn.Latitude;
            var longitude = checkIn.Longitude;
            double nD = checkInInfo.Count(c => c.UserId.Equals(userId) &&
                                            GeoCodeCalc.CalcDistance(latitude, longitude,
                                            c.Latitude, c.Longitude, GeoCodeCalcMeasurement.Kilometers) < thrDist);
            var n = checkInInfo.Count(c => c.UserId.Equals(userId));
            var N = checkInInfo.Select(c => c.PlaceId).Distinct().Count();

            var tempList = checkInInfo.Where(c => c.UserId.Equals(userId) &&
                                            GeoCodeCalc.CalcDistance(latitude, longitude,
                                            c.Latitude, c.Longitude, GeoCodeCalcMeasurement.Kilometers) < thrDist);
            var Nd = tempList.Select(c => c.PlaceId).Distinct().Count();

            return (nD / n) * (Math.Log(1 + N/Nd));
        }

        public static double CalculatePsiC(List<CheckIn> checkInInfo, string userId, string placeId)
        {
            var checkIn = checkInInfo.First(c => c.PlaceId.Equals(placeId));
            var category = checkIn.Category;
            double nC = checkInInfo.Count(c => c.UserId.Equals(userId) &&
                                               c.Category.Equals(category));
            var n = checkInInfo.Count(c => c.UserId.Equals(userId));
            var N = checkInInfo.Select(c => c.PlaceId).Distinct().Count();

            var tempList = checkInInfo.Where(c => c.Category.Equals(category));
            var Nc = tempList.Select(c => c.PlaceId).Distinct().Count();

            return (nC / n) * (Math.Log(1 + N / Nc));
        }

        public static double CalculatePsiS(List<CheckIn> checkInInfo, string userId, string placeId)
        {
            var checkIn = checkInInfo.First(c => c.PlaceId.Equals(placeId));
            var category = checkIn.Category;
            double nC = checkInInfo.Count(c => c.UserId.Equals(userId) &&
                                               c.Category.Equals(category));
            var n = checkInInfo.Count(c => c.UserId.Equals(userId));
            var N = checkInInfo.Select(c => c.PlaceId).Distinct().Count();

            var tempList = checkInInfo.Where(c => c.Category.Equals(category));
            var Nc = tempList.Select(c => c.PlaceId).Distinct().Count();

            return (nC / n) * (Math.Log(1 + N / Nc));
        }

        public static void Main(string[] args)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            var checkInInfo = new List<CheckIn>();

            StreamReader csvreader = new StreamReader(@"D:\Материали\Препоръчващи системи\Филтрирани теми за проекти\weeplaces\weeplace_checkins.csv");
            string inputLine = "";
            var count = 0;
            var popularCategories = new Dictionary<string, int>();

            Console.WriteLine("Loading Weeplaces Dataset...");
            while ((inputLine = csvreader.ReadLine()) != null)
            {
                if (count == 0)
                {
                    count++;
                    continue;
                }

                var checkIn = new CheckIn();
                string[] csvArray = inputLine.Split(',');
                checkIn.UserId = csvArray[0].Trim().Replace("\"", "");
                checkIn.PlaceId = csvArray[1];
                checkIn.DateTime = DateTime.Parse(csvArray[2], null, System.Globalization.DateTimeStyles.RoundtripKind);
                checkIn.Latitude = double.Parse(csvArray[3]);
                checkIn.Longitude = double.Parse(csvArray[4]);
                checkIn.City = csvArray[5];
                checkIn.Category = csvArray[6].Trim();

                if (checkIn.City.Equals("") ||
                    checkIn.Category.Equals(""))
                {
                    continue;
                }

                checkInInfo.Add(checkIn);
                count++;

                

                if (!popularCategories.ContainsKey(checkIn.Category))
                {
                    popularCategories.Add(checkIn.Category, 1);
                }
                else
                {
                    popularCategories[checkIn.Category]++;
                }

                if (count % 100000 == 0)
                {
                    Console.WriteLine(count);
                }
            }

            //var test1 = GetDiffUsers(checkInInfo);
            //var test2 = GetDiffLocations(checkInInfo);
            //var test3 = VisitsByUsers(checkInInfo, 40.733994, -74.001374,
            //    DateTime.Parse("2009-10-25T12:00:10", null, System.Globalization.DateTimeStyles.RoundtripKind),
            //    DateTime.Parse("2011-10-25T12:00:10", null, System.Globalization.DateTimeStyles.RoundtripKind));
            //var test4 = VisitsByUsersAndCategory(checkInInfo, 40.733994, -74.001374,
            //    DateTime.Parse("2009-10-25T12:00:10", null, System.Globalization.DateTimeStyles.RoundtripKind),
            //    DateTime.Parse("2011-10-25T12:00:10", null, System.Globalization.DateTimeStyles.RoundtripKind));

            //var placeId = "sinclairs-fleet-rec-center-bremerton";
            //var placeId = "starbucks-inside-great-wolf-lodge-thurston";
            //var checkInObj = checkInInfo.First(c => c.PlaceId.Equals(placeId));
            //var category = checkInObj.Category;
            //var catRankTest = CategorySensitiveFactor(checkInInfo, placeId, category,
            //    DateTime.Parse("2010-05-29T12:00:10", null, System.Globalization.DateTimeStyles.RoundtripKind),
            //    DateTime.Parse("2011-05-6T12:00:10", null, System.Globalization.DateTimeStyles.RoundtripKind));

            //var placeId = "blue-lagoon-grindavik";
            //var placeId = "our-lady-of-perpetual-help-schoolyard-brooklyn";
            //var checkInObj = checkInInfo.First(c => c.PlaceId.Equals(placeId));
            //var category = checkInObj.Category;
            //var catRankTest = CategorySensitiveFactor(checkInInfo, placeId,
            //    DateTime.Parse("2011-01-01T12:00:10", null, System.Globalization.DateTimeStyles.RoundtripKind),
            //    DateTime.Parse("2011-04-06T12:00:10", null, System.Globalization.DateTimeStyles.RoundtripKind));

            //var popularCategoriesSortedDesc = popularCategories.OrderByDescending(x => x.Value);
            //var popularCategoriesSortedAsc = popularCategories.OrderBy(x => x.Value);

            //var lessPopularCategories = popularCategories.Where(x => x.Value == 3);

            // Calculate Distance in Kilometers
            GeoCodeCalc.CalcDistance(47.8131545175277, -122.783203125, 42.0982224111897, -87.890625, GeoCodeCalcMeasurement.Kilometers);

            stopwatch.Stop();
            var elapsedTime = stopwatch.ElapsedMilliseconds;
            Console.WriteLine("Elapsed time (in seconds): {0}", elapsedTime / 1000);
        }
    }
}
