﻿using Agency.Auth;
using Agency.Helpers;
using Agency.Models;
using Agency.Services;
using Agency.ViewModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace Agency.Controllers
{
    [CustomAuthenticationFilter]
    public class ReservationDetailController : Controller
    {
        AgencyDbContext db = new AgencyDbContext();

        // GET: ReservationDetail
        public ActionResult Index(int? id)
        {
            if (Request.IsAjaxRequest())
            {
                var draw = Request.Form.GetValues("draw").FirstOrDefault();
                var start = Request.Form.GetValues("start").FirstOrDefault();
                var length = Request.Form.GetValues("length").FirstOrDefault();
                var searchValue = Request.Form.GetValues("search[value]").FirstOrDefault();
                int pageSize = length != null ? Convert.ToInt32(length) : 0;
                int skip = start != null ? Convert.ToInt32(start) : 0;

                // Getting all data    
                var resDetailData = (from resDetail in db.ReservationDetails
                               join res in db.Reservations on resDetail.reservation_id equals res.id
                               join client in db.Clients on resDetail.client_id equals client.id
                               select new ReservationDetailViewModel
                               {
                                   id = resDetail.id,
                                   amount = resDetail.amount,
                                   tax = resDetail.tax,
                                   room_type = resDetail.room_type,
                                   reservation_from = resDetail.reservation_from,
                                   reservation_to = resDetail.reservation_to,
                                   no_of_days = resDetail.no_of_days,
                                   active = resDetail.active,
                                   client_id = resDetail.client_id,
                                   client_first_name = client.first_name,
                                   client_last_name = client.last_name,
                                   reservation_id = resDetail.reservation_id,
                                   currency = res.currency
                               }).Where(r => r.reservation_id == id);

                //Search    
                if (!string.IsNullOrEmpty(searchValue))
                {
                    resDetailData = resDetailData.Where(m => m.client_first_name.ToLower().Contains(searchValue.ToLower()) || m.amount.ToString().ToLower().Contains(searchValue.ToLower()) ||
                     m.client_last_name.ToLower().Contains(searchValue.ToLower()) || m.no_of_days.ToString().ToLower().Contains(searchValue.ToLower()));
                }

                //total number of rows count     
                var displayResult = resDetailData.OrderByDescending(u => u.id).Skip(skip)
                     .Take(pageSize).ToList();
                var totalRecords = resDetailData.Count();

                return Json(new
                {
                    draw = draw,
                    recordsTotal = totalRecords,
                    recordsFiltered = totalRecords,
                    data = displayResult

                }, JsonRequestBehavior.AllowGet);

            }
            ViewBag.id = id;
            
            return View();
        }
        public JsonResult saveReservation(ReservationDetailViewModel detailViewModel)
        {
            if(detailViewModel.id == 0)
            {
                ReservationDetail detail = AutoMapper.Mapper.Map<ReservationDetailViewModel, ReservationDetail>(detailViewModel);

                Reservation reservation = db.Reservations.Find(detail.reservation_id);

                EventHotel eventHotel = db.EventHotels.Find(reservation.event_hotel_id);

                Event event_row = db.Events.Find(eventHotel.event_id);

                double? room_price = 0;
                double? vendor_room_price = 0;

                if (detailViewModel.room_type == 1)
                {
                    room_price = reservation.single_price;
                    vendor_room_price = reservation.vendor_single_price;
                }
                else
                {
                    room_price = reservation.double_price;
                    vendor_room_price = reservation.vendor_douple_price;
                }
                int Days = (detailViewModel.reservation_to - detailViewModel.reservation_from).Days + 1;
                detail.no_of_days = Days;
                var amount = (room_price * Days); //* (100 - reservation.advance_reservation_percentage)) / 100;  //event_row
                var vendor_amount = (vendor_room_price * Days) ;//* (100 - reservation.advance_reservation_percentage)) / 100;  //event_row
                detail.tax = amount * (reservation.tax/100);
                detail.amount = amount;
                detail.amount_after_tax = amount + detail.tax;
                detail.created_at = DateTime.Now;
                detail.created_by = Session["id"].ToString().ToInt();
                detail.active = 1;
                db.ReservationDetails.Add(detail);
                db.SaveChanges();

                Client client = new Client();
                client.first_name = detailViewModel.client_first_name;
                client.last_name = detailViewModel.client_last_name;
                client.company_id = reservation.company_id;
                client.active = 1;
                client.created_at = DateTime.Now;
                client.created_by = Session["id"].ToString().ToInt();
                db.Clients.Add(client);
                db.SaveChanges();

                List<ReservationDetail> reservationDetails = db.ReservationDetails.Where(r => r.reservation_id == detailViewModel.reservation_id).ToList();
                if (reservationDetails.Count() != 0)
                {
                    DateTime minDate = reservationDetails.Select(s => s.reservation_from).Min();
                    DateTime maxDate = reservationDetails.Select(s => s.reservation_to).Max();

                    reservation.check_in = minDate;
                    reservation.check_out = maxDate;
                }

                ReservationViewModel updatedTotals = ReservationService.calculateTotalandVendor(reservation.id);

                reservation.total_amount = updatedTotals.total_amount;
                reservation.total_amount_after_tax = updatedTotals.total_amount_after_tax;
                reservation.total_amount_from_vendor = updatedTotals.total_amount_from_vendor;
                reservation.tax_amount = updatedTotals.tax_amount;

                //reservation.tax_amount= reservation.tax_amount == null ? 0 : reservation.tax_amount;
                //reservation.total_amount_from_vendor = reservation.total_amount_from_vendor == null ? 0 : reservation.total_amount_from_vendor;
                //reservation.total_amount_from_vendor += vendor_amount;
                //reservation.tax_amount += detail.tax;
                //reservation.total_amount_after_tax += detail.amount_after_tax;
                //reservation.total_amount += detail.amount;
                reservation.updated_at = DateTime.Now;
                reservation.updated_by = Session["id"].ToString().ToInt();
                db.SaveChanges();

                detail.client_id = client.id;
                db.SaveChanges();
            }
            else
            {
                ReservationDetail detail = db.ReservationDetails.Find(detailViewModel.id);

                Reservation reservation = db.Reservations.Find(detail.reservation_id);

                EventHotel eventHotel = db.EventHotels.Find(reservation.event_hotel_id);

                Event event_row = db.Events.Find(eventHotel.event_id);

                detail.reservation_from = detailViewModel.reservation_from;
                detail.reservation_to = detailViewModel.reservation_to;
                detail.room_type = detailViewModel.room_type;

                double? room_price = 0;
                double? vendor_room_price = 0;

                if (detailViewModel.room_type == 1)
                {
                    room_price = reservation.single_price;
                    vendor_room_price = reservation.vendor_single_price;
                }
                else
                {
                    room_price = reservation.double_price;
                    vendor_room_price = reservation.vendor_douple_price;
                }
                int Days = (detailViewModel.reservation_to - detailViewModel.reservation_from).Days + 1;
                detail.no_of_days = Days;
                var amount = (room_price * Days); //* (100 - reservation.advance_reservation_percentage)) / 100;  //event_row
                var vendor_amount = (vendor_room_price * Days);//* (100 - reservation.advance_reservation_percentage)) / 100;  //event_row
                detail.tax = amount * (reservation.tax / 100);
                detail.amount = amount;
                detail.amount_after_tax = amount + detail.tax;

                detail.updated_at = DateTime.Now;
                detail.updated_by = Session["id"].ToString().ToInt();
                db.SaveChanges();

                Client client = db.Clients.Find(detail.client_id);
                client.first_name = detailViewModel.client_first_name;
                client.last_name = detailViewModel.client_last_name;
                client.updated_at = DateTime.Now;
                client.updated_by = Session["id"].ToString().ToInt();
                db.SaveChanges();

                List<ReservationDetail> reservationDetails = db.ReservationDetails.Where(r => r.reservation_id == detailViewModel.reservation_id).ToList();
                if (reservationDetails.Count() != 0)
                {
                    DateTime minDate = reservationDetails.Select(s => s.reservation_from).Min();
                    DateTime maxDate = reservationDetails.Select(s => s.reservation_to).Max();

                    reservation.check_in = minDate;
                    reservation.check_out = maxDate;
                }

                ReservationViewModel updatedTotals = ReservationService.calculateTotalandVendor(reservation.id);

                reservation.total_amount = updatedTotals.total_amount;
                reservation.total_amount_after_tax = updatedTotals.total_amount_after_tax;
                reservation.total_amount_from_vendor = updatedTotals.total_amount_from_vendor;
                reservation.tax_amount = updatedTotals.tax_amount;

                //reservation.tax += detail.tax;
                //reservation.total_amount += detail.amount;
                reservation.updated_at = DateTime.Now;
                reservation.updated_by = Session["id"].ToString().ToInt();
                db.SaveChanges();
            }
           
            return Json(new { message = "done" }, JsonRequestBehavior.AllowGet);

        }
        public JsonResult deleteReservation(int id)
        {
            ReservationDetail detail = db.ReservationDetails.Find(id);

            Reservation reservation = db.Reservations.Find(detail.reservation_id);
           
            double? amount = detail.amount;
            double? tax = detail.tax;

            db.ReservationDetails.Remove(detail);
            db.SaveChanges();

            ReservationViewModel updatedTotals = ReservationService.calculateTotalandVendor(reservation.id);

            reservation.total_amount = updatedTotals.total_amount;
            reservation.total_amount_after_tax = updatedTotals.total_amount_after_tax;
            reservation.total_amount_from_vendor = updatedTotals.total_amount_from_vendor;
            reservation.tax_amount = updatedTotals.tax_amount;

           
            reservation.updated_at = DateTime.Now;
            reservation.updated_by = Session["id"].ToString().ToInt();
            db.SaveChanges();

            return Json(new { message = "done" }, JsonRequestBehavior.AllowGet);

        }
        public ActionResult Itinirary(int id)
        {
          
            ReservationViewModel resData = (from res in db.Reservations
                           join company in db.Companies on res.company_id equals company.id
                           join event_hotel in db.EventHotels on res.event_hotel_id equals event_hotel.id
                           join even in db.Events on event_hotel.event_id equals even.id
                           join hotel in db.Hotels on event_hotel.hotel_id equals hotel.id
                           //join locationHotel in db.HotelLocations on hotel.id equals locationHotel.hotel_id
                           //join location in db.Locations on locationHotel.location_id equals location.id
                           select new ReservationViewModel
                           {
                               id = res.id,
                               total_amount = res.total_amount,
                               currency = res.currency,
                               tax = res.tax,
                               financial_advance = res.financial_advance,
                               financial_advance_date = res.financial_advance_date,
                               financial_due = res.financial_due,
                               financial_due_date = res.financial_due_date,
                               status = res.status,
                               single_price = res.single_price,
                               double_price = res.double_price,
                               triple_price = res.triple_price,
                               quad_price = res.quad_price,
                               vendor_single_price = res.vendor_single_price,
                               vendor_douple_price = res.vendor_douple_price,
                               vendor_triple_price = res.vendor_triple_price,
                               vendor_quad_price = res.vendor_quad_price,
                               vendor_id = res.vendor_id,
                               active = res.active,
                               created_at = res.created_at,
                               company_name = company.name,
                               phone = company.phone,
                               email = company.email,
                               company_id = res.company_id,
                               event_hotel_id = event_hotel.id,
                               hotel_id = hotel.id,
                               hotel_name = hotel.name,
                               hotel_rate = hotel.rate,
                               hotel_address = hotel.address,
                               reservations_officer_name = res.reservations_officer_name,
                               reservations_officer_email = res.reservations_officer_email,
                               reservations_officer_phone = res.reservations_officer_phone,
                               opener = res.opener,
                               closer = res.closer,
                               check_in = res.check_in,
                               check_out = res.check_out,
                               string_check_in = res.check_in.ToString(),
                               string_check_out = res.check_out.ToString(),
                               //total_nights = res.total_nights,
                               //total_rooms = res.total_rooms,
                               profit = res.profit,
                               shift = res.shift,
                               event_name = even.title,
                               event_tax = even.tax,
                               event_single_price = event_hotel.single_price,
                               event_double_price = event_hotel.double_price,
                               event_triple_price = event_hotel.triple_price,
                               event_quad_price = event_hotel.quad_price,
                               event_vendor_single_price = event_hotel.vendor_single_price,
                               event_vendor_double_price = event_hotel.vendor_douple_price,
                               event_vendor_triple_price = event_hotel.vendor_triple_price,
                               event_vendor_quad_price = event_hotel.vendor_quad_price,
                               total_price = db.ReservationDetails.Where(r => r.reservation_id == res.id).Select(p => p.amount).Sum(),
                               paid_amount = res.paid_amount,
                               total_amount_after_tax = res.total_amount_after_tax,
                               total_amount_from_vendor = res.total_amount_from_vendor,
                               advance_reservation_percentage = res.advance_reservation_percentage,
                               tax_amount = res.tax_amount,
                               total_rooms = db.ReservationDetails.Where(tr => tr.reservation_id == id).ToList().Count(),
                               total_nights = db.ReservationDetails.Where(tr => tr.reservation_id == id).Select(tn => tn.no_of_days).Sum(),
                               hotelFacilities = db.HotelFacilities.Where(f => f.hotel_id == hotel.id).Select(fh => fh.name).ToList(),
                               //profit = calculateProfit(res.id).profit,
                               ReservationDetailVM = (from resDet in db.ReservationDetails
                                                    join client in db.Clients on resDet.client_id equals client.id
                                                    select new ReservationDetailViewModel
                                                    {
                                                        res_id = resDet.id,
                                                        reservation_from = resDet.reservation_from,
                                                        reservation_to = resDet.reservation_to,
                                                        room_type = resDet.room_type,
                                                        client_first_name = client.first_name,
                                                        client_last_name = client.last_name,

                                                    }).Where(rd => rd.res_id == id).ToList()
                           }).Where(r => r.id == id).FirstOrDefault();
            var report = new Rotativa.ViewAsPdf("Itinirary", resData);
            return report;
        }

        public ActionResult Invoice(int id)
        {
            ReservationViewModel resData = (from res in db.Reservations
                                            join company in db.Companies on res.company_id equals company.id
                                            join event_hotel in db.EventHotels on res.event_hotel_id equals event_hotel.id
                                            join even in db.Events on event_hotel.event_id equals even.id
                                            join hotel in db.Hotels on event_hotel.hotel_id equals hotel.id
                                            //join locationHotel in db.HotelLocations on hotel.id equals locationHotel.hotel_id
                                            //join location in db.Locations on locationHotel.location_id equals location.id
                                            select new ReservationViewModel
                                            {
                                                id = res.id,
                                                total_amount = res.total_amount,
                                                currency = res.currency,
                                                tax = res.tax,
                                                financial_advance = res.financial_advance,
                                                financial_advance_date = res.financial_advance_date,
                                                financial_due = res.financial_due,
                                                financial_due_date = res.financial_due_date,
                                                status = res.status,
                                                single_price = res.single_price,
                                                double_price = res.double_price,
                                                triple_price = res.triple_price,
                                                quad_price = res.quad_price,
                                                vendor_single_price = res.vendor_single_price,
                                                vendor_douple_price = res.vendor_douple_price,
                                                vendor_triple_price = res.vendor_triple_price,
                                                vendor_quad_price = res.vendor_quad_price,
                                                vendor_id = res.vendor_id,
                                                active = res.active,
                                                created_at = res.created_at,
                                                company_name = company.name,
                                                phone = company.phone,
                                                email = company.email,
                                                company_id = res.company_id,
                                                event_hotel_id = event_hotel.id,
                                                hotel_id = hotel.id,
                                                hotel_name = hotel.name,
                                                hotel_rate = hotel.rate,
                                                hotel_address = hotel.address,
                                                reservations_officer_name = res.reservations_officer_name,
                                                reservations_officer_email = res.reservations_officer_email,
                                                reservations_officer_phone = res.reservations_officer_phone,
                                                opener = res.opener,
                                                closer = res.closer,
                                                check_in = res.check_in,
                                                check_out = res.check_out,
                                                string_check_in = res.check_in.ToString(),
                                                string_check_out = res.check_out.ToString(),
                                                //total_nights = res.total_nights,
                                                //total_rooms = res.total_rooms,
                                                profit = res.profit,
                                                shift = res.shift,
                                                event_name = even.title,
                                                event_tax = even.tax,
                                                event_single_price = event_hotel.single_price,
                                                event_double_price = event_hotel.double_price,
                                                event_triple_price = event_hotel.triple_price,
                                                event_quad_price = event_hotel.quad_price,
                                                event_vendor_single_price = event_hotel.vendor_single_price,
                                                event_vendor_double_price = event_hotel.vendor_douple_price,
                                                event_vendor_triple_price = event_hotel.vendor_triple_price,
                                                event_vendor_quad_price = event_hotel.vendor_quad_price,
                                                total_price = db.ReservationDetails.Where(r => r.reservation_id == res.id).Select(p => p.amount).Sum(),
                                                paid_amount = res.paid_amount,
                                                total_amount_after_tax = res.total_amount_after_tax,
                                                total_amount_from_vendor = res.total_amount_from_vendor,
                                                advance_reservation_percentage = res.advance_reservation_percentage,
                                                tax_amount = res.tax_amount,
                                                total_rooms = db.ReservationDetails.Where(tr => tr.reservation_id == id).ToList().Count(),
                                                total_nights = db.ReservationDetails.Where(tr => tr.reservation_id == id).Select(tn => tn.no_of_days).Sum(),
                                                hotelFacilities = db.HotelFacilities.Where(f => f.hotel_id == hotel.id).Select(fh => fh.name).ToList(),
                                                single_rooms = db.ReservationDetails.Where(sr => sr.room_type == 1 && sr.reservation_id == id).Count(),
                                                double_rooms = db.ReservationDetails.Where(sr => sr.room_type == 2 && sr.reservation_id == id).Count(),
                                                triple_rooms = db.ReservationDetails.Where(sr => sr.room_type == 3 && sr.reservation_id == id).Count(),
                                                quad_rooms = db.ReservationDetails.Where(sr => sr.room_type == 4 && sr.reservation_id == id).Count(),
                                                single_nights = db.ReservationDetails.Where(sr => sr.room_type == 1 && sr.reservation_id == id).Select(sr => sr.no_of_days).Sum(),
                                                double_nights = db.ReservationDetails.Where(sr => sr.room_type == 2 && sr.reservation_id == id).Select(sr => sr.no_of_days).Sum(),
                                                triple_nights = db.ReservationDetails.Where(sr => sr.room_type == 3 && sr.reservation_id == id).Select(sr => sr.no_of_days).Sum(),
                                                quad_nights = db.ReservationDetails.Where(sr => sr.room_type == 4 && sr.reservation_id == id).Select(sr => sr.no_of_days).Sum(),
                                                //profit = calculateProfit(res.id).profit,
                                                ReservationDetailVM = (from resDet in db.ReservationDetails
                                                                       join client in db.Clients on resDet.client_id equals client.id
                                                                       select new ReservationDetailViewModel
                                                                       {
                                                                           res_id = resDet.id,
                                                                           reservation_from = resDet.reservation_from,
                                                                           reservation_to = resDet.reservation_to,
                                                                           room_type = resDet.room_type,
                                                                           client_first_name = client.first_name,
                                                                           client_last_name = client.last_name,

                                                                       }).Where(rd => rd.res_id == id).ToList()
                                            }).Where(r => r.id == id).FirstOrDefault();
            var report = new Rotativa.ViewAsPdf("Invoice", resData);
            return report;
        }

    }
}