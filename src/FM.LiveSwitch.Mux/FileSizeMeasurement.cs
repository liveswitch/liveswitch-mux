using Newtonsoft.Json;
using System;
using System.IO;

namespace FM.LiveSwitch.Mux
{
	public class FileSizeMeasurement
	{
		[JsonProperty]
		public string FileName { get; private set; }

		[JsonProperty]
		public long FileSize { get; private set; }

		[JsonProperty]
		public DateTime LastChange { get; private set; }

		[JsonProperty]
		private int _MinimumOrphanDuration;

		public bool IsOrphan
		{
			get
			{
				Measure();
				return ((DateTime.Now - LastChange).TotalMinutes >= _MinimumOrphanDuration);
			}
		}

		public FileSizeMeasurement()
        {
        }

		public FileSizeMeasurement(string fileName, int minimumOrphanDurationMinutes)
		{
			FileName = fileName;
			FileSize = new FileInfo(FileName).Length;
			LastChange = DateTime.Now;
			_MinimumOrphanDuration = minimumOrphanDurationMinutes;
		}

		private void Measure()
		{
			var fileSize = new FileInfo(FileName).Length;
			if (fileSize != FileSize)
			{
				FileSize = fileSize;
				LastChange = DateTime.Now;
			}
		}
	}
}
