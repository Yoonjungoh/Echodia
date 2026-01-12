using System;
using System.Collections.Generic;
using System.Text;

namespace Server.Game
{
	// 모아둔 Job 실행하는 클래스
	public class JobSerializer
	{
		JobTimer _jobTimer = new JobTimer();
		Queue<IJob> _jobQueue = new Queue<IJob>();
		// 커맨드 패턴
		// 하나의 쓰레드가 끝날 때까지 다른 쓰레드는 아무것도 못하는
		// 상호배타적락이 아니라 queue에 접근 할 때만 lock이 있는 것
		object _lock = new object();
		bool _flush = false;

		// 예약해서 쓸 애들 (코루틴)
		public void PushAfter(Action action, int tickAfter) { PushAfter(new Job(action), tickAfter); }
		public void PushAfter<T1>(Action<T1> action, T1 t1, int tickAfter) { PushAfter(new Job<T1>(action, t1), tickAfter); }
		public void PushAfter<T1, T2>(Action<T1, T2> action, T1 t1, T2 t2, int tickAfter) { PushAfter(new Job<T1, T2>(action, t1, t2), tickAfter); }
		public void PushAfter<T1, T2, T3>(Action<T1, T2, T3> action, T1 t1, T2 t2, T3 t3, int tickAfter) { PushAfter(new Job<T1, T2, T3>(action, t1, t2, t3), tickAfter); }

		public void PushAfter(IJob job, int tickAfter)
		{
			_jobTimer.Push(job, tickAfter);
		}
		public void Push(Action action) { Push(new Job(action)); }
		public void Push<T1>(Action<T1> action, T1 t1) { Push(new Job<T1>(action, t1)); }
		public void Push<T1, T2>(Action<T1, T2> action, T1 t1, T2 t2) { Push(new Job<T1, T2>(action, t1, t2)); }
		public void Push<T1, T2, T3>(Action<T1, T2, T3> action, T1 t1, T2 t2, T3 t3) { Push(new Job<T1, T2, T3>(action, t1, t2, t3)); }
		public void Push<T1, T2, T3, T4>(Action<T1, T2, T3, T4> action, T1 t1, T2 t2, T3 t3, T4 t4) { Push(new Job<T1, T2, T3, T4>(action, t1, t2, t3, t4)); }
		public void Push<T1, T2, T3, T4, T5>(Action<T1, T2, T3, T4, T5> action, T1 t1, T2 t2, T3 t3, T4 t4, T5 t5) { Push(new Job<T1, T2, T3, T4, T5>(action, t1, t2, t3, t4, t5)); }
		public void Push(IJob job)
		{
			lock (_lock)
			{
				_jobQueue.Enqueue(job);
			}
		}

		private HashSet<string> _exceptJobName = new HashSet<string>()
		{
			//"HandleMove",
		};

		public void Flush()
		{
			_jobTimer.Flush();

			while (true)
			{
				IJob job = Pop();
				if (job == null)
					return;
				if (_exceptJobName.Contains(job.GetJobName()) == false)
				{
                    ConsoleLogManager.Instance.Log($"{job.GetJobName()}");
                }
                try
                {
                    job.Execute();
                }
                catch (Exception ex)
                {
                    ConsoleLogManager.Instance.Log($"Job execution failed: {ex}");
                }
            }
		}

		IJob Pop()
		{
			lock (_lock)
			{
				// queue에 job 다 처리하면 빠져나오기
				if (_jobQueue.Count == 0)
				{
					_flush = false;
					return null;
				}
				return _jobQueue.Dequeue();
			}
		}

	}
}
