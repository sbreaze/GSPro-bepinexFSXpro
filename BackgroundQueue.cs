using System;
using System.Threading;
using System.Threading.Tasks;

namespace Api
{
	public class BackgroundQueue
	{
		private Task previousTask = Task.FromResult(result: true);

		private object key = new object();

		public Task QueueTask(Action action)
		{
			lock (key)
			{
				previousTask = previousTask.ContinueWith(delegate
				{
					action();
				}, CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
				return previousTask;
			}
		}

		public Task<T> QueueTask<T>(Func<T> work)
		{
			lock (key)
			{
				return (Task<T>)(previousTask = previousTask.ContinueWith((Task t) => work(), CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default));
			}
		}
	}
}
