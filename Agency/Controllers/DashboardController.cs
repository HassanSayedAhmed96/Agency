using Agency.Auth;
using Agency.Helpers;
using Agency.Models;
using Agency.ViewModel;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace Agency.Controllers
{
    [CustomAuthenticationFilter]
    public class DashboardController : Controller
    {
        AgencyDbContext db = new AgencyDbContext();
        // GET: Dashboard
        public ActionResult Index()
        {
            User currentUser = Session["user"] as User;
            UserViewModel userData = (from user in db.Users
                                      select new UserViewModel
                                      {
                                          id = user.id,
                                          user_name = user.user_name,
                                          full_name = user.full_name,
                                          password = user.password,
                                          type = user.type,
                                          phone1 = user.phone1,
                                          phone2 = user.phone2,
                                          imagePath = user.image,
                                          address1 = user.address1,
                                          address2 = user.address2,
                                          birthDate = user.birthDate,
                                          code = user.code,
                                          email = user.email,
                                          gender = user.gender,
                                          active = user.active,
                                      }).Where(u => u.id == currentUser.id).FirstOrDefault();

            var topSelling = (from hotel in db.Hotels
                              join eventHotel in db.EventHotels on hotel.id equals eventHotel.hotel_id
                              join res in db.Reservations on eventHotel.id equals res.event_hotel_id
                              join resDet in db.ReservationDetails on res.id equals resDet.reservation_id
                              select new ReservationViewModel
                              {
                                  hotel_id = hotel.id,
                                  hotel_name = hotel.name,
                                  total_nights = res.total_nights
                              }).GroupBy(g => g.hotel_id).ToList();

            string cs = ConfigurationManager.ConnectionStrings["DBContextADO"].ConnectionString;

            SqlConnection sql = new SqlConnection(cs);
            sql.Open();

            //Top Selling Hotels by Nights
            SqlCommand comm = new SqlCommand(@"select top 10 sum(total_nights) as total_nights, Hotels.name as hotel_name, Cities.name as city_name, Hotels.rate,(select top 1 HotelImages.path from HotelImages where HotelImages.hotel_id = Hotels.id) as hotel_image
											from Reservations 
                                            join EventHotels on Reservations.event_hotel_id = EventHotels.id
                                            join Hotels on EventHotels.hotel_id = Hotels.id
                                            join Cities on Cities.id = Hotels.city_id
                                            group by Hotels.id, Hotels.name, Cities.name, Hotels.rate", sql);
            SqlDataReader reader = comm.ExecuteReader();
            List<HotelViewModel> hotels  = new List<HotelViewModel>();

            while (reader.Read())
            {
                hotels.Add(new HotelViewModel { total_nights = reader["total_nights"].ToString().ToInt(), 
                    name = reader["hotel_name"].ToString(),
                    cityName = reader["city_name"].ToString(),
                    rating = Convert.ToDouble(reader["rate"].ToString()),
                    path = reader["hotel_image"].ToString(),
                });
            }
            reader.Close();

            //Latest Messages
            comm = new SqlCommand(@"select top 5 orginalChats.from_user,users.full_name,users.image, (select top 1 message from Chats where from_user = orginalChats.from_user order by created_at desc) as latest_message
                                    from chats as orginalChats
                                    inner join users on orginalChats.from_user = users.id
                                    where orginalChats.to_user = 1
                                    group by orginalChats.from_user,users.full_name,users.image", sql);
            reader = comm.ExecuteReader();
            List<ChatViewModel> LatestChats = new List<ChatViewModel>();

            while (reader.Read())
            {
                LatestChats.Add(new ChatViewModel
                {
                    strings_from_user = reader["full_name"].ToString(),
                    message = reader["latest_message"].ToString(),
                    image = reader["image"].ToString()
                });
            }
            reader.Close();
            List<ReservationLogViewModel> reservationLog = (from resLog in db.ReservationLogs
                                                   join user in db.Users on resLog.user_id equals user.id
                                                   select new ReservationLogViewModel
                                                   {
                                                        action = resLog.action,
                                                        description = resLog.description,
                                                        full_name = user.full_name,
                                                        created_at = resLog.created_at
                                                   }).Take(10).ToList();
            DashboardViewModel dashboardViewModel = new DashboardViewModel();
            dashboardViewModel.topSellingHotels = hotels;
            dashboardViewModel.logs = reservationLog;
            dashboardViewModel.userData = userData;
            dashboardViewModel.LatestChats = LatestChats;
            return View(dashboardViewModel);
        }

        public JsonResult revenueReservations()
        {
            string cs = ConfigurationManager.ConnectionStrings["DBContextADO"].ConnectionString;

            SqlConnection sql = new SqlConnection(cs);
            sql.Open();

            SqlCommand comm = new SqlCommand(@"select SUM(paid_amount) as collected,SUM(total_amount_after_tax-paid_amount) as balance , SUM(total_amount_after_tax -total_amount_from_vendor) as profit,CONCAT( YEAR(created_at),'-' ,MONTH(created_at)) as date from Reservations group by YEAR(created_at),MONTH(created_at)", sql);
            SqlDataReader reader = comm.ExecuteReader();
            List<int> yAxisCollected = new List<int>();
            List<int> yAxisBalance = new List<int>();
            List<int> yAxisProfit = new List<int>();
            List<string> xAxis = new List<string>();

            while (reader.Read())
            {
                yAxisCollected.Add(reader["collected"].ToString().ToInt());
                yAxisBalance.Add(reader["balance"].ToString().ToInt());
                yAxisProfit.Add(reader["profit"].ToString().ToInt());
                xAxis.Add(reader["date"].ToString());
            }
            reader.Close();
            List<double?> total_amount_after_tax_reservations = db.Reservations.Select(p => p.total_amount_after_tax).ToList();
            double? total_amount_after_tax = 0;
            if (total_amount_after_tax_reservations != null)
            {
                total_amount_after_tax = total_amount_after_tax_reservations.Sum();
            }

            List<double?> total_amount_from_vendor_reservations = db.Reservations.Select(p => p.total_amount_from_vendor).ToList();
            double? total_amount_from_vendor = 0;
            if (total_amount_from_vendor_reservations != null)
            {
                total_amount_from_vendor = total_amount_from_vendor_reservations.Sum();
            }

            List<double?> paid_amount_reservations = db.Reservations.Select(p => p.paid_amount).ToList();
            double? paid_amount = 0;
            if (paid_amount_reservations != null)
            {
                paid_amount = paid_amount_reservations.Sum();
            }

            List<double?> total_amount_from_vendor_finished_reservations = db.Reservations.Where(p => p.paid_amount == p.total_amount_after_tax).Select(p => p.total_amount_from_vendor).ToList();
            double? total_amount_from_vendor_finished = 0;
            if (total_amount_from_vendor_finished_reservations != null)
            {
                total_amount_from_vendor_finished = total_amount_from_vendor_finished_reservations.Sum();
            }


            List<double?> ttotal_amount_after_tax_finished_reservations = db.Reservations.Where(p => p.paid_amount == p.total_amount_after_tax).Select(p => p.total_amount_after_tax).ToList();
            double? total_amount_after_tax_finished = 0;
            if (total_amount_from_vendor_finished_reservations != null)
            {
                total_amount_after_tax_finished = ttotal_amount_after_tax_finished_reservations.Sum();
            }


            double? profit = total_amount_after_tax - total_amount_from_vendor;
            double? actualProfit = total_amount_after_tax_finished - total_amount_from_vendor_finished;

            double? collected = paid_amount;
            double? balance = total_amount_after_tax - paid_amount;
            
            double? profitPercentage = (profit / total_amount_after_tax) * 100;
            double? actualProfitPercentage = 0;
            if(actualProfit != 0 && total_amount_after_tax_finished != 0)
            {
                actualProfitPercentage = (actualProfit / total_amount_after_tax_finished) * 100;
            }

            double? collectedPercentage = (collected / total_amount_after_tax) * 100;
            double? balancePercentage = (balance / total_amount_after_tax) * 100;

            return Json(new { 
                xAxis = xAxis,
                yAxisCollected = yAxisCollected,
                yAxisBalance = yAxisBalance,
                yAxisProfit = yAxisProfit,
                profit = profit,
                actualProfit = actualProfit,
                collected = collected,
                balance = balance,
                profitPercentage = profitPercentage,
                actualProfitPercentage = actualProfitPercentage,
                collectedPercentage = collectedPercentage,
                balancePercentage = balancePercentage
           }, JsonRequestBehavior.AllowGet);
        }

        public JsonResult dailyReservations()
        {
            string cs = ConfigurationManager.ConnectionStrings["DBContextADO"].ConnectionString;

            SqlConnection sql = new SqlConnection(cs);
            sql.Open();

            SqlCommand comm = new SqlCommand(@"select SUM(paid_amount) as collected,SUM(total_amount_after_tax-paid_amount) as balance , SUM(total_amount_after_tax - total_amount_from_vendor) as profit,CONCAT( YEAR(created_at),'-' ,MONTH(created_at),'-' ,DAY(created_at)) as date from Reservations where created_at >= DATEADD(DAY,-7,GETDATE())  group by YEAR(created_at),MONTH(created_at),DAY(created_at)", sql);
            SqlDataReader reader = comm.ExecuteReader();
            List<int> yAxisCollected = new List<int>();
            List<int> yAxisBalance = new List<int>();
            List<int> yAxisProfit = new List<int>();
            List<string> xAxis = new List<string>();

            while (reader.Read())
            {
                yAxisCollected.Add(reader["collected"].ToString().ToInt());
                yAxisBalance.Add(reader["balance"].ToString().ToInt());
                yAxisProfit.Add(reader["profit"].ToString().ToInt());
                xAxis.Add(reader["date"].ToString());
            }
            reader.Close();
            
            return Json(new
            {
                xAxis = xAxis,
                yAxisCollected = yAxisCollected,
                yAxisBalance = yAxisBalance,
                yAxisProfit = yAxisProfit
            }, JsonRequestBehavior.AllowGet);
        }


    }
}