using System;
using System.Linq;
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


        [HttpGet("todos")]
        public async Task<IActionResult> GetTodos()
        {
            return this.Json((await this.TodoRepository.FindAllTodos()).ToArray());
        }

        public class TodoParams
        {
            public string Title { get; set; }
            public bool Completed { get; set; }
        }

        [HttpPost("todos")]
        public async Task<IActionResult> CreateTodo([FromBody] TodoParams param)
        {
            return this.Json(await this.TodoRepository.CreateTodo(param.Title, param.Completed));
        }

        [HttpPut("todos/{id}")]
        public async Task<IActionResult> UpdateTodo(int id, [FromBody] TodoParams param)
        {
            await this.TodoRepository.UpdateTodo(id, param.Title, param.Completed);
            return this.NoContent();
        }

        [HttpDelete("todos/{id}")]
        public async Task<IActionResult> DeleteTodo(int id)
        {
            await this.TodoRepository.DeleteTodo(id);
            return this.NoContent();
        }

        [HttpGet("todos/{id}/history")]
        public async Task<IActionResult> FindTodoHistory(int id)
        {
            return this.Json((await this.TodoRepository.FindTodoHistory(id)).ToArray());
        }

        [HttpGet("todos/all")]
        public async Task<IActionResult> FindDeletedTodos()
        {
            return this.Json((await this.TodoRepository.FindDeletedTodos()).ToArray());
        }
    }
}
