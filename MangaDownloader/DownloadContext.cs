using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using HtmlAgilityPack;
using MangaDownloader.Data;
using Microsoft.Practices.Prism.Commands;
using Microsoft.Practices.Prism.Mvvm;
using Microsoft.Practices.Prism.ViewModel;

namespace MangaDownloader
{
	public static class DownloadHelper
	{
		public static void RetryFunction(Action Action, Action<Exception> OnError = null)
		{
			while (true)
			{
				try
				{
					Action();
					break;
				}
				catch (Exception ex)
				{
					if (OnError != null)
						OnError(ex);
				}
			}
		}

		public static T RetryFunction<T>(Func<T> Function, Action<Exception> OnError = null)
		{
			while (true)
			{
				try
				{
					return Function();
				}
				catch (Exception ex)
				{
					if (OnError != null)
						OnError(ex);
				}
			}
		}

		public async static Task RetryActionAsync<T>(Func<Task> Action, Action<Exception> OnError = null)
		{
			while (true)
			{
				try
				{
					await Action();
					break;
				}
				catch (Exception ex)
				{
					if (OnError != null)
						OnError(ex);
				}
			}
		}

		public async static Task<T> RetryFunctionAsync<T>(Func<Task<T>> Function, Action<Exception> OnError = null)
		{
			while (true)
			{
				try
				{
					return await Function();
				}
				catch (Exception ex)
				{
					if (OnError != null)
						OnError(ex);
				}
			}
		}
	}

	public abstract class ErrorHandlingBindableBase : BindableBase, INotifyDataErrorInfo
	{
		public event EventHandler<DataErrorsChangedEventArgs> ErrorsChanged;

		protected ErrorsContainer<ValidationResult> ErrorsContainer { get; set; }
		public bool HasErrors
		{
			get { return this.ErrorsContainer.HasErrors; }
		}

		public IEnumerable GetErrors(string propertyName)
		{
			return this.ErrorsContainer.GetErrors(propertyName);
		}
		protected void RaiseErrorsChanged(string propertyName)
		{
			var handler = this.ErrorsChanged;
			if (handler != null)
				handler(this, new DataErrorsChangedEventArgs(propertyName));
		}

		public ErrorHandlingBindableBase()
		{
			this.ErrorsContainer = new ErrorsContainer<ValidationResult>(
				pn => this.RaiseErrorsChanged(pn));
		}
	}

	class DownloadContext : ErrorHandlingBindableBase
	{
		public DelegateCommand DownloadCommand { get; private set; }
		public DelegateCommand UpdateMangasCommand { get; private set; }

		private string mSourceUrl;
		public string SourceUrl
		{
			get { return mSourceUrl; }
			set { SetProperty(ref mSourceUrl, value); }
		}

		private string mDestinationDirectory;
		public string DestinationDirectory
		{
			get { return mDestinationDirectory; }
			set { SetProperty(ref mDestinationDirectory, value); }
		}

		private ObservableCollection<Manga> mMangas;
		public ObservableCollection<Manga> Mangas
		{
			get { return mMangas; }
			private set { SetProperty(ref mMangas, value); }
		}

		private bool mIsParsingNewContent;
		private bool IsParsingNewContent
		{
			get { return mIsParsingNewContent; }
			set
			{
				this.mIsParsingNewContent = value;
				if (this.DownloadCommand != null)
					this.DownloadCommand.RaiseCanExecuteChanged();
			}
		}

		private int mDownloadedCount;
		public int DownloadedCount
		{
			get { return mDownloadedCount; }
			set { SetProperty(ref mDownloadedCount, value); }
		}

		private int mDownloadsTotal;
		public int DownloadsTotal
		{
			get { return mDownloadsTotal; }
			set { SetProperty(ref mDownloadsTotal, value); }
		}

		public DownloadContext()
		{
			this.Mangas = new ObservableCollection<Manga>();
			

			DownloadedCount = 0;
			DownloadsTotal = 0;
			DownloaderSingleton.Instance.QueuedDownloads += Instance_QueuedDownloads;
			DownloaderSingleton.Instance.FinishedDownload += Instance_FinishedDownload;
			
			string userDir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
			//this.SourceUrl = "http://m.mangatown.com/manga/fairy_tail/";
			this.SourceUrl = "http://m.mangatown.com/manga/trash/";
			this.DestinationDirectory = Path.Combine(userDir, "Downloads");

			this.IsParsingNewContent = false;
			
			this.DownloadCommand = new DelegateCommand(
				this.OnDownload,
				this.CanDownload);

			this.UpdateMangasCommand = new DelegateCommand(
				this.OnUpdateMangas,
				this.CanDownload);
		}

		void Instance_FinishedDownload(object sender, FinishedDownloadEventArgs e)
		{
			this.DownloadedCount++;
		}

		void Instance_QueuedDownloads(object sender, QueuedDownloadsEventArgs e)
		{
			this.DownloadsTotal++;
		}

		private async void OnDownload()
		{
			this.IsParsingNewContent = true;
			try
			{
				await AddMangaImagesDownloadJobs(this.SourceUrl);
			}
			finally
			{
				this.IsParsingNewContent = false;
			}
		}

		private async void OnUpdateMangas()
		{
			this.IsParsingNewContent = true;
			try
			{
				using (DownloadDbContext databaseContext = new DownloadDbContext())
				{
					var mangas = from manga in databaseContext.Mangas.Include("Chapters.Pages")
								 select manga;

					foreach (Manga manga in mangas)
					{
						this.Mangas.Add(manga);
						await AddMangaImagesDownloadJobs(manga.Address);
					}
				}
			}
			finally
			{
				this.IsParsingNewContent = false;
			}
		}

		private bool CanDownload()
		{
			return !this.IsParsingNewContent;
		}

		private async Task AddMangaImagesDownloadJobs(string mangaUrl)
		{
			using (WebClient client = new WebClient())
			{
				using (DownloadDbContext databaseContext = new DownloadDbContext())
				{
					//databaseContext.Database.Log = Console.Write;

					string html = await DownloadHelper.RetryFunction(
						() => client.DownloadStringTaskAsync(mangaUrl),
						(ex) => this.ErrorsContainer.SetErrors(() => this.Mangas, new ValidationResult[] { new ValidationResult(false, ex) })
					);

					HtmlDocument document = new HtmlDocument();
					document.LoadHtml(html);

					string mangaTitle = document.DocumentNode.SelectSingleNode("//h1[@class='title-top']").InnerText.Trim();
					Manga manga = (from m in this.Mangas
								   where m.Title == mangaTitle
								   select m).SingleOrDefault();
					if (manga == null)
					{
						manga = new Manga(mangaTitle, this.SourceUrl);
						
						this.Mangas.Add(manga);
						databaseContext.Mangas.Add(manga);

						await databaseContext.SaveChangesAsync();
					}

					HtmlNode chapterList = document.DocumentNode.SelectSingleNode("//ul[@class='chapter_list']");
					foreach (HtmlNode chapterNode in chapterList.SelectNodes("li/a"))
					{
						string chapterUrl = chapterNode.GetAttributeValue("href", "");
						string pageChapterTitle = chapterNode.InnerText.Trim();
						string indexString = pageChapterTitle.Substring(mangaTitle.Length + 1);
						double index = double.Parse(indexString);
						string chapterTitle = string.Format("Chapter {0:000.#}", index);

						Chapter chapter = (from c in manga.Chapters
										   where c.Title == chapterTitle
										   select c).SingleOrDefault();
						if (chapter == null)
						{
							chapter = new Chapter(chapterTitle, chapterUrl);
							manga.Chapters.Add(chapter);
							databaseContext.Chapters.Add(chapter);

							await databaseContext.SaveChangesAsync();

							await AddChapterImagesDownloadJobs(manga, chapter, databaseContext);
						}
					}

					
				}
			}
		}

		private async Task AddChapterImagesDownloadJobs(Manga manga, Chapter chapter, DownloadDbContext databaseContext)
		{
			using (WebClient client = new WebClient())
			{
				string destinationDirectory = Path.Combine(this.DestinationDirectory, manga.Title, chapter.Title);
				Directory.CreateDirectory(destinationDirectory);

				string html = await DownloadHelper.RetryFunction(
					() => client.DownloadStringTaskAsync(chapter.Address),
					(ex) => this.ErrorsContainer.SetErrors(() => this.Mangas, new ValidationResult[] { new ValidationResult(false, ex) })
				);

				HtmlDocument document = new HtmlDocument();
				document.LoadHtml(html);

				var pagesToDownload = from option in document.DocumentNode.SelectNodes("//div[@class='page_select']/select/option")
									  select option.GetAttributeValue("value", "");

				int index = 0;
				foreach (string pageToDownload in pagesToDownload)
				{
					string pageTitle = string.Format("Page {0:00}", ++index);
					string fileName = string.Format("{0}.jpg", pageTitle);
					string destinationPath = Path.Combine(destinationDirectory, fileName);

					MangaPage page = new MangaPage(pageTitle, pageToDownload, destinationPath, true);
					lock (chapter)
					{
						int i = 0;
						string newName = page.Title;
						for (; i < chapter.Pages.Count; i++)
						{
							string currName = chapter.Pages[i].Title;
							if (newName.CompareTo(currName) < 0)
								break;
						}

						chapter.Pages.Insert(i, page);
						databaseContext.MangaPages.Add(page);
					}
					await databaseContext.SaveChangesAsync();
				}
			}
		}

	}

	
	
}
