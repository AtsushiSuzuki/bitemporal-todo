using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Dapper;
using XidNet;

namespace bitemporal_todo
{
    public class TodoRepository
    {
        public IDbConnection Connection { get; }


        public TodoRepository(IDbConnection connection)
        {
            this.Connection = connection;
        }


        public async Task CreateDatabase()
        {
            await this.Connection.ExecuteAsync(@"
                CREATE TABLE IF NOT EXISTS ""task"" (
                    ""id""              TEXT    NOT NULL,
                    ""title""           TEXT    NOT NULL,
                    ""due_date""        DATETIME,
                    ""completed_at""    DATETIME,
                    ""valid_from""      DATETIME,
                    ""valid_to""        DATETIME,
                    ""transact_from""   DATETIME,
                    ""transact_to""     DATETIME
                );
            ");
        }

        public async Task DropDatabase()
        {
            await this.Connection.ExecuteAsync(@"
                DROP TABLE IF EXISTS ""task"";
            ");
        }

        public async Task<IEnumerable<TaskItem>> FindAllTasks()
        {
            return await this.Connection.QueryAsync<TaskItem>(@"
                SELECT
                    t.""id"",
                    t.""title"",
                    t.""due_date"",
                    t.""completed_at""
                FROM
                    ""task"" AS t
                WHERE
                    (t.""valid_from"" IS NULL OR t.""valid_from"" <= CURRENT_TIMESTAMP) AND
                    (t.""valid_to"" IS NULL OR CURRENT_TIMESTAMP < t.""valid_to"") AND
                    (t.""transact_from"" IS NULL OR t.""transact_from"" <= CURRENT_TIMESTAMP) AND
                    (t.""transact_to"" IS NULL OR CURRENT_TIMESTAMP < t.""transact_to"")
                ORDER BY
                    t.""id""
            ");
        }

        public async Task<string> CreateTask(string title, DateTime? dueDate, DateTime? completedAt)
        {
            var id = Xid.NewXid().ToString();
            await this.Connection.ExecuteAsync(@"
                INSERT INTO ""task""
                    (""id"", ""title"", ""due_date"", ""completed_at"", ""valid_from"", ""valid_to"", ""transact_from"", ""transact_to"")
                VALUES
                    (@id, @title, @dueDate, @completedDate, CURRENT_TIMESTAMP, NULL, CURRENT_TIMESTAMP, NULL)
            ", new { id, title, dueDate, completedAt, });
            return id;
        }

        public async Task UpdateTaskCompletedAt(string id, DateTime? completedAt, DateTime? validFrom)
        {
            await this.Connection.ExecuteAsync(@"
                BEGIN;

                UPDATE ""task""
                SET
                    ""valid_to"" = @validFrom
                WHERE
                    ""id"" = @id AND
                    (""valid_from"" IS NULL OR ""valid_from"" <= @validFrom) AND
                    (""valid_to"" IS NULL OR @validFrom < ""valid_to"") AND
                    (""transact_from"" IS NULL OR ""transact_from"" <= CURRENT_TIMESTAMP) AND
                    (""transact_to"" IS NULL OR CURRENT_TIMESTAMP < ""transact_to"")
                ;

                INSERT INTO ""task""
                    (""id"", )

                COMMIT;
            ");
        }
    }
}
