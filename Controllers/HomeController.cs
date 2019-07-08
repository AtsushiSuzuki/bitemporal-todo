using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace bitemporal_todo
{
    [Route("")]
    public class HomeController: Controller
    {
        [Route("")]
        public async Task<IActionResult> Index()
        {
            return this.RedirectToAction("Tasks");
        }

        [Route("tasks")]
        public async Task<IActionResult> Tasks()
        {
            return this.View();
        }

        [Route("tasks/{id}/history")]
        public async Task<IActionResult> TaskHistory(string id)
        {
            this.ViewData["Id"] = id;
            return this.View("TaskHistory");
        }
    }
}