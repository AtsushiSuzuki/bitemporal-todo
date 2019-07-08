using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace bitemporal_todo
{
    [Route("api")]
    public class APIController: Controller
    {
        public TodoRepository TodoRepository { get; }


        public APIController(TodoRepository todoRepository)
        {
            this.TodoRepository = todoRepository;
        }


        [HttpGet("tasks")]
        public async Task<IActionResult> GetAllTasks()
        {
            var tasks = await this.TodoRepository.FindAllTasks();
            return this.Json(tasks);
        }

        [HttpPost("tasks")]
        public async Task<IActionResult> CreateTask(string title, DateTime? dueDate, DateTime? completedDate)
        {
            var id = await this.TodoRepository.CreateTask(title, dueDate, completedDate);
            return this.Json(id);
        }
    }
}
