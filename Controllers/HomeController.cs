using Microsoft.AspNetCore.Mvc;

namespace bitemporal_todo.Controllers
{
    [Route("")]
    public class HomeController : Controller
    {
        [HttpGet("")]
        public IActionResult Index()
        {
            return this.View();
        }

        [HttpGet("todos/{id}/history")]
        public IActionResult GetTodoHistory(int id)
        {
            this.ViewData["Id"] = id;
            return this.View("History");
        }
    }
}
