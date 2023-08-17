﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GAScheduling
{
    using Buckets = List<List<Chromosome>>;                       //创建一个列表，把一个个单元格看成一个个桶，假设奏议到周五上课，每天排课8个，也就是有40个桶
                                                                  //桶里装的是这个时间段内所有可能的安排
    public interface ICriterion                                   //构造一个类，存储适应度的值和起最大值
    {
        float Evaluate();

        float Max();
    }
    //
    public class BucketBasedCheck
    {
        protected Buckets mBuckets;

        public BucketBasedCheck(Buckets buckets)
        {
            mBuckets = buckets;
        }

        public float BucketCheck(Func<List<Chromosome>, int> check)
        {
            int violations = 0;
            var datasets = Datasets.Get();

            foreach (var bucket in mBuckets)
            {
                if (bucket.Count == 0)
                    continue;

                violations += check(bucket);
            }

            return violations;
        }
    }

    public class TimeCheck : BucketBasedCheck, ICriterion
    {
        private Individual mIndividual;

        public TimeCheck(Individual individual, Buckets buckets) :
            base(buckets)
        {
            mIndividual = individual;
        }       

        public float Evaluate()
        {
            var datasets = Datasets.Get();

            var violations = BucketCheck(bucket =>
            {
                var marked = new List<Chromosome>();
                var count = 0;

                foreach (var chromo in bucket)
                {
                    if (marked.Contains(chromo))
                        continue;

                    marked.Add(chromo);

                    if (chromo.IsSubClass)
                    {
                        var rawClasses = datasets.SubClasses.Find(x => x.Id == chromo.ClassId).DerivingFrom;
                        foreach (var other in bucket)
                        {
                            if (marked.Contains(other))
                                continue;

                            if ((!other.IsSubClass && rawClasses.Contains(other.ClassId)) ||
                                (other.IsSubClass && other.ClassId == chromo.ClassId))
                            {
                                count += 1;
                                marked.Add(other);
                            }
                        }
                    }
                    else
                    {
                        foreach (var other in bucket)
                        {
                            if (marked.Contains(other))
                                continue;

                            if (!other.IsSubClass && other.ClassId == chromo.ClassId)
                            {
                                count += 1;
                                marked.Add(other);
                            }
                        }
                    }
                }

                return count;
            });
            var maxAllowed = mIndividual.Genome.Count / 6;     
            var score = Math.Max(maxAllowed - violations, 0);                  //违反规则吵过课程最大总数的1/6次，我们将他置为0，违反次数为0，得到最大适应度

            score *= score;                                                 //为了使适应度高的被选中的概率更大，我们乘以平方来扩大差距

            return score;
        }

        public float Max()
        {
            var maxAllowed = mIndividual.Genome.Count / 6;
            return maxAllowed * maxAllowed;
        }
    }                                                            //四种约束条件

    public class RoomCheck : BucketBasedCheck, ICriterion
    {
        private Individual mIndividual;

        public RoomCheck(Individual individual, Buckets buckets) :
            base(buckets)
        {
            mIndividual = individual;
        }

        public float Evaluate()
        {
            var datasets = Datasets.Get();

            var violations = BucketCheck(bucket =>
            {
                var dic = new Dictionary<int, List<Chromosome>>();
                var count = 0;

                foreach (var chromo in bucket)
                {
                    if (!dic.Keys.Contains(chromo.Gene.RoomId))
                        dic.Add(chromo.Gene.RoomId, new List<Chromosome>());
                    dic[chromo.Gene.RoomId].Add(chromo);
                }

                foreach (var room in dic)
                {
                    var list = room.Value;
                    var marked = new List<Chromosome>();
                    
                    var first = list[0];
                    int number = 0;

                    foreach (var other in list)
                    {
                        //if (other == first)
                        //    continue;

                        if (first.CourseId != other.CourseId ||
                            first.Gene.TeacherId != other.Gene.TeacherId)
                            count += 1;
                        else
                            number += first.IsSubClass ?
                                datasets.SubClasses.Find(cls => cls.Id == first.ClassId).NumberOfStudents :
                                datasets.Classes.Find(cls => cls.Id == first.ClassId).NumberOfStudents;
                    }

                    if (number > datasets.Rooms.Find(r => r.Id == room.Key).Capacity)
                        count += 1;
                }
                return count;
            });
            var maxAllowed = mIndividual.Genome.Count / 6;
            var score = Math.Max(maxAllowed - violations, 0);

            score *= score;

            return score;
        }

        public float Max()
        {
            var maxAllowed = mIndividual.Genome.Count / 6;
            return maxAllowed * maxAllowed;
        }
    }

    public class TeacherCheck : ICriterion
    {
        private Individual mIndividual;

        public TeacherCheck(Individual individual)
        {
            mIndividual = individual;
        }

        public float Evaluate()
        {
            var datasets = Datasets.Get();
            var violations = 0;

            foreach (var chromo in mIndividual.Genome)
            {
                var teacher = datasets.Teachers.Find(t => t.Id == chromo.Gene.TeacherId);
                if (!teacher.ProvidedCourse.Contains(chromo.CourseId))
                    violations += 1;
            }
            var error = Convert.ToSingle(violations) / mIndividual.Genome.Count;
            var maxAllowed = mIndividual.Genome.Count / 6;
            var score = maxAllowed * (1 - error);
            score *= score;
            return score;
        }

        public float Max()
        {
            var maxAllowed = mIndividual.Genome.Count / 6;
            return maxAllowed * maxAllowed;
        }
    }

    public class ProgressCheck : ICriterion
    {
        private Individual mIndividual;

        public ProgressCheck(Individual individual)
        {
            mIndividual = individual;
        }

        public float Evaluate()
        {
            var datasets = Datasets.Get();
            int violations = 0;

            foreach (var course in datasets.Courses)
            {
                var progress = new Dictionary<int, int>();

                var list = new List<Chromosome>();
                foreach (var chromo in mIndividual.Genome)
                    if (chromo.CourseId == course.Id)
                        list.Add(chromo);
                list.Sort();

                int time = -1;
                var current = new List<Chromosome>();

                foreach (var chromo in list)
                {
                    if (!progress.Keys.Contains(chromo.ClassId))
                        progress.Add(chromo.ClassId, 1);
                    else
                        progress[chromo.ClassId]++;

                    if (time < chromo.Gene.TimeId)
                    {
                        time = chromo.Gene.TimeId;
                        current.Clear();
                        current.Add(chromo);
                        continue;
                    }
                    else if (time == chromo.Gene.TimeId)
                    {
                        foreach (var other in current)
                        {
                            if (chromo.Gene.RoomId != other.Gene.RoomId ||
                                progress[chromo.ClassId] != progress[other.ClassId])
                                violations += 1;
                        }
                    }
                    else
                    {
                        throw new Exception("Time mismatch.");
                    }
                }
            }

            var maxAllowed = mIndividual.Genome.Count / 6;
            var score = Math.Max(maxAllowed - violations, 0);

            score *= score;

            return score;
        }

        public float Max()
        {
            var maxAllowed = mIndividual.Genome.Count / 6;
            return maxAllowed * maxAllowed;
        }
    }

    class Evaluator
    {
        private List<ICriterion> mCriteria;                         //准则列表，通过填充列表实现对任意数量约束条件进行适应度运算，拓展性和伸缩性强

        public Evaluator(Individual individual)                     //依靠其四种约束条件四种计算其适应度，适应度越高，其遗传给下一代的概率就越大
        {
            var buckets = GenerateBueckets(individual);             
            RegisterCriteria(individual, buckets);
        }

        private Buckets GenerateBueckets(Individual individual)
        {
            var datasets = Datasets.Get();

            var buckets = new Buckets(datasets.TotalPeriods);
            for (int i = 0; i < datasets.TotalPeriods; ++i)
                buckets.Add(new List<Chromosome>());
            foreach (var chromo in individual.Genome)
                buckets[chromo.Gene.TimeId].Add(chromo);

            return buckets;
        }

        private void RegisterCriteria(Individual individual, Buckets buckets)
        {
            mCriteria = new List<ICriterion>();

            mCriteria.Add(new TimeCheck(individual, buckets));
            mCriteria.Add(new RoomCheck(individual, buckets));
            mCriteria.Add(new TeacherCheck(individual));
            mCriteria.Add(new ProgressCheck(individual));
        }

        public float Evaluate()
        {
            float sum = 0.0f;

            foreach (var criterion in mCriteria)
                sum += criterion.Evaluate();

            return sum;
        }

        public float Max()
        {
            float sum = 0.0f;

            foreach (var criterion in mCriteria)
                sum += criterion.Max();

            return sum;
        }
    }
}
