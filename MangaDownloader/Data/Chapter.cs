using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Practices.Prism.Commands;
using Microsoft.Practices.Prism.Mvvm;

namespace MangaDownloader.Data
{
	public class ChapterDownloadedEventArgs : EventArgs
	{
		private readonly Chapter mDownloadedChapter;
		public Chapter DownloadedChapter { get { return mDownloadedChapter; } }

		public ChapterDownloadedEventArgs(Chapter downloadedChapter)
		{
			this.mDownloadedChapter = downloadedChapter;
		}
	}

	public class Chapter : BindableBase
	{
		public event EventHandler<ChapterDownloadedEventArgs> ChapterDownloaded;

		[Key]
		[DatabaseGeneratedAttribute(DatabaseGeneratedOption.Identity)]
		public long Id { get; set; }

		[NotMapped]
		public DelegateCommand DownloadCommand { get; private set; }

		private string mTitle;
		public string Title
		{
			get { return mTitle; }
			private set { SetProperty(ref mTitle, value); }
		}

		private string mAddress;
		public string Address
		{
			get { return mAddress; }
			private set { SetProperty(ref mAddress, value); }
		}

		private ObservableCollection<MangaPage> mPages;
		public virtual ObservableCollection<MangaPage> Pages
		{
			get { return mPages; }
			private set { SetProperty(ref mPages, value); }
		}

		public string ImagePath
		{
			get { return StateToImageConverter.ToImagePath(this.State); }
		}

		[NotMapped]
		public DownloadState State
		{
			get
			{
				if (this.Pages.All((page) => page.Done))
					return DownloadState.Complete;
				else if (this.Pages.Any((page) => page.IsNew))
					return DownloadState.Updated;
				else
					return DownloadState.None;
			}
		}

		private void RaiseChapterDownloaded()
		{
			var handler = ChapterDownloaded;
			if (handler != null)
				handler(this, new ChapterDownloadedEventArgs(this));
		}

		public Chapter()
			: this(null, null) { }

		public Chapter(string title, string address)
		{
			this.Title = title;
			this.Address = address;
			this.Pages = new ObservableCollection<MangaPage>();
			this.Pages.CollectionChanged += Pages_CollectionChanged;

			this.DownloadCommand = new DelegateCommand(
				this.OnDownload,
				this.CanDownload
				);
		}

		void Pages_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
		{
			foreach (MangaPage item in e.NewItems)
				item.DownloadCompleted += item_DownloadCompleted;

			OnPropertyChanged(() => this.State);
			OnPropertyChanged(() => this.ImagePath);
		}

		void item_DownloadCompleted(object sender, System.ComponentModel.AsyncCompletedEventArgs e)
		{
			OnPropertyChanged(() => this.State);
			OnPropertyChanged(() => this.ImagePath);
		}

		private void OnDownload()
		{
			var pagesToDownload = from page in this.Pages
								  where page.CanDownload()
								  select page;

			DownloaderSingleton.Instance.AddRange(pagesToDownload.ToArray());
		}

		public bool CanDownload()
		{
			var pagesToDownload = from page in this.Pages
								  where page.CanDownload()
								  select page;
			
			return pagesToDownload.Any();
		}
	}

}
