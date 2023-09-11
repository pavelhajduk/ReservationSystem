using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PSRes.Controllers
{
    [Authorize]
    public class SecuredController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
