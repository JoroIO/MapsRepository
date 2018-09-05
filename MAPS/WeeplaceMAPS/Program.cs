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
        public static float alpha = 0.85F;
        public static float thrDist = 3.0F;
        public static float gammaOne = 0.75F;
        public static float gammaTwo = 0.25F;
        public static float sigmaOne = 0.75F;
        public static float sigmaTwo = 0.25F;
        public static float epsOne = 0.25F;
        public static float epsTwo = 0.75F;
        public static float error = 0.001F;
        public static List<string> distinctLocs = null;

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
                                                c.DateTime.Hour >= t1.Hour &&
                                                c.DateTime.Hour <= t2.Hour);

            return sum;
        }

        //Get the sum of the number of visits by the users in the dataset to the locations with the same category within the time interval (t1, t2)
        public static int VisitsByUsersAndCategory(List<CheckIn> checkInInfo, string placeId,
            DateTime t1, DateTime t2)
        {
            var checkIn = checkInInfo.First(c => c.PlaceId.Equals(placeId));
            var category = checkIn.Category;
            var sum = checkInInfo.Count(c =>    c.Category.Equals(category) &&
                                                c.DateTime.Hour >= t1.Hour &&
                                                c.DateTime.Hour <= t2.Hour);

            return sum;
        }

        //Get the sum of the number of visits by the users in the dataset to all the locations within the time interval (t1, t2)
        public static int VisitsByUsersAndLocation(List<CheckIn> checkInInfo, DateTime t1, DateTime t2)
        {
            var sum = checkInInfo.Count(c => c.DateTime.Hour >= t1.Hour &&
                                                c.DateTime.Hour <= t2.Hour);

            return sum;
        }

        //Get the sum of the number of visits by the users in the dataset to the locations within the threshold distance and within the time interval(t1, t2)
        public static int VisitsByUsersAndDistance(List<CheckIn> checkInInfo, string placeId,
            DateTime t1, DateTime t2)
        {
            var checkIn = checkInInfo.First(c => c.PlaceId.Equals(placeId));
            var latitude = checkIn.Latitude;
            var longitude = checkIn.Longitude;
            var sum = checkInInfo.Count(c =>!c.PlaceId.Equals(placeId) && 
                                            GeoCodeCalc.CalcDistance(latitude, longitude,
                                                c.Latitude, c.Longitude, GeoCodeCalcMeasurement.Kilometers) < thrDist &&
                                            c.DateTime.Hour >= t1.Hour &&
                                            c.DateTime.Hour <= t2.Hour);

            return sum;
        }

        //Calculate the category sensitive rank
        public static float CategorySensitiveFactor(List<CheckIn> checkInInfo, string placeId, DateTime t1, DateTime t2)
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

            var beta = gammaOne*((float)vu / (float)vuc) + gammaTwo*((float)vuc / (float)vul);

            return beta;
        }

        public static float FindCategoryRank(List<CheckIn> checkInInfo, string placeId, DateTime t1, DateTime t2)
        {
            var checkIn = checkInInfo.First(c => c.PlaceId.Equals(placeId));
            var category = checkIn.Category;
            var locationsSameCat = checkInInfo.Where(c => c.Category.Equals(category) &&
                                                        c.DateTime.Hour >= t1.Hour &&
                                                        c.DateTime.Hour <= t2.Hour).ToList();

            List<CheckIn> distinctLocs = locationsSameCat.GroupBy(c => c.PlaceId).Select(c => c.First()).ToList();

            float[] betas = new float[distinctLocs.Count];
            int position = 0;
            for (int i = 0; i < distinctLocs.Count; i++)
            {
                betas[i] = CategorySensitiveFactor(checkInInfo, distinctLocs[i].PlaceId, t1, t2);
                if (distinctLocs[i].PlaceId.Equals(placeId))
                {
                    position = i;
                }
            }

            var rank = GetCategoryRanks(distinctLocs, betas)[position, 1];
            //var rank = GetCategoryRank(distinctLocs, betas, position);

            return rank;
        }

        public static float[,] GetCategoryRanks(List<CheckIn> distinctLocs, float[] betas)
        {
            float[,] ranks = new float[distinctLocs.Count, 2];
            for (int i = 0; i < ranks.GetLength(0); i++)
            {
                ranks[i, 0] = 0.0F;
                ranks[i, 1] = 0.0F;
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
                    if (Math.Abs(ranks[i, 0] - ranks[i, 1]) >= error)
                    {
                        hasBigger = true;
                    }
                    ranks[i, 0] = ranks[i, 1];
                }

                for (int k = 0; k < ranks.GetLength(0); k++)
                {
                    Console.WriteLine( ranks[k, 1].ToString("N3"));
                }

                Console.WriteLine();
            } while (hasBigger);

            return ranks;
        }

        public static float GetCategoryRank(List<CheckIn> distinctLocs, float[] betas, int position)
        {
            return (alpha * betas[position] + 1 - alpha) / (2 - alpha);
        }

        public static float Sum(float[,] ranks, int i)
        {
            float sum = 0.0F;
            for (int j = 0; j < ranks.GetLength(0); j++)
            {
                if (j == i) continue;

                sum += ranks[j, 1]/ ranks.GetLength(0);
            }

            return sum;
        }

        //Calculate the distance sensitive rank
        public static float DistanceSensitiveFactor(List<CheckIn> checkInInfo, string placeId, DateTime t1, DateTime t2)
        {
            var vu = VisitsByUsers(checkInInfo, placeId, t1, t2);
            var vud = VisitsByUsersAndDistance(checkInInfo, placeId, t1, t2);

            if (vud == 0)
            {
                vud = 1;
            }

            var vul = VisitsByUsersAndLocation(checkInInfo, t1, t2);

            if (vul == 0)
            {
                vul = 1;
            }

            var theta = sigmaOne * ((float)vu / (float)vud) + sigmaTwo * ((float)vud / (float)vul);

            return theta;
        }

        public static float FindDistanceRank(List<CheckIn> checkInInfo, string placeId, DateTime t1, DateTime t2)
        {
            var checkIn = checkInInfo.First(c => c.PlaceId.Equals(placeId));
            var latitude = checkIn.Latitude;
            var longitude = checkIn.Longitude;
            var locationsDist = checkInInfo.Where(c => GeoCodeCalc.CalcDistance(latitude, longitude,
                                            c.Latitude, c.Longitude, GeoCodeCalcMeasurement.Kilometers) < thrDist &&
                                        c.DateTime.Hour >= t1.Hour &&
                                        c.DateTime.Hour <= t2.Hour).ToList();

            List<CheckIn> distinctLocs = locationsDist.GroupBy(c => c.PlaceId).Select(c => c.First()).ToList();

            float[] thetas = new float[distinctLocs.Count];
            int position = 0;
            for (int i = 0; i < distinctLocs.Count; i++)
            {
                thetas[i] = DistanceSensitiveFactor(checkInInfo, distinctLocs[i].PlaceId, t1, t2);
                if (distinctLocs[i].PlaceId.Equals(placeId))
                {
                    position = i;
                }
            }

            return GetDistanceRanks(distinctLocs, thetas)[position, 1];
        }

        public static float[,] GetDistanceRanks(List<CheckIn> distinctLocs, float[] thetas)
        {
            float[,] ranks = new float[distinctLocs.Count, 2];
            for (int i = 0; i < ranks.GetLength(0); i++)
            {
                ranks[i, 0] = 1.0F;
                ranks[i, 1] = 1.0F;
            }

            bool hasBigger = false;
            do
            {
                for (int i = 0; i < distinctLocs.Count; i++)
                {
                    ranks[i, 1] = alpha * thetas[i] + (1 - alpha) * Sum(ranks, i);
                }

                hasBigger = false;
                for (int i = 0; i < distinctLocs.Count; i++)
                {
                    if (Math.Abs(ranks[i, 0] - ranks[i, 1]) >= error)
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

        public static float GetDistanceRank(List<CheckIn> distinctLocs, float[] thetas, int position)
        {
            return (alpha * thetas[position] + 1 - alpha) / (2 - alpha);
        }

        public static float FindUnifiedRank(List<CheckIn> checkInInfo, string placeId, DateTime t1, DateTime t2,
            bool catRankncluded, bool distRankIncluded)
        {
            var catRank = catRankncluded ? FindCategoryRank(checkInInfo, placeId, t1, t2) : 0.0F;
            var distRank = distRankIncluded ? FindDistanceRank(checkInInfo, placeId, t1, t2) : 0.0F;

            return epsOne*catRank + epsTwo*distRank;
        }

        public static float CalculatePsiD(List<CheckIn> checkInInfo, string userId, string placeId)
        {
            var checkIn = checkInInfo.First(c => c.PlaceId.Equals(placeId));
            var latitude = checkIn.Latitude;
            var longitude = checkIn.Longitude;
            float nD = checkInInfo.Count(c => c.UserId.Equals(userId) &&
                                            GeoCodeCalc.CalcDistance(latitude, longitude,
                                            c.Latitude, c.Longitude, GeoCodeCalcMeasurement.Kilometers) < thrDist);
            var n = checkInInfo.Count(c => c.UserId.Equals(userId));
            var N = checkInInfo.Select(c => c.PlaceId).Distinct().Count();

            var locsByUser = checkInInfo.Where(c => c.UserId.Equals(userId));
            List<CheckIn> distinctLocs = locsByUser.GroupBy(c => c.PlaceId).Select(c => c.First()).ToList();

            var Nd = distinctLocs.Count(c => GeoCodeCalc.CalcDistance(latitude, longitude,
                                            c.Latitude, c.Longitude, GeoCodeCalcMeasurement.Kilometers) < thrDist);

            if (Nd == 0)
            {
                Nd = 1;
            }

            return (nD / n) * (float)(Math.Log(1 + N/Nd));
        }

        public static float CalculatePsiC(List<CheckIn> checkInInfo, string userId, string placeId)
        {
            var checkIn = checkInInfo.First(c => c.PlaceId.Equals(placeId));
            var category = checkIn.Category;
            float nC = checkInInfo.Count(c => c.UserId.Equals(userId) &&
                                               c.Category.Equals(category));
            var n = checkInInfo.Count(c => c.UserId.Equals(userId));
            var N = checkInInfo.Select(c => c.PlaceId).Distinct().Count();

            var tempList = checkInInfo.Where(c => c.Category.Equals(category));
            var Nc = tempList.Select(c => c.PlaceId).Distinct().Count();

            return (nC / n) * (float)(Math.Log(1 + N / Nc));
        }

        public static float CalculatePsiS(List<CheckIn> checkInInfo, Dictionary<string, List<string>> userFriends, string userId, string placeId)
        {
            float nS = GetVisitsByUsers(new List<string>(){ userId }, checkInInfo, userFriends);

            var n = checkInInfo.Count(c => c.UserId.Equals(userId));
            var N = checkInInfo.Select(c => c.PlaceId).Distinct().Count();

            var Ns = GetVisitsByUsers(userFriends.Keys.ToList(), checkInInfo, userFriends);

            return (nS / n) * (float)(Math.Log(1 + N / Ns));
        }

        public static int GetVisitsByUsers(List<string> users, List<CheckIn> checkInInfo,
            Dictionary<string, List<string>> userFriends)
        {
            var count = 0;
            //foreach (var user in users)
            //{
            //    var placesVisitedByUser = checkInInfo.Where(c => c.UserId.Equals(user)).Select(c => c.PlaceId).Distinct();

            //    List<string> userFriendsList = userFriends[user];

            //    foreach (var place in placesVisitedByUser)
            //    {
            //        foreach (var userFriend in userFriendsList)
            //        {
            //            if (checkInInfo.Count(c => c.UserId.Equals(userFriend) && c.PlaceId.Equals(place)) > 0)
            //            {
            //                count += checkInInfo.Count(c => c.UserId.Equals(user) && c.PlaceId.Equals(place));
            //                break;
            //            }
            //        }
            //    }
            //}
            foreach (var user in users)
            {
                var checkinsByUser = checkInInfo.Where(c => c.UserId.Equals(user));
                List<string> userFriendsList = userFriends[user];
                List <string> placesViseitedByFriends = new List<string>();
                foreach (var usr in userFriendsList)
                {
                    List<string> distinctPlacesForUser = checkInInfo.Where(c => c.UserId.Equals(usr)).GroupBy(c => c.PlaceId).Select(c => c.First().PlaceId).ToList();
                    placesViseitedByFriends = placesViseitedByFriends.Union(distinctPlacesForUser).ToList();
                }
                count += checkinsByUser.Count(c => placesViseitedByFriends.Contains(c.PlaceId));
            }
            return count;
        }

        //Get the sum of the number of visits by the current user in the dataset to the locations within the threshold distance and within the time interval(t1, t2)
        public static int VisitsByUserAndDistance(List<CheckIn> checkInInfo, string placeId, string userId,
            DateTime t1, DateTime t2)
        {
            var checkIn = checkInInfo.First(c => c.PlaceId.Equals(placeId));
            var latitude = checkIn.Latitude;
            var longitude = checkIn.Longitude;
            var sum = checkInInfo.Count(c => c.UserId.Equals(userId) &&
                                            GeoCodeCalc.CalcDistance(latitude, longitude,
                                                c.Latitude, c.Longitude, GeoCodeCalcMeasurement.Kilometers) < thrDist &&
                                            c.DateTime.Hour >= t1.Hour &&
                                            c.DateTime.Hour <= t2.Hour);

            return sum;
        }

        //Get the sum of the number of visits by the current user in the dataset to the locations with the same category within the time interval (t1, t2)
        public static int VisitsByUserAndCategory(List<CheckIn> checkInInfo, string placeId, string userId,
            DateTime t1, DateTime t2)
        {
            var checkIn = checkInInfo.First(c => c.PlaceId.Equals(placeId));
            var category = checkIn.Category;
            var sum = checkInInfo.Count(c => c.UserId.Equals(userId) &&
                                             c.Category.Equals(category) &&
                                             c.DateTime.Hour >= t1.Hour &&
                                             c.DateTime.Hour <= t2.Hour);

            return sum;
        }

        //Get the sum of the number of visits by the friends of the current user in the dataset to the current location with the same category within the time interval (t1, t2)
        public static int VisitsByUserFriends(List<CheckIn> checkInInfo, Dictionary<string, List<string>> userFriends, string placeId, string userId,
            DateTime t1, DateTime t2)
        {
            var userFriendsList = userFriends[userId];

            var sum = checkInInfo.Count(c => c.PlaceId.Equals(placeId) &&
                                             userFriendsList.Contains(c.UserId) &&
                                             c.DateTime.Hour >= t1.Hour &&
                                             c.DateTime.Hour <= t2.Hour);

            return sum;
        }

        public static float GetProbability(List<CheckIn> checkInInfo, Dictionary<string, List<string>> userFriends, 
            string userId, string placeId, DateTime t1, DateTime t2, bool catRankIncluded = true, bool distRankIncluded = true)
        {
            var P = FindUnifiedRank(checkInInfo, placeId, t1, t2, catRankIncluded, distRankIncluded)*
                    (CalculatePsiD(checkInInfo, userId, placeId)*
                     VisitsByUserAndDistance(checkInInfo, placeId, userId, t1, t2) +
                     CalculatePsiC(checkInInfo, userId, placeId)*
                     VisitsByUserAndCategory(checkInInfo, placeId, userId, t1, t2) +
                     CalculatePsiS(checkInInfo, userFriends, userId, placeId)*
                     VisitsByUserFriends(checkInInfo, userFriends, placeId, userId, t1, t2));

            return P;
        }

        public static void Main(string[] args)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            var checkInInfo = new List<CheckIn>();

            StreamReader csvreader = new StreamReader(Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory) + @"\weeplaces\weeplace_checkins.csv");
            string inputLine = "";
            var count = 0;
            var popularCategories = new Dictionary<string, int>();

            // ignore header row
            inputLine = csvreader.ReadLine();

            Console.WriteLine("Loading Weeplaces Dataset...");
            while ((inputLine = csvreader.ReadLine()) != null)
            {
                var checkIn = new CheckIn();
                string[] csvArray = inputLine.Split(',');
                checkIn.UserId = csvArray[0].Trim().Replace("\"", "");
                checkIn.PlaceId = csvArray[1];
                checkIn.DateTime = DateTime.Parse(csvArray[2], null, System.Globalization.DateTimeStyles.RoundtripKind);
                checkIn.Latitude = float.Parse(csvArray[3]);
                checkIn.Longitude = float.Parse(csvArray[4]);
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

            StreamReader csvUsersReader = new StreamReader(Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory) + @"\weeplaces\weeplace_friends.csv");
            inputLine = "";
            var userFrinedsCount = 0;
            var userFriends = new Dictionary<string, List<string>>();

            // ignore header row
            inputLine = csvUsersReader.ReadLine(); 

            Console.WriteLine("Loading Weeplaces Friends Dataset...");
            while ((inputLine = csvUsersReader.ReadLine()) != null)
            {
                string[] csvArray = inputLine.Split(',');
                var userId = csvArray[0].Trim().Replace("\"", "");
                var friendUserid = csvArray[1];

                if (!userFriends.ContainsKey(userId))
                {
                    List<string> frList = new List<string>();
                    frList.Add(friendUserid);
                    userFriends.Add(userId, frList);
                }
                else
                {
                    userFriends[userId].Add(friendUserid);
                }
                userFrinedsCount++;

                if (userFrinedsCount % 1000 == 0)
                {
                    Console.WriteLine(userFrinedsCount);
                }
            }

            Console.WriteLine("P = " + GetProbability(checkInInfo, userFriends, "fred-wilson", "jeffreys-grocery-and-luncheonette-new-york", DateTime.Parse("2010-10-25T12:03:10"), DateTime.Parse("2010-10-25T12:33:10"), true, false));
            distinctLocs = checkInInfo.GroupBy(c => c.PlaceId).Select(c => c.First().PlaceId).ToList();
            stopwatch.Stop();
            var elapsedTime = stopwatch.ElapsedMilliseconds;
            Console.WriteLine("Elapsed time (in seconds): {0}", elapsedTime / 1000);
        }
    }
}
