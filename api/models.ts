import {Entity, PrimaryGeneratedColumn, Column, EntityRepository, Connection, SelectQueryBuilder} from "typeorm";
import {ObjectId} from "bson";

export interface Task {
  id: string;
  title: string;
  completed: number|null;
  deleted?: number|null;
}

@Entity()
export class TaskHistory {
  @PrimaryGeneratedColumn()
  id!: number;
  
  @Column()
  taskId!: string;
  
  @Column()
  title!: string;
  
  @Column({type: "double precision", nullable: true})
  completed!: number|null;

  @Column({type: "double precision"})
  validFrom!: number;

  @Column({type: "double precision"})
  validTo!: number;

  @Column({type: "double precision"})
  transactFrom!: number;

  @Column({type: "double precision"})
  transactTo!: number;
}

@EntityRepository()
export class TaskRepository {
  constructor(private conn: Connection) {
  }

  async findTasks({validAt = new Date().valueOf(), transactAt = new Date().valueOf()}: {validAt?: number, transactAt?: number} = {}) {
    const query = this.conn.createQueryBuilder()
      .select("t")
      .from(TaskHistory, "t")
      .orderBy("t.taskId");
    forBusinessTimeAsOf(query, "t", validAt);
    forSystemTimeAsOf(query, "t", transactAt);

    return (await query.getMany()).map(item => {
      return {
        id: item.taskId,
        title: item.title,
        completed: item.completed,
      };
    });
  }

  async findAllTasks({validAt = new Date().valueOf(), transactAt = new Date().valueOf()}: {validAt?: number, transactAt?: number} = {}) {
    const query = this.conn.createQueryBuilder()
      .select("t")
      .addSelect("CASE WHEN :validAt < t.validTo THEN t.validTo ELSE NULL END", "deleted")
      .setParameters({validAt})
      .from(TaskHistory, "t")
      .orderBy("t.taskId");
    forSystemTimeAsOf(query, "t", transactAt);
    query.where(q => {
      const subquery = q.subQuery()
        .select("MAX(t2.validFrom)")
        .from(TaskHistory, "t2")
        .where("t.taskId = t2.taskId");
      forBusinessTimeBetween(subquery, "t2", Number.NEGATIVE_INFINITY, validAt);
      forSystemTimeAsOf(subquery, "t2", transactAt);
      return `t.validFrom = ${subquery.getQuery()}`;
    });

    const {raw, entities} = await query.getRawAndEntities();
    return entities.map((item, i) => {
      return {
        id: item.taskId,
        title: item.title,
        completed: item.completed,
        deleted: raw[i].deleted,
      } as Task;
    });
  }

  async createTask({title, created}: {title: string, created: Date|null}, {validFrom = new Date().valueOf(), validTo = Number.POSITIVE_INFINITY, transactAt = new Date().valueOf()}: {validFrom?: number, validTo?: number, transactAt?: number} = {}) {
    const id = new ObjectId().toHexString();
    await this.conn.manager.save(Object.assign(new TaskHistory(), {
      taskId: id,
      title,
      created,
      validFrom: validFrom,
      validTo: validTo,
      transactFrom: transactAt,
      transactTo: Number.POSITIVE_INFINITY,
    }));
    return id;
  }

  async updateTask(id: string, updator: (task: Task) => Task, {validFrom = new Date().valueOf(), validTo = Number.POSITIVE_INFINITY, transactAt = new Date().valueOf()}: {validFrom?: number, validTo?: number, transactAt?: number} = {}) {
    this.conn.transaction("SERIALIZABLE", async manager => {
      const query = manager.createQueryBuilder()
        .select("t")
        .from(TaskHistory, "t")
        .where("t.taskId = :id", {id})
        .orderBy("t.validFrom");
      forSystemTimeAsOf(query, "t", transactAt);
      forBusinessTimeBetween(query, "t", validFrom, validTo);

      for (const item of await query.getMany()) {
        await manager.createQueryBuilder()
          .update(TaskHistory)
          .whereEntity(item)
          .set({transactTo: transactAt})
          .execute();
        
        if (item.validFrom < validFrom) {
          await manager.createQueryBuilder()
            .insert()
            .into(TaskHistory)
            .values({
              taskId: id,
              title: item.title,
              completed: item.completed,
              validFrom: item.validFrom,
              validTo: validFrom,
              transactFrom: transactAt,
              transactTo: Number.POSITIVE_INFINITY,
            })
            .execute();
        }

        const updated = updator({
          id: item.taskId,
          title: item.title,
          completed: item.completed,
        });
        if (updated) {
          await manager.createQueryBuilder()
            .insert()
            .into(TaskHistory)
            .values({
              taskId: updated.id,
              title: updated.title,
              completed: updated.completed,
              validFrom: Math.max(validFrom, item.validFrom),
              validTo: Math.min(validTo, item.validTo),
              transactFrom: transactAt,
              transactTo: Number.POSITIVE_INFINITY,
            })
            .execute();
        }

        if (validTo < item.validTo) {
          await manager.createQueryBuilder()
            .insert()
            .into(TaskHistory)
            .values({
              taskId: id,
              title: item.title,
              completed: item.completed,
              validFrom: validTo,
              validTo: item.validTo,
              transactFrom: transactAt,
              transactTo: Number.POSITIVE_INFINITY,
            })
        }
      }
    });
  }
}

function forBusinessTimeAsOf(query: SelectQueryBuilder<TaskHistory>, alias: string, validAt: number) {
  const t = alias;
  query.andWhere(`(${t}.validFrom IS NULL OR ${t}.validFrom <= :validAt)`, {validAt});
  query.andWhere(`(${t}.validTo IS NULL OR :validAt < ${t}.validTo)`, {validAt});
  return query;
}

function forBusinessTimeBetween(query: SelectQueryBuilder<TaskHistory>, alias: string, validFrom: number, validTo: number) {
  const t = alias;
  query.andWhere(`(${t}.validTo IS NULL OR :validFrom < ${t}.validTo)`, {validFrom});
  query.andWhere(`(${t}.validFrom IS NULL OR ${t}.validFrom <= :validTo)`, {validTo});
  return query;
}

function forSystemTimeAsOf(query: SelectQueryBuilder<TaskHistory>, alias: string, transactAt: number) {
  const t = alias;
  query.andWhere(`(${t}.transactFrom IS NULL OR ${t}.transactFrom <= :transactAt)`, {transactAt});
  query.andWhere(`(${t}.transactTo IS NULL OR :transactAt < ${t}.transactTo)`, {transactAt});
  return query;
}
