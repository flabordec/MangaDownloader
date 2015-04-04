using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MangaDownloader.Data
{
	public class FinishedDownloadEventArgs : EventArgs
	{
		private readonly MangaPage mItem;
		public MangaPage Item { get { return mItem; } }

		public FinishedDownloadEventArgs(MangaPage item)
		{
			this.mItem = item;
		}
	}

	public class QueuedDownloadsEventArgs : EventArgs
	{
		private readonly int mTotal;
		public int Total { get { return mTotal; } }

		public QueuedDownloadsEventArgs(int total)
		{
			this.mTotal = total;
		}
	}

	public class DownloaderSingleton
	{
		public event EventHandler<QueuedDownloadsEventArgs> QueuedDownloads;
		public event EventHandler<FinishedDownloadEventArgs> FinishedDownload;

		private BlockingCollection<MangaPage> Jobs { get; set; }

		public static DownloaderSingleton Instance = new DownloaderSingleton();

		private DownloaderSingleton()
		{
			this.Jobs = new BlockingCollection<MangaPage>();
			
			Task.Run(() => ProcessDownloadQueue());
			Task.Run(() => ProcessDownloadQueue());
			Task.Run(() => ProcessDownloadQueue());
		}

		private void FireQueuedDownload(int total)
		{
			var handler = this.QueuedDownloads;
			if (handler != null)
				handler(this, new QueuedDownloadsEventArgs(total));
		}

		private void FireFinishedDownload(MangaPage item)
		{
			var handler = this.FinishedDownload;
			if (handler != null)
				handler(this, new FinishedDownloadEventArgs(item));
		}

		public void Add(MangaPage item) 
		{
			Debug.Assert(item.CanDownload());
			this.Jobs.Add(item);
			FireQueuedDownload(this.Jobs.Count + 1);
		}

		public void AddRange(IEnumerable<MangaPage> items)
		{
			foreach (MangaPage item in items)
				Add(item);
		}

		private async Task ProcessDownloadQueue()
		{
			while (true)
			{
				MangaPage job = this.Jobs.Take();
				await job.DownloadAsync();
				FireFinishedDownload(job);
			}
		}
	}
}
