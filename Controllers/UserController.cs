
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace OnlineFoodOrderingSystem.Controllers
{
    [Authorize(Roles = "Customer")]
    public class UserController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
