using PSRes.Models;
using PSRes.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using SendGrid.Helpers.Mail.Model;
using Microsoft.Extensions.Options;
using EllipticCurve.Utils;
using Microsoft.AspNetCore.Html;

namespace PSRes.Controllers
{
    public class AccountController : Controller
    {
        private UserManager<ApplicationUser> userManager;
        private SignInManager<ApplicationUser> signInManager;
        private ILogger<AccountController> logger;
        private IEmailSender emailSender;

        public AccountController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, ILogger<AccountController> logger, IEmailSender emailSender)
        {
            this.userManager = userManager;
            this.signInManager = signInManager;
            this.logger = logger;
            this.emailSender = emailSender;
        }

        [AllowAnonymous]
        public IActionResult Login()
        {
            return View();
        }


        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login([Required] string username, [Required] string password, string returnurl)
        {
            if (ModelState.IsValid)
            {
                ApplicationUser appUser = await userManager.FindByNameAsync(username);
                if (appUser != null)
                {
                    await signInManager.SignOutAsync();
                    Microsoft.AspNetCore.Identity.SignInResult result = await signInManager.PasswordSignInAsync(appUser, password, false, false);
                    if (result.Succeeded)
                    {
                        return Redirect(returnurl ?? "/");
                    }
                    else
                    {
                        ViewBag.ShowPasswordResetLink = true;
                        ViewBag.UserName = appUser.UserName;
                        HttpContext.Items["UserName"] = appUser.UserName;
                    }
                }
                string strMsg = appUser == null ? "Login Failed: Invalid User Name or Password" : appUser.EmailConfirmed == false ? "Email not confirmed yet!" : "Login Failed: Invalid User Name or Password";
                ModelState.AddModelError(nameof(username), strMsg);
                if (appUser != null && appUser.EmailConfirmed == false)
                {
                    ViewBag.ShowEmailConfirmationLink = true;
                    ViewBag.UserName = appUser.UserName;
                    
                    HttpContext.Items["UserName"] = appUser.UserName;
                }
                
            }
            
            return View();
        }

         
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            await signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }
        
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> ConfirmEmail(string userId, string token)
        {
            if (userId == null || token == null)
            {
                return RedirectToAction("index", "home");
            }


            var user = await userManager.FindByIdAsync(userId);


        if (user == null) {
                ViewBag.StatusMessage = "User not found!";
                return RedirectToAction("index","home", new { status="UserNotFound"}); 
            }
            var result = await userManager.ConfirmEmailAsync(user, token.ToString());

        if (result.Succeeded) {
                return RedirectToAction("index", "home", new { status = "EmailSuccessfullyConfirmed" });
            }
            return RedirectToAction("Index", "Home", new { status = "EmailConfirmationTokenInvalid" }) ;
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult ResetPassword(string userId, string token)
        {
            if (userId == null || token == null)
            {
                ModelState.AddModelError("" , "Invalid password reset token");
                return RedirectToAction("index", "home", new { status = "PasswordResetFailure" });
            }
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> ResetPassword(ResetPasswordModel model)
        {
            if (ModelState.IsValid)
            {
                var user = await userManager.FindByIdAsync(model.UserId);
                if (user != null)
                {
                    var result = await userManager.ResetPasswordAsync(user, model.Token, model.Password);
                    if (result.Succeeded)
                    {
                        return RedirectToAction("index", "home", new { status = "PasswordResetSuccess" });
                    }
                    foreach (var error in result.Errors)
                    {
                        ModelState.AddModelError("", error.Description);
                    }
                    return View(model);
                }
                ModelState.AddModelError("", "User doesn't exist");
                return View(model);
            }
            return View(model);
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> PasswordResetLink(string UserName, int level)
        {
            ApplicationUser appUser = await userManager.FindByNameAsync(UserName);

            ViewBag.UserEmail = appUser?.Email;

            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> PasswordResetLink([Required][EmailAddress] string email)
        {
            ApplicationUser appUser = await userManager.FindByEmailAsync(email);

            if (appUser == null)
            {
                return RedirectToAction("index", "home", new { status = "EmailInvalid" });
            }
            var token = await userManager.GeneratePasswordResetTokenAsync(appUser);

            if (token != null)
            {
                var resetLink = Url.Action(nameof(ResetPassword), "Account", new { token, userId = appUser.Id }, Request.Scheme);
                string messageStatus = await emailSender.SendEmailAsync(email, "", resetLink);
                return RedirectToAction("index", "home", new { status = "PasswordResetLinkSent"});
            }
            return RedirectToAction("index", "home");
        }

        [HttpPost, Route("SendEmail")]
        public async Task<IActionResult> SendEmailAsync(string recipientEmail, string recipientFirstName, string Link)
        {
            try
            {
                string messageStatus = await emailSender.SendEmailAsync(recipientEmail, recipientFirstName, Link);
                return Ok(messageStatus);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message.ToString());
            }
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> EmailConfirmationLink(string UserName, int level)
        {
            ApplicationUser appUser = await userManager.FindByNameAsync(UserName);

            ViewBag.UserEmail = appUser?.Email;

            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> EmailConfirmationLink([Required][EmailAddress] string email)
        {
            ApplicationUser appUser = await userManager.FindByEmailAsync(email);
            
            if (appUser==null)
            {
                return RedirectToAction("index", "home", new { status = "EmailInvalid" });
            }

            
            var token = await userManager.GenerateEmailConfirmationTokenAsync(appUser);

            if (token != null)
            {
                var confirmationLink = Url.Action(nameof(ConfirmEmail), "Account", new { token, userId = appUser.Id }, Request.Scheme);
                string messageStatus = await emailSender.SendEmailAsync(email, "", confirmationLink);
                return RedirectToAction("index", "home");
            }
            return RedirectToAction("index", "home");
        }


    }
}
