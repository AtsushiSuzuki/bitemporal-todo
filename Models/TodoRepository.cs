using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Dapper;

namespace bitemporal_todo
{
    public class Todo
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public bool Completed { get; set; }
    }

    public class TodoHistory
    {
        public int RowId { get; set; }
        public string Title { get; set; }
        public bool Completed { get; set; }
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
    }

    public class DeletedTodo
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public bool Completed { get; set; }
        public DateTime? Deleted { get; set; }
    }

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
                CREATE SEQUENCE IF NOT EXISTS ""seq_todo_id"";
                CREATE TABLE IF NOT EXISTS ""todo"" (
                    ""rowid""         SERIAL  PRIMARY KEY,
                    ""id""            INTEGER NOT NULL,
                    ""title""         TEXT    NOT NULL,
                    ""completed""     BOOL    NOT NULL,
                    ""valid""         TSRANGE NOT NULL,
                    ""transaction""   TSRANGE NOT NULL
                );
            ");
        }

        public async Task DropDatabase()
        {
            await this.Connection.ExecuteAsync(@"
                DROP SEQUENCE IF EXISTS ""seq_todo_id"";
                DROP TABLE IF EXISTS ""todo"";
            ");
        }

        public async Task<IEnumerable<Todo>> FindAllTodos()
        {
            return await this.Connection.QueryAsync<Todo>(@"
                SELECT
                    t.""id"",
                    t.""title"",
                    t.""completed""
                FROM
                    ""todo"" AS t
                WHERE
                    NOW()::timestamp <@ t.""valid"" AND
                    NOW()::timestamp <@ t.""transaction""
                ORDER BY
                    t.""id""
                ;
            ");
        }

        public async Task<int> CreateTodo(string title, bool completed)
        {
            return await this.Connection.ExecuteScalarAsync<int>(@"
                INSERT INTO ""todo""
                    (""id"", ""title"", ""completed"", ""valid"", ""transaction"")
                VALUES
                    (NEXTVAL('seq_todo_id'), @title, @completed, TSRANGE(NOW()::timestamp, NULL), TSRANGE(NOW()::timestamp, NULL))
                ;

                SELECT CURRVAL('seq_todo_id');
            ", new { title, completed });
        }

        public async Task UpdateTodo(int id, string title, bool completed)
        {
            await this.Connection.ExecuteAsync(@"
                BEGIN;

                UPDATE ""todo""
                SET
                    ""valid"" = TSRANGE(LOWER(""valid""), NOW()::timestamp)
                WHERE
                    ""id"" = @id AND
                    NOW()::timestamp <@ ""valid"" AND
                    NOW()::timestamp <@ ""transaction""
                ;

                INSERT INTO ""todo""
                    (""id"", ""title"", ""completed"", ""valid"", ""transaction"")
                VALUES
                    (@id, @title, @completed, TSRANGE(NOW()::timestamp, NULL), TSRANGE(NOW()::timestamp, NULL))
                ;

                COMMIT;
            ", new { id, title, completed });
        }

        public async Task DeleteTodo(int id)
        {
            await this.Connection.ExecuteAsync(@"
                UPDATE ""todo""
                SET
                    ""valid"" = TSRANGE(LOWER(""valid""), NOW()::timestamp)
                WHERE
                    ""id"" = @id AND
                    NOW()::timestamp <@ ""valid"" AND
                    NOW()::timestamp <@ ""transaction""
                ;
            ", new { id });
        }

        public async Task<IEnumerable<TodoHistory>> FindTodoHistory(int id)
        {
            return await this.Connection.QueryAsync<TodoHistory>(@"
                SELECT
                    ""rowid"",
                    ""title"",
                    ""completed"",
                    LOWER(""valid"") AS ""from"",
                    UPPER(""valid"") AS ""to""
                FROM
                    ""todo""
                WHERE
                    ""id"" = @id AND
                    NOW()::timestamp <@ ""transaction""
                ORDER BY
                    ""rowid""
            ", new { id });
        }

        public async Task<IEnumerable<DeletedTodo>> FindDeletedTodos()
        {
            return await this.Connection.QueryAsync<DeletedTodo>(@"
                SELECT
                    t2.""id"",
                    t2.""title"",
                    t2.""completed"",
                    UPPER(t2.""valid"") AS ""deleted""
                FROM
                    (
                        SELECT
                            MAX(t1.""rowid"") AS ""rowid""
                        FROM
                            ""todo"" AS t1
                        WHERE
                            NOW()::timestamp <@ t1.""transaction""
                        GROUP BY
                            t1.""id""
                    ) AS r
                JOIN
                    ""todo"" AS t2
                ON
                    r.""rowid"" = t2.""rowid""
                ORDER BY
                    t2.""id""
            ");
        }
    }
}
