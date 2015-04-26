using System;
using System.Collections.Generic;
using System.ComponentModel;
using DataAnnotations = System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using HtmlAgilityPack;
using Microsoft.Practices.Prism.Commands;
using System.ComponentModel.DataAnnotations.Schema;

namespace MangaDownloader.Data
{
	public class MangaPage : ErrorHandlingBindableBase
	{
		[DataAnnotations.Key]
		public long Id { get; set; }

		public event AsyncCompletedEventHandler DownloadCompleted;

		[NotMapped]
		public DelegateCommand<string> OpenFileCommand { get; private set; }
		[NotMapped]
		public DelegateCommand<string> OpenInExplorerCommand { get; private set; }
		[NotMapped]
		public DelegateCommand<string> CopyCommand { get; private set; }
		[NotMapped]
		public DelegateCommand DownloadCommand { get; private set; }

		private string mTitle;
		public string Title
		{
			get { return mTitle; }
			set { SetProperty(ref mTitle, value); }
		}

		private string mAddress;
		public string Address
		{
			get { return mAddress; }
			private set { SetProperty(ref mAddress, value); }
		}

		private int mProgress;
		public int Progress
		{
			get { return mProgress; }
			private set
			{
				SetProperty(ref mProgress, value);
				OnPropertyChanged(() => this.Done);
				OnPropertyChanged(() => this.ImagePath);
			}
		}

		private long mBytesTotal;
		public long BytesTotal
		{
			get { return mBytesTotal; }
			private set { SetProperty(ref mBytesTotal, value); }
		}

		private long mBytesDownloaded;
		public long BytesDownloaded
		{
			get { return mBytesDownloaded; }
			private set { SetProperty(ref mBytesDownloaded, value); }
		}

		private string mDestinationPath;
		public string DestinationPath
		{
			get { return mDestinationPath; }
			set
			{
				SetProperty(ref mDestinationPath, value);
				if (File.Exists(mDestinationPath))
					this.Progress = 100;
				else
					this.Progress = 0;
			}
		}

		private bool Running { get; set; }

		public string ImagePath
		{
			get
			{
				DownloadState state = DownloadState.None;
				if (Done)
					state = DownloadState.Complete;
				else if (IsNew)
					state = DownloadState.Updated;
				

				return StateToImageConverter.ToImagePath(state);
			}
		}

		[NotMapped]
		public readonly bool mIsNew;
		public bool IsNew
		{
			get { return mIsNew; }
		}

		[NotMapped]
		public bool Done
		{
			get { return mProgress == 100; }
		}


		public MangaPage()
			: this(null, null, null, false) { }

		public MangaPage(string title, string address, string destinationPath, bool isNew)
		{
			this.Title = title;
			this.Address = address;
			this.DestinationPath = destinationPath;
			this.mIsNew = isNew;

			this.OpenInExplorerCommand = new DelegateCommand<string>(
				this.OnOpenInExplorer,
				this.CanOpenInExplorer);

			this.OpenFileCommand = new DelegateCommand<string>(
				this.OnOpenFile,
				this.CanOpenFile);

			this.CopyCommand = new DelegateCommand<string>(
				this.OnCopy,
				this.CanCopy);

			this.DownloadCommand = new DelegateCommand(
				this.OnDownload,
				this.CanDownload);
		}

		public bool CanDownload()
		{
			return !this.Running && this.Progress < 100;
		}

		private void OnDownload()
		{
			DownloaderSingleton.Instance.Add(this);
		}

		private void OnOpenFile(string path)
		{
			ProcessStartInfo pfi = new ProcessStartInfo(path);
			System.Diagnostics.Process.Start(pfi);
		}

		private bool CanOpenFile(string path)
		{
			if (path == null)
				return true;

			return File.Exists(path);
		}

		private void OnCopy(string uri)
		{
			Clipboard.SetText(uri);
		}

		private bool CanCopy(string uri)
		{
			return true;
		}

		private void OnOpenInExplorer(string path)
		{
			string args = string.Format("/Select, \"{0}\"", path);
			ProcessStartInfo pfi = new ProcessStartInfo("Explorer.exe", args);
			System.Diagnostics.Process.Start(pfi);
		}

		private bool CanOpenInExplorer(string path)
		{
			if (path == null)
				return true;

			return File.Exists(path);
		}

		private async Task DownloadFileAsync(Uri uri, string destinationPath)
		{
			await Task.Run(() =>
			{
				byte[] lnBuffer;
				byte[] lnFile;
				HttpWebRequest httpWebRequest = (HttpWebRequest)HttpWebRequest.Create(uri);
				httpWebRequest.Timeout = 30000;
				httpWebRequest.KeepAlive = true;

				int downloadedBytes = 0;

				using (HttpWebResponse httpWebResponse = (HttpWebResponse)httpWebRequest.GetResponse())
				{
					using (BinaryReader binaryReader = new BinaryReader(httpWebResponse.GetResponseStream()))
					{
						using (MemoryStream memoryStream = new MemoryStream())
						{
							while (true)
							{
								lnBuffer = binaryReader.ReadBytes(1024);

								downloadedBytes += lnBuffer.Length;
								DownloadProgressChanged(downloadedBytes, httpWebResponse.ContentLength);

								if (lnBuffer.Length == 0)
									break;

								memoryStream.Write(lnBuffer, 0, lnBuffer.Length);
							}
							lnFile = new byte[(int)memoryStream.Length];
							memoryStream.Position = 0;
							memoryStream.Read(lnFile, 0, lnFile.Length);
						}
					}
				}
				using (FileStream lxFS = new FileStream(destinationPath, FileMode.Create))
				{
					lxFS.Write(lnFile, 0, lnFile.Length);
				}
				RaiseDownloadFileCompleted(this, new AsyncCompletedEventArgs(null, false, null));
			});
		}

		public async Task DownloadAsync()
		{
			try
			{
				Uri imageUrl = null;
				using (WebClient client = new WebClient())
				{
					string html = await DownloadHelper.RetryFunction(
						() => client.DownloadStringTaskAsync(this.Address),
						(ex) => this.ErrorsContainer.SetErrors(() => this.Progress, new ValidationResult[] { new System.Windows.Controls.ValidationResult(false, ex) })
					);

					HtmlDocument document = new HtmlDocument();
					document.LoadHtml(html);

					HtmlNode image = document.DocumentNode.SelectSingleNode("//div[@id='viewer']/a/img");
					string imageUrlString = image.GetAttributeValue("src", "");
					imageUrl = new Uri(imageUrlString);
				}

				await DownloadHelper.RetryActionAsync<Task>(
					() => DownloadFileAsync(imageUrl, this.DestinationPath),
					(ex) => this.ErrorsContainer.SetErrors(() => this.Progress, new ValidationResult[] { new ValidationResult(false, ex) })
				);

				Debug.Assert(new FileInfo(this.DestinationPath).Length > 0);

				this.ErrorsContainer.ClearErrors("Progress");
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex);
			}
		}

		private void RaiseDownloadFileCompleted(object sender, AsyncCompletedEventArgs e)
		{
			var handler = this.DownloadCompleted;
			if (handler != null)
				handler(sender, e);

			this.Progress = 100;
		}

		private void DownloadProgressChanged(long bytesDownloaded, long bytesTotal)
		{
			this.BytesDownloaded = bytesDownloaded;
			this.BytesTotal = bytesTotal;
			this.Progress = (int)(100 * bytesDownloaded / bytesTotal);
		}
	}
}
