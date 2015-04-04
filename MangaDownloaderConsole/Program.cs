using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HtmlAgilityPack;

namespace MangaDownloaderConsole
{
	class Program
	{
		static void Main(string[] args)
		{
			Task task = DownloadMangaAsync();
			task.Wait();
		}

		static async Task DownloadMangaAsync()
		{
			Uri url = new Uri("http://m.mangatown.com/manga/fairy_tail/");
			using (WebClient client = new WebClient())
			{
				string html = DownloadString(client, url);

				HtmlDocument document = new HtmlDocument();
				document.LoadHtml(html);

				HtmlNode chapterList = document.DocumentNode.SelectSingleNode("//ul[@class='chapter_list']");
				foreach (HtmlNode chapter in chapterList.SelectNodes("li/a"))
				{
					string chapterTitle = chapter.InnerText.Trim();
					Uri chapterUrl = new Uri(chapter.GetAttributeValue("href", ""));

					await DownloadChapterAsync(chapterTitle, chapterUrl);
				}
			}
		}

		static async Task DownloadChapterAsync(string title, Uri url)
		{
			using (WebClient client = new WebClient())
			{
				Console.WriteLine("Downloading chapter {0}", title);

				string destinationDirectory = Path.Combine("Downloads", title);
				Directory.CreateDirectory(destinationDirectory);

				string html = DownloadString(client, url);

				HtmlDocument document = new HtmlDocument();
				document.LoadHtml(html);

				var pagesToDownload = from option in document.DocumentNode.SelectNodes("//div[@class='page_select']/select/option")
									  select new Uri(option.GetAttributeValue("value", ""));

				int index = 0;
				List<Task> downloads = new List<Task>();
				foreach (Uri pageToDownload in pagesToDownload)
				{
					Task task = DownloadPageAsync(destinationDirectory, pageToDownload, title, ++index);
					downloads.Add(task);
				}

				await Task.WhenAll(downloads);
			}
		}

		static async Task DownloadPageAsync(string destinationDirectory, Uri url, string title, int page)
		{
			await Task.Run(() => DownloadPage(destinationDirectory, url, title, page));
		}

		static void DownloadPage(string destinationDirectory, Uri url, string title, int page)
		{
			try
			{
				using (WebClient client = new WebClient())
				{
					Console.WriteLine("Chapter {0}, page {1:000} -- Starting download", title, page);

					string fileName = string.Format("{0:000}.jpg", page);
					string destinationPath = Path.Combine(destinationDirectory, fileName);

					if (File.Exists(destinationPath))
					{
						Console.WriteLine("Chapter {0}, page {1:000} -- Already exists", title, page);
						return;
					}

					string html = DownloadString(client, url);

					HtmlDocument document = new HtmlDocument();
					document.LoadHtml(html);

					HtmlNode image = document.DocumentNode.SelectSingleNode("//div[@id='viewer']/a/img");
					string imageUrlString = image.GetAttributeValue("src", "");
					Uri imageUrl = new Uri(imageUrlString);
					string imageFileName = imageUrl.GetComponents(UriComponents.Path, UriFormat.UriEscaped);
					string extension = Path.GetExtension(imageFileName);

					DownloadFile(client, imageUrl, destinationPath);

					Console.WriteLine("Chapter {0}, page {1:000} -- Finished download", title, page);
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex);
			}
		}

		static string DownloadString(WebClient client, Uri address)
		{
			return RetryFunction(() => client.DownloadString(address));
		}

		static void DownloadFile(WebClient client, Uri address, string destinationPath)
		{
			RetryFunction(() => 
			{
				client.DownloadFile(address, destinationPath);
				return true;
			});
		}

		static T RetryFunction<T>(Func<T> a)
		{
			while (true)
			{
				try
				{
					return a();
				}
				catch (Exception ex)
				{
					Console.WriteLine("Error {0}, retrying", ex.Message);
				}
			}
		}
	}
}
