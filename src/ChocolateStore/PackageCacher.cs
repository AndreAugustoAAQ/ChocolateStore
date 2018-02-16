﻿using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Ionic.Zip;
using HtmlAgilityPack;

namespace ChocolateStore
{
	class PackageCacher
	{

		private const string INSTALL_FILE = "tools/chocolateyInstall.ps1";

		public delegate void FileHandler(string fileName);
		public delegate void DownloadFailedHandler(string url, Exception ex);

		public event FileHandler SkippingFile = delegate { };
		public event FileHandler DownloadingFile = delegate { };		
		public event DownloadFailedHandler DownloadFailed = delegate { };

		public void CachePackage(Arguments arguments)
		{
			if (arguments.Url == null)
			{
				arguments.Url = GetPackagePath(arguments.PackageName);
			}

			var packagePath = DownloadFile(arguments.Url, arguments.Directory);

			using (var zip = ZipFile.Read(packagePath))
			{
				var entry = zip.FirstOrDefault(x => string.Equals(x.FileName, INSTALL_FILE, StringComparison.OrdinalIgnoreCase));

				if (entry != null) {
					string content = null;
					var packageName = Path.GetFileNameWithoutExtension(packagePath);

					using (MemoryStream ms = new MemoryStream()) {
						entry.Extract(ms);
                        ms.Position = 0;
                        using (StreamReader reader = new StreamReader(ms, true))
                        {
                            content = reader.ReadToEnd();
                        }
                    }

					content = CacheUrlFiles(Path.Combine(arguments.Directory, packageName), content);
					zip.UpdateEntry(INSTALL_FILE, content);
					zip.Save();

				}

			}

		}

		private string CacheUrlFiles(string folder, string content)
		{

            const string pattern = "(?<=['\"])http[\\S ]*(?=['\"])";

			if (!Directory.Exists(folder)) {
				Directory.CreateDirectory(folder);
			}

			return Regex.Replace(content, pattern, new MatchEvaluator(m => DownloadFile(m.Value, folder)));

		}

		private string GetPackagePath(string packageName)
		{
			var url = "https://chocolatey.org/packages/" + packageName;
			var web = new HtmlWeb();
			var doc = web.Load(url);
			return doc.DocumentNode
				.SelectSingleNode("//a[contains(@title, 'nupkg')]")
				.Attributes["href"].Value;
		}

		private string DownloadFile(string url, string destination)
		{

			try
			{
				var request = WebRequest.Create(url);
				var response = request.GetResponse();
				var fileName = Path.GetFileName(response.ResponseUri.LocalPath);
				var filePath = Path.Combine(destination, fileName);

				if (File.Exists(filePath))
				{
					SkippingFile(fileName);
				}
				else
				{
					DownloadingFile(fileName);
					using (var fs = File.Create(filePath))
					{
						response.GetResponseStream().CopyTo(fs);
					}
				}

				return filePath;
			}
			catch (Exception ex)
			{
				DownloadFailed(url, ex);
				return url;
			}

		}

	}
}