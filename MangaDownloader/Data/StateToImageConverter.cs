using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Data;

namespace MangaDownloader.Data
{
	class StateToImageConverter
	{
		public static string ToImagePath(DownloadState state)
		{
			switch (state)
			{
				case DownloadState.Complete:
					return @"Media\checkbox-icon.png";
				case DownloadState.Updated:
					return @"Media\new-icon.png";
				case DownloadState.None:
					return @"Media\default-icon.png";
				default:
					return null;
			}
		}
	}
}
