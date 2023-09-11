using PSRes.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace PSRes.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index(string status)
        {
            if (status == "EmailConfirmationTokenInvalid")
            {
                ViewBag.StatusMessage = "Invalid Confirmation Token!";
            }
            else if (status == "EmailConfirmationLinkSent")
            {
                ViewBag.StatusMessage = "Confirmation link sent to email!";
            }
            else if (status == "EmailSuccessfullyConfirmed")
            {
                ViewBag.StatusMessage = "Email successfully confirmed!";
            }
            else if (status == "UserNotFound")
            {
                ViewBag.StatusMessage = "User not found!";
            }
            else if (status == "PasswordResetSuccess")
            {
                ViewBag.StatusMessage = "Password has been changed successfully!";
            }
            else if (status == "PasswordResetFailure")
            {
                ViewBag.StatusMessage = "Couldn't reset password!";
            }
            else if (status == "PasswordResetLinkSent")
            {
                ViewBag.StatusMessage = "Password reset link has been sent to email!";
            }
            if (User.Identity.IsAuthenticated) return RedirectToAction("Index", "Reservation");
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        } 

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
