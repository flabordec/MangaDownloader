using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using Microsoft.Practices.Prism.Commands;
using Microsoft.Practices.Prism.Mvvm;

namespace MangaDownloader.Data
{
	public class Manga : BindableBase
	{
		[Key]
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

		private ObservableCollection<Chapter> mChapters;
		public virtual ObservableCollection<Chapter> Chapters
		{
			get { return mChapters; }
			set { SetProperty(ref mChapters, value); }
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
				if (this.Chapters.All((chapter) => chapter.State == DownloadState.Complete))
					return DownloadState.Complete;
				else if (this.Chapters.Any((chapter) => chapter.State == DownloadState.Updated))
					return DownloadState.Updated;
				else
					return DownloadState.None;
			}
		}

		public Manga()
			: this(null, null) { }

		public Manga(string title, string address)
		{
			this.Title = title;
			this.Address = address;
			this.Chapters = new ObservableCollection<Chapter>();
			this.Chapters.CollectionChanged += Chapters_CollectionChanged;
			
			this.DownloadCommand = new DelegateCommand(
				this.OnDownload,
				this.CanDownload
				);
		}

		void Chapters_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
		{
			foreach(Chapter chapter in e.NewItems)
				chapter.ChapterDownloaded += chapter_ChapterDownloaded;

			OnPropertyChanged(() => this.State);
			OnPropertyChanged(() => this.ImagePath);
		}

		void chapter_ChapterDownloaded(object sender, ChapterDownloadedEventArgs e)
		{
			OnPropertyChanged(() => this.State);
			OnPropertyChanged(() => this.ImagePath);
		}

		private void OnDownload()
		{
			var pagesToDownload = from chapter in this.Chapters
								  from page in chapter.Pages
								  where page.CanDownload()
								  select page;

			DownloaderSingleton.Instance.AddRange(pagesToDownload.ToArray());
		}

		public bool CanDownload()
		{
			var pagesToDownload = from chapter in this.Chapters
								  from page in chapter.Pages
								  where page.CanDownload()
								  select page;

			return pagesToDownload.Any();
		}
	}
}
