import * as express from "express";
import "reflect-metadata";
import {createConnection} from "typeorm";
import { TaskRepository } from "./models";

const connected = createConnection();
async function getRepository() {
  return new TaskRepository(await connected);
}

const app = express();
app.use(express.json({strict: false}));
app.use(express.urlencoded({extended: true}));

app.get("/tasks", async (req, res) => {
  const repo = await getRepository();
  const tasks = await repo.findTasks();
  res.json(tasks);
});

app.get("/tasks/all", async (req, res) => {
  const repo = await getRepository();
  const tasks = await repo.findAllTasks();
  res.json(tasks);
});

app.post("/tasks", async (req, res, next) => {
  const {title} = req.body;
  if (typeof title !== "string") {
    return next(Object.assign(new Error("missing required parameter `title`"), {
      statusCode: 400,
    }));
  }

  const repo = await getRepository();
  const id = await repo.createTask({title, created: null});

  res.json({id});
});

app.put("/tasks/:id/title", async (req, res, next) => {
  const {id} = req.params;
  if (typeof id !== "string") {
    return next(Object.assign(new Error("resource not found"), {
      statusCode: 404,
    }));
  }
  const {title} = req.body;
  if (typeof title !== "string") {
    return next(Object.assign(new Error("missing required parameter `title`"), {
      statusCode: 400,
    }));
  }

  const repo = await getRepository();
  await repo.updateTask(id, task => {
    return {
      id,
      title,
      completed: task.completed,
    };
  });

  res.sendStatus(204);
});

app.put("/tasks/:id/completed", async (req, res, next) => {
  const {id} = req.params;
  if (typeof id !== "string") {
    return next(Object.assign(new Error("resource not found"), {
      statusCode: 404,
    }));
  }
  const completed = (v => (v !== null && typeof v !== "undefined") ? Number(v) : null)(req.body.completed);
  if (completed && !Number.isFinite(new Date(completed).valueOf())) {
    return next(Object.assign(new Error("invalid parameter value `completed`"), {
      statusCode: 400,
    }));
  }

  const repo = await getRepository();
  await repo.updateTask(id, task => {
    return {
      id,
      title: task.title,
      completed,
    };
  }, {validFrom: completed || undefined});

  res.sendStatus(204);
});

export default {
  path: "/api",
  handler: app,
};
