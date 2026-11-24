
using Microsoft.AspNetCore.Mvc;
using OnlineFoodOrderingSystem.Models;

namespace OnlineFoodOrderingSystem.Controllers
{
    public class HomeController : Controller
    {
        private readonly DatabaseHelper _databaseHelper;

        public HomeController(DatabaseHelper databaseHelper)
        {
            _databaseHelper = databaseHelper;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                bool connected =
                    await _databaseHelper.TestConnectionAsync();

                ViewBag.DatabaseStatus = connected
                    ? "Ligação à base de dados bem-sucedida!"
                    : "A aplicação não está ligada à base de dados esperada.";
            }
            catch (Exception)
            {
                Response.StatusCode = 500;
                ViewBag.DatabaseStatus =
                    "Não foi possível ligar à base de dados. Verifica a configuração.";
            }

            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }
    }
}
