using IdentityMongo.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using System;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authorization;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.Serialization.Json;
using System.IO;
using System.Text;
using MimeKit.Encodings;
using IdentityMongo.Services;

namespace IdentityMongo.Controllers
{
    [Authorize(Roles = "ResAdmin")]
    public class OperationsController : Controller
    {
        private UserManager<ApplicationUser> userManager;
        private RoleManager<ApplicationRole> roleManager;
        private MongoDbReservationService mongoDbService;
        private ILogger<OperationsController> logger;

        public OperationsController(UserManager<ApplicationUser> userManager, RoleManager<ApplicationRole> roleManager, MongoDbReservationService mongoDbService, ILogger<OperationsController> logger)
        {
            this.userManager = userManager;
            this.roleManager = roleManager;
            this.mongoDbService = mongoDbService;
            this.logger = logger;
        }

        [AllowAnonymous]
        public ViewResult Create() => View();

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> Create(User user)
        {
            if (ModelState.IsValid)
            {
                ApplicationUser appUser = new ApplicationUser
                {
                    UserName = user.Name,
                    Email = user.Email,
                    FirstName = user.FirstName,
                    LastName = user.LastName
                };

                IdentityResult result = await userManager.CreateAsync(appUser, user.Password);

                //Adding User to Admin Role
                //await userManager.AddToRoleAsync(appUser, "Admin");

                if (result.Succeeded)
                {
                    ViewBag.Message = "User Created Successfully";
                    var token = await userManager.GenerateEmailConfirmationTokenAsync(appUser);
                    var confirmationLink = Url.Action("ConfirmEmail", "Account", new { userId = appUser.Id, token }, Request.Scheme);
                    logger.Log(LogLevel.Warning, confirmationLink);
                }
                else
                {
                    foreach (IdentityError error in result.Errors)
                        ModelState.AddModelError("", error.Description);
                }

            }
            return View(user);
        }

        public async Task<ActionResult> Edit(string user)
        {
            ApplicationUser u = userManager.Users.Where(x => x.UserName == user).FirstOrDefault();
            if (u == null) return NoContent();
            EditUser eu = new EditUser();
            bool bRet = await ReadUpdateUser(u, eu, false);
            
            if (bRet) return View(eu);
            else return NoContent();

        }
        public async Task<bool> ReadUpdateUser(ApplicationUser u, EditUser eu, bool update)
        {
            if (u == null) return false;

            List<string> rolesAvailable = new List<string>();
            foreach (ApplicationRole rl in roleManager.Roles)
            {
                rolesAvailable.Add(rl.Name);
            }

            if (!update)//HttpGet
            {
                eu.Id = u.Id;
                eu.Name = u.UserName;
                eu.Email = u.Email;
                eu.EmailConfirmed = u.EmailConfirmed;
                eu.RolesAvailable = rolesAvailable;
                eu.RolesAssigned = new List<string>();
                eu.FirstName = u.FirstName;
                eu.LastName = u.LastName;

                //List<string> rolesAssigned = roleManager.Roles.AsQueryable().Join(u.Roles.AsQueryable(), x => x.Id, y => y, (x, y) => new string(x.Name))?.ToList();
                //eu.RolesAvailable = rolesAvailable.Except(rolesAssigned).ToList();
                
                //makeshift join
                foreach (Guid rl in u.Roles)
                {
                    string roleName = roleManager.Roles.Where(x => x.Id == rl).FirstOrDefault()?.Name;
                    if (roleName != null) eu.RolesAssigned.Add(roleName);
                    Guid roleId = rl;
                }
                eu.RolesAvailable = eu.RolesAvailable.Except(eu.RolesAssigned).ToList();
                

            }
            else//HttpPost
            {
                Deserialize(eu);
                //List<string> rolesAssigned = eu.RolesAssigned;
                //List<string> rolesPrevAssigned = roleManager.Roles.Join(u.Roles, x => x.Id, y => y, (x, y) => x.Name)?.ToList();
                

                ///makeshift join
                List<string> rolesAssignedPreviously = new List<string>();
                foreach (Guid rl in u.Roles)
                {
                    string roleName = roleManager.Roles.Where(x => x.Id == rl).FirstOrDefault()?.Name;
                    if (roleName != null) rolesAssignedPreviously.Add(roleName);
                    Guid roleId = rl;
                }
                List<string> rolesAdded = eu.RolesAssigned?.Except(rolesAssignedPreviously).ToList();
                List<string> rolesRemoved = rolesAssignedPreviously.Except(eu.RolesAssigned).ToList();



                await userManager.AddToRolesAsync(u, rolesAdded);
                await userManager.RemoveFromRolesAsync(u, rolesRemoved);


                u.UserName = eu.Name;
                u.Email = eu.Email;
                u.EmailConfirmed = eu.EmailConfirmed;
                u.FirstName = eu.FirstName;
                u.LastName = eu.LastName;
                IdentityResult r = await userManager.UpdateAsync(u);
                
            }
            return true;
        }

        [HttpPost]
        public async Task<ActionResult> Edit(EditUser eu)
        {
            ApplicationUser u = userManager.Users.Where(x => x.Id == eu.Id).FirstOrDefault();
            bool bRet = await ReadUpdateUser(u, eu, true);
            return RedirectToAction("UserOverview", "Operations");
        }

        public ViewResult UserOverview()
        {
            //List<ApplicationUser> l = userManager.Users.Select(x => x).ToList();
            List<ApplicationUser> l = userManager.Users.ToList();
            List<EditUser> leu = new List<EditUser>();
            List<ApplicationRole> roles = roleManager.Roles.ToList();

            foreach (ApplicationUser u in l)
            {
                EditUser eu = new EditUser();
                eu.Id = u.Id;
                eu.Name = u.UserName; 
                eu.Email = u.Email;
                eu.FirstName = u.FirstName;
                eu.LastName = u.LastName;
                eu.EmailConfirmed = u.EmailConfirmed;
                eu.RolesAssigned = roles.Where(x => u.Roles.Contains(x.Id)).Select(x => x.Name).ToList();
                leu.Add(eu);
            }

            UserOverview o = new UserOverview(leu); 
            return View(o);
        }

        [HttpPost]
        public async Task<ActionResult> UserOverview(UserOverview o)
        {
            //TODO continue here
           
           //IList<ApplicationUser> l = userManager.Users.Select(x => x).ToList();
            return View(o);
        }

        public async Task<ActionResult> Delete(string user) 
        { 
           ApplicationUser u = userManager.Users.Where(x=> (x.UserName == user)).FirstOrDefault();

            if (u==null) return  NoContent();

            List<string> rn = new List<string>();
            foreach (Guid rid in u.Roles)
            {
                ApplicationRole r = await roleManager.FindByIdAsync(rid.ToString());
                rn.Add(r.Name);
            }
            await userManager.RemoveFromRolesAsync(u, rn);

            await mongoDbService.DeleteReservationsByUserId(u.Id.ToString());
            await userManager.DeleteAsync(u);
            return RedirectToAction("UserOverview", "Operations");
        }



        public IActionResult CreateRole() => View();

        [HttpPost]
        public async Task<IActionResult> CreateRole([Required] string name)
        {
            if (ModelState.IsValid)
            {
                IdentityResult result = await roleManager.CreateAsync(new ApplicationRole() { Name = name });
                if (result.Succeeded)
                    ViewBag.Message = "Role Created Successfully";
                else
                {
                    foreach (IdentityError error in result.Errors)
                        ModelState.AddModelError("", error.Description);
                }
            }
            return View();
        }
        public IActionResult DeleteRole(string role)
        {

            return RedirectToAction("RoleOverview", "Operations");
        }
        public IActionResult RoleOverview()
        {
            List<ApplicationRole> roles = roleManager.Roles.ToList();
            return View(roles);
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult EmailConfirmationLink()
        {
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> EmailConfirmationLink([Required][EmailAddress] string email)
        {
            ApplicationUser appUser = await userManager.FindByEmailAsync(email);
            if (appUser == null)
            {
                return RedirectToAction("home", "index", new { status = "EmailInvalid" });
            }
            var token = await userManager.GenerateEmailConfirmationTokenAsync(appUser);
            if (token == null)
            {
                /*string messageStatus = await _emailSender.SendEmailAsync(recipientEmail, recipientFirstName, Link);*/
                return RedirectToAction("home", "index");
            }
            return RedirectToAction("home", "index");
        }

        protected bool Deserialize(EditUser editUser)
        {
            if (editUser == null)
            {
                return false;
            }

            using (var ms = new MemoryStream(Encoding.Unicode.GetBytes(editUser.RolesAssignedSerial)))
            {
                DataContractJsonSerializer js = new DataContractJsonSerializer(typeof(List<string>));
                editUser.RolesAssigned = (List<string>)js.ReadObject(ms);
            }
                
            //js.ReadObject()
            return true;
        }
    }
}
