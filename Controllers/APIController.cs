using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using XidNet;

namespace bitemporal_todo
{
    [Route("api")]
    public class APIController: Controller
    {
        public TodoContext TodoContext { get; }


        public APIController(TodoContext todoContext)
        {
            this.TodoContext = todoContext;
        }


        [HttpGet("tasks")]
        public async Task<IActionResult> FindTasks()
        {
            var now = DateTime.Now;
            var tasks = await this.TodoContext.TaskHistory
                .ForBusinessTimeAsOf(now)
                .ForSystemTimeAsOf(now)
                .OrderBy(th => th.Id)
                .Select(th => new TaskItem()
                {
                    Id = th.TaskId,
                    Title = th.Title,
                    Completed = th.Completed,
                })
                .ToArrayAsync();
            return this.Json(tasks);
        }

        [HttpGet("tasks/all")]
        public async Task<IActionResult> FindAllTasks()
        {
            var now = DateTime.Now;
            var tasks = await (from th1 in this.TodoContext.TaskHistory.ForSystemTimeAsOf(now)
                               join th2 in (
                                   from th in this.TodoContext.TaskHistory.ForSystemTimeAsOf(now)
                                   group th by th.TaskId into g
                                   select new { TaskId = g.Key, ValidFrom = g.Max(th => th.ValidFrom), }
                               ) on new { th1.TaskId, th1.ValidFrom, } equals new { TaskId = th2.TaskId, ValidFrom = th2.ValidFrom, }
                               select new TaskItem() {
                                   Id = th1.TaskId,
                                   Title = th1.Title,
                                   Completed = th1.Completed,
                                   Deleted = th1.ValidTo,
                               }).ToArrayAsync();
            return this.Json(tasks);
        }

        [HttpGet("tasks/{id}/history")]
        public async Task<IActionResult> FindTaskHistory([Required] string id)
        {
            var now = DateTime.Now;
            var history = await this.TodoContext.TaskHistory
                .ForBusinessTimeAll()
                .ForSystemTimeAsOf(now)
                .Where(th => th.TaskId == id)
                .OrderBy(th => th.ValidFrom)
                .Select(th => new TaskHistoryItem()
                {
                    HistoryId = th.Id,
                    Id = th.TaskId,
                    Title = th.Title,
                    Completed = th.Completed,
                    ValidFrom = th.ValidFrom,
                    ValidTo = th.ValidTo,
                })
                .ToArrayAsync();
            return this.Json(history);
        }

        public class CreateTaskParams
        {
            [Required]
            public string Title { get; set; }
            public DateTime? Completed { get; set; }
        }

        [HttpPost("tasks")]
        public async Task<IActionResult> CreateTask([FromBody] CreateTaskParams param)
        {
            var id = Xid.NewXid().ToString();
            var now = DateTime.Now;
            await this.TodoContext.TaskHistory.AddAsync(new TaskHistoryEntity()
            {
                TaskId = id,
                Title = param.Title,
                Completed = param.Completed,
                ValidFrom = now,
                ValidTo = null,
                TransactFrom = now,
                TransactTo = null,
            });
            await this.TodoContext.SaveChangesAsync();
            return this.Json(id);
        }

        public class UpdateTaskTitleParams
        {
            [Required]
            public string Title { get; set; }
        }

        [HttpPut("tasks/{id}/title")]
        public async Task<IActionResult> UpdateTaskTitle([Required] string id, [FromBody] UpdateTaskTitleParams param)
        {
            await this.TodoContext.UpdateTask(id, (task) => new TaskItem()
            {
                Id = task.Id,
                Title = param.Title,
                Completed = task.Completed,
            }, DateTime.Now, null);

            return this.NoContent();
        }

        public class UpdateTaskCompletedParams
        {
            public DateTime? Completed { get; set; }
        }

        [HttpPut("tasks/{id}/completed")]
        public async Task<IActionResult> UpdateTaskCompleted([Required] string id, [FromBody] UpdateTaskCompletedParams param)
        {
            await this.TodoContext.UpdateTask(id, (task) => new TaskItem()
            {
                Id = task.Id,
                Title = task.Title,
                Completed = param.Completed,
            }, param.Completed ?? DateTime.Now, null);

            return this.NoContent();
        }

        [HttpDelete("tasks/{id}")]
        public async Task<IActionResult> DeleteTask([Required] string id)
        {
            await this.TodoContext.UpdateTask(id, (task) => null, DateTime.Now, null);

            return this.NoContent();
        }
    }
}
