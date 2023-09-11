using PSRes.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using PSRes.Services;
using Microsoft.AspNetCore.Identity;
using System.Security.Cryptography;
using MongoDB.Driver;
using Humanizer;
using Microsoft.AspNetCore.Html;
using Org.BouncyCastle.Bcpg;
using Microsoft.AspNetCore.Authorization;

namespace PSRes.Controllers
{
    public class ReservationController : Controller
    {
        private readonly ILogger _logger;
        private readonly MongoDbReservationService _mongoDbService;
        private readonly UserManager<ApplicationUser> _mongoDbUserManager;

        public ReservationController(MongoDbReservationService mongoDbService, UserManager<ApplicationUser> userManager)
        {
            _mongoDbService = mongoDbService;
            _mongoDbUserManager = userManager;
        }

        public IActionResult Index(int Week = 0, int Interval = 2)
        {
            ReservationWindow window = FetchData(Week);
            
            //processing of info note
            string Note = TempData["reservedto"]?.ToString();
            Task<ApplicationUser> u = _mongoDbUserManager.FindByIdAsync(Note);
            Note = u.Result?.UserName;
            window.Note = !string.IsNullOrEmpty(Note) ? $"This site has been reserved by {Note}" : "";
            
            return View(window);
        }

        [HttpPost]
        public async Task<IActionResult> Index(string button)
        {
            //return NoContent();
            
            (int nOffset, int nInterval) = ReservationWindow.ParseArrowButtonValue(button);
            if (nOffset > -1)
            {
                return RedirectToAction("Index", new { Week = nOffset, Interval = nInterval});
            }

            (int nClickedRow, int nClickedColumn, nOffset, nInterval) = ReservationWindow.ParseGridButtonValue(button);

           
            int IsGridButton = await TryHandleGridButton(button);
            if (IsGridButton>-1)
            {
                return RedirectToAction("Index", new { Week = nOffset, Interval = nInterval });
            }
            return NoContent();
        }

        private async Task<int> TryHandleGridButton(string buttonString)
        {
            (int nClickedRow, int nClickedColumn, int nOffset, int nInterval) = ReservationWindow.ParseGridButtonValue(buttonString);
            int ret = -1;//-1..wrong input, 0..no reservation, 1..reserved by me, 2..reserved by someone else
            if (nClickedRow > -1 && nClickedColumn > -1)
            {
                ReservationWindow window = FetchData(nOffset);
                Reservation r = window.ReservationAt(nClickedRow, nClickedColumn);
                /*if (r != null)
                {
                    TempData["reservedto"] = r.UserId;
                }
                else*/
                if (User?.Identity?.IsAuthenticated ?? false)
                {
                    ApplicationUser u = await _mongoDbUserManager.FindByNameAsync(User.Identity.Name);
                    string uid = u?.Id.ToString();
                    List<ReservationPoint> rpl = await _mongoDbService.GetAsyncP();

                    if (rpl?.Count > nClickedRow && !string.IsNullOrEmpty(uid))
                    {
                        ReservationPoint rp = rpl[nClickedRow];
                        DateOnly dt = window.DateAt(nClickedColumn);
                        if (dt.Year != 0)
                        {
                            if (r == null)
                            {
                                await _mongoDbService.CreateAsyncR(rp, uid, dt);
                                ret = 1;
                            }
                            else
                            {
                                if (uid == r.UserId)
                                {
                                    //todo: delete reservation
                                    bool deleteResult = await _mongoDbService.DeleteAsyncR(rp, r.Id);
                                    ret = 0;
                                }
                                else
                                {
                                    TempData["reservedto"] = r.UserId;
                                    ret = 2;
                                }
                            }
                        }
                    }
                }
                else if (r != null)
                {
                    TempData["reservedto"] = r.UserId;
                    ret = 2;
                }
                TempData["row"] = nClickedRow;
                TempData["col"] = nClickedColumn;
                //return RedirectToAction("Index", new { Week = nOffset, Interval = nInterval });
            }
            return ret;
        }
        //Reservation points editing
        public ActionResult Edit(string id)
        {
           
            Task<ReservationPoint> p = _mongoDbService.GetAsyncP(id);
            EditPoint ep = new EditPoint();
            ep.Id = p.Result.Id;
            ep.Name = p.Result.Name;
            
            return View(ep);
        }

        [HttpPost]
        public async Task<ActionResult> EditAsync(EditPoint ep)
        {
            ReservationPoint rp = new ReservationPoint();
            rp.Id = ep.Id;
            rp.Name = ep.Name;
            await _mongoDbService.UpdateAsyncP(rp);
            return RedirectToAction("PointsOverview", "Reservation");
        }
        public ActionResult Create()
        {
            
            return View();
        }
        [HttpPost]
        public async Task<ActionResult> CreateAsync(EditPoint ep)
        {
            ReservationPoint rp = new ReservationPoint();
            rp.Id = ep.Id;
            rp.Name = ep.Name;
            rp.ReservationIDs = new List<string>();
            await _mongoDbService.CreateAsyncP(rp);
            return RedirectToAction("PointsOverview", "Reservation");
        }

        public async Task<ActionResult> Delete(string id)
        {
            await _mongoDbService.DeleteReservationsByPoint(id);
            _mongoDbService.DeleteP(id);
            return RedirectToAction("PointsOverview");
        }

        public ViewResult PointsOverview()
        {
            Task<List<ReservationPoint>> p = _mongoDbService.GetAsyncP();
            
            return View(p.Result);
        }


        public async Task<ViewResult> ReservationsOverviewAsync()
        {
            List<Reservation> r = await _mongoDbService.GetAsyncR();
            List<ReservationPoint> p = await _mongoDbService.GetAsyncP();
            List<ApplicationUser> u = _mongoDbUserManager.Users.ToList();
            List<ReservationEdit> re = new List<ReservationEdit>();

            //makeshift join
            foreach (Reservation rs in r)
            {
                ReservationEdit red = new ReservationEdit();
                red.Reservation = rs;
                red.Point = p.Where(x => x.Id == rs.RpId).FirstOrDefault();
                red.User = u.Where(x => x.Id.ToString() == rs.UserId).FirstOrDefault();
                re.Add(red);
            }


            return View(re);
        }

        [HttpPost]
        public async Task<ActionResult> DeleteReservationAsync(string delres)
        {
            await _mongoDbService.DeleteAsyncR(delres);
            return RedirectToAction("ReservationsOverview", "Reservation");
        }
        
        [HttpPost]
        public async Task<ActionResult> Cleanup()
        {
            int ret = await _mongoDbService.DeleteOverdueAsync();
            return RedirectToAction("ReservationsOverview", "Reservation");
        }

        //user data ajax endpoint
        [HttpPost]
        public IActionResult AjaxUserData(ButtonTextContainer ButtonTextContainer)
        {
            string s = ButtonTextContainer?.ButtonText;
            string sDate = "unknown date";
            if (s == null) {
                s = new string(@"");
                return Json(new { Message = s });
            }
            Task<DateTime?> TextDateT = GetDateAsync(s);
            while (TextDateT.Status != TaskStatus.RanToCompletion) { }
            DateTime? dt = TextDateT.Result;
            if (dt != null)
            {
                DateOnly dol = DateOnly.FromDateTime(dt.Value);
                sDate = dol.ToString();
            }

            Task<Reservation> rt = GetReservationAsync(s);
            while (rt.Status != TaskStatus.RanToCompletion) { }
            Reservation r = rt?.Result;
            
            if (r == null)
            {
                s = @"no reservation";
                return Json(new { msg = sDate+ ": " + s });
            }

            Task<ApplicationUser> ut = _mongoDbUserManager.FindByIdAsync(r.UserId);
              while (ut.Status != TaskStatus.RanToCompletion) { }
            ApplicationUser u = ut.Result;

            string ret = new String(sDate + @": " + u.FirstName + @" " +  u.LastName);
            return Json(new { msg = ret });
        }

        //grid button click ajax endpoint
        [HttpPost]
        [Authorize]
        public IActionResult AjaxGridButtonSubmit(ButtonTextContainer ButtonTextContainer)
        {
            string ret = new String(@"");
            string s = ButtonTextContainer?.ButtonText;
            Task<int> rt = TryHandleGridButton(s);
            while (rt.Status != TaskStatus.RanToCompletion) { }
            int stat = rt.Result;
            //-1..wrong input, 0..no reservation, 1..reserved by me, 2..reserved by someone else
            if (stat == -1) return Json(new { msg = ret });
            HtmlString hret = ReservationWindow.ImgSrc(stat, false);
            return Json(new { msg = hret.ToString() });
        }
           

        //helper functions
        private async Task<Reservation> GetReservationAsync(string buttonString) 
        {
            List<ReservationPoint> points = await _mongoDbService.GetAsyncP();
            (int nClickedRow, int nClickedColumn, int nOffset, int nInterval) = ReservationWindow.ParseGridButtonValue(buttonString);
            ReservationPoint point = points?.ElementAt(nClickedRow);
            if (point == null) return null;
            DateTime dt = TimeZoneInfo.ConvertTime(DateTime.Now, _mongoDbService.TimeZone);
            
            dt = dt.AddDays(nOffset + nClickedColumn);

            Reservation ret = await _mongoDbService.GetAsyncR(point.Id, dt);

            return ret;
        }
        private async Task<DateTime?> GetDateAsync(string buttonString)
        {
            List<ReservationPoint> points = await _mongoDbService.GetAsyncP();
            (int nClickedRow, int nClickedColumn, int nOffset, int nInterval) = ReservationWindow.ParseGridButtonValue(buttonString);
            ReservationPoint point = points?.ElementAt(nClickedRow);
            if (point == null) return null;
            DateTime dt = TimeZoneInfo.ConvertTime(DateTime.Now, _mongoDbService.TimeZone);
            dt = dt.AddDays(nOffset + nClickedColumn);
            return dt;
        }

        private ReservationWindow FetchData(int week, int nInterval = 2)
        {
            Task<List<ReservationPoint>> pointst = _mongoDbService.GetAsyncP();
            while (pointst.Status != TaskStatus.RanToCompletion) { }
            List<ReservationPoint> points = pointst.Result;
            
            Task<ApplicationUser> tuser = _mongoDbUserManager.FindByNameAsync(User?.Identity?.Name);
            while (tuser.Status != TaskStatus.RanToCompletion) { }
            ApplicationUser u = tuser.Result;
            string userId = u?.Id.ToString();


            int nOffset = week * nInterval * 7;
            DateOnly dateStart = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTime.Now, _mongoDbService.TimeZone));
            dateStart = dateStart.AddDays(nOffset);
            DateOnly dateEnd = dateStart.AddDays(nInterval * 7);
            Task<List<ReservationDay>> dayst = _mongoDbService.GetAsyncR(dateStart, dateEnd);
            List<ReservationDay> days = dayst.Result;
            List<ApplicationUser> usrs = _mongoDbUserManager.Users.ToList();
            ReservationWindow window = new ReservationWindow(points, days, week, userId);

            return window;
        }


    }
    [Serializable]
    public class ButtonTextContainer
    {
        public string ButtonText { get; set; }
    }
}
