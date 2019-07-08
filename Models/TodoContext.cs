using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace bitemporal_todo
{
    public class TaskHistoryEntity
    {
        public long Id { get; set; }
        public string TaskId { get; set; }
        public string Title { get; set; }
        public DateTime? Completed { get; set; }

        public DateTime? ValidFrom { get; set; }
        public DateTime? ValidTo { get; set; }
        public DateTime? TransactFrom { get; set; }
        public DateTime? TransactTo { get; set; }
    }

    public class TodoContext: DbContext
    {
        public delegate TaskItem TaskUpdator(TaskItem task);


        public DbSet<TaskHistoryEntity> TaskHistory { get; set; }


        public TodoContext(DbContextOptions<TodoContext> options)
            : base(options)
        {
        }


        public async Task UpdateTask(string id, TaskUpdator updator, DateTime? from = null, DateTime? to = null)
        {
            using (var tx = await this.Database.BeginTransactionAsync(IsolationLevel.Serializable))
            {
                var now = DateTime.Now;

                var overwrapped = await this.TaskHistory
                    .Where(th => th.TaskId == id)
                    .ForSystemTimeAsOf(now)
                    .ForBusinessTimeBetween(from, to)
                    .OrderBy(th => th.ValidFrom)
                    .ToArrayAsync();
                foreach (var item in overwrapped)
                {
                    item.TransactTo = now;
                    
                    if (item.ValidFrom.IsLeftOf(from))
                    {
                        await this.AddAsync(new TaskHistoryEntity()
                        {
                            TaskId = item.TaskId,
                            Title = item.Title,
                            Completed = item.Completed,
                            ValidFrom = item.ValidFrom,
                            ValidTo = from,
                            TransactFrom = now,
                            TransactTo = null,
                        });
                    }
                    // HACK: perserve INSERT order
                    await this.SaveChangesAsync();

                    var updated = updator(new TaskItem()
                    {
                        Id = item.TaskId,
                        Title = item.Title,
                        Completed = item.Completed,
                    });
                    if (updated != null)
                    {
                        await this.AddAsync(new TaskHistoryEntity()
                        {
                            TaskId = updated.Id,
                            Title = updated.Title,
                            Completed = updated.Completed,
                            ValidFrom = DateTimeExtension.MaxLowerBounds(from, item.ValidFrom),
                            ValidTo = DateTimeExtension.MinUpperBounds(to, item.ValidTo),
                            TransactFrom = now,
                            TransactTo = null,
                        });
                        // HACK: perserve INSERT order
                        await this.SaveChangesAsync();
                    }

                    if (item.ValidTo.IsRightOf(to))
                    {
                        await this.AddAsync(new TaskHistoryEntity()
                        {
                            TaskId = item.TaskId,
                            Title = item.Title,
                            Completed = item.Completed,
                            ValidFrom = to,
                            ValidTo = item.ValidTo,
                            TransactFrom = now,
                            TransactTo = null,
                        });
                    }
                    // HACK: perserve INSERT order
                    await this.SaveChangesAsync();
                }

                await this.SaveChangesAsync();
                tx.Commit();
            }
        }

        public async Task ReplaceTask(string id, TaskItem task, DateTime? from = null, DateTime? to = null)
        {
            using (var tx = await this.Database.BeginTransactionAsync(IsolationLevel.Serializable))
            {
                var now = DateTime.Now;

                var overwrapped = await this.TaskHistory
                    .Where(th => th.TaskId == id)
                    .ForSystemTimeAsOf(now)
                    .ForBusinessTimeBetween(from, to)
                    .OrderBy(th => th.ValidFrom)
                    .ToArrayAsync();
                foreach (var item in overwrapped)
                {
                    item.TransactTo = now;
                    
                    if (item.ValidFrom.IsLeftOf(from))
                    {
                        await this.AddAsync(new TaskHistoryEntity()
                        {
                            TaskId = item.TaskId,
                            Title = item.Title,
                            Completed = item.Completed,
                            ValidFrom = item.ValidFrom,
                            ValidTo = from,
                            TransactFrom = now,
                            TransactTo = null,
                        });
                    }
                    // HACK: perserve INSERT order
                    await this.SaveChangesAsync();
                }

                await this.AddAsync(new TaskHistoryEntity()
                {
                    TaskId = task.Id,
                    Title = task.Title,
                    Completed = task.Completed,
                    ValidFrom = from,
                    ValidTo = to,
                    TransactFrom = now,
                    TransactTo = null,
                });
                // HACK: perserve INSERT order
                await this.SaveChangesAsync();

                foreach (var item in overwrapped)
                {
                    if (item.ValidTo.IsRightOf(to))
                    {
                        await this.AddAsync(new TaskHistoryEntity()
                        {
                            TaskId = item.TaskId,
                            Title = item.Title,
                            Completed = item.Completed,
                            ValidFrom = to,
                            ValidTo = item.ValidTo,
                            TransactFrom = now,
                            TransactTo = null,
                        });
                    }
                    // HACK: perserve INSERT order
                    await this.SaveChangesAsync();
                }

                await this.SaveChangesAsync();
                tx.Commit();
            }
        }
    }

    public static class DateTimeExtension
    {
        public static DateTime Min(DateTime lhs, DateTime rhs)
        {
            return (lhs < rhs) ? lhs : rhs;
        }

        public static DateTime? MaxLowerBounds(DateTime? lhs, DateTime? rhs)
        {
            if (lhs == null && rhs == null)
            {
                return null;
            }
            else if (lhs == null)
            {
                return rhs.Value;
            }
            else if (rhs == null)
            {
                return lhs.Value;
            }
            else
            {
                return Max(lhs.Value, rhs.Value);
            }
        }

        public static DateTime Max(DateTime lhs, DateTime rhs)
        {
            return (lhs < rhs) ? rhs : lhs;
        }

        public static DateTime? MinUpperBounds(DateTime? lhs, DateTime? rhs)
        {
            if (lhs == null && rhs == null)
            {
                return null;
            }
            else if (lhs == null)
            {
                return rhs.Value;
            }
            else if (rhs == null)
            {
                return lhs.Value;
            }
            else
            {
                return Min(lhs.Value, rhs.Value);
            }
        }

        public static bool IsLeftOf(this DateTime? lhs, DateTime? rhs)
        {
            if (rhs == null)
            {
                return false;
            }
            else if (lhs == null)
            {
                return true;
            }
            else
            {
                return lhs.Value < rhs.Value;
            }
        }

        public static bool IsRightOf(this DateTime? lhs, DateTime? rhs)
        {
            if (rhs == null)
            {
                return false;
            }
            else if (lhs == null)
            {
                return true;
            }
            else
            {
                return rhs.Value < lhs.Value;
            }
        }
    }

    public static class QueryableOfTaskHistoryEntityExetnsion
    {
        public static IQueryable<TaskHistoryEntity> ForBusinessTimeAsOf(this IQueryable<TaskHistoryEntity> query, DateTime businessTime)
        {
            return query
                .Where(th => th.ValidFrom == null || th.ValidFrom <= businessTime)
                .Where(th => th.ValidTo == null || businessTime < th.ValidTo);
        }

        public static IQueryable<TaskHistoryEntity> ForSystemTimeAsOf(this IQueryable<TaskHistoryEntity> query, DateTime systemTime)
        {
            return query
                .Where(th => th.TransactFrom == null || th.TransactFrom <= systemTime)
                .Where(th => th.TransactTo == null || systemTime < th.TransactTo);
        }

        public static IQueryable<TaskHistoryEntity> ForBusinessTimeBetween(this IQueryable<TaskHistoryEntity> query, DateTime? from, DateTime? to)
        {
            if (from != null)
            {
                query = query.Where(th => th.ValidTo == null || from < th.ValidTo);
            }
            if (to != null)
            {
                query = query.Where(th => th.ValidFrom == null || th.ValidFrom < to);
            }
            return query;
        }

        public static IQueryable<TaskHistoryEntity> ForBusinessTimeAll(this IQueryable<TaskHistoryEntity> query)
        {
            return query;
        }
    }

    public static class ObjectExtension
    {
        public static R Let<T, R>(this T thiz, Func<T, R> fn)
        {
            return fn(thiz);
        }

        public static T Also<T>(this T thiz, Action<T> fn)
        {
            fn(thiz);
            return thiz;
        }
    }
}
