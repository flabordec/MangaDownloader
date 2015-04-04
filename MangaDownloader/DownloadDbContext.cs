using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MangaDownloader.Data;

namespace MangaDownloader
{
	class DownloadDbContext : DbContext
	{
		public DbSet<Manga> Mangas { get; set; }
		public DbSet<Chapter> Chapters { get; set; }
		public DbSet<MangaPage> MangaPages { get; set; }
	}
}
