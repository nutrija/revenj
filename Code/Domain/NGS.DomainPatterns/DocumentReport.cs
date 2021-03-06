﻿using System;
using System.Configuration;
using System.IO;
using NGS.Templater;
using NGS.Utility;

namespace NGS.DomainPatterns
{
	public abstract class DocumentReport
	{
		private static readonly IDocumentFactory Factory = NGS.Templater.Configuration.Factory;
		private static readonly string DocumentFolder;

		static DocumentReport()
		{
			var path = ConfigurationManager.AppSettings["DocumentsPath"];
			DocumentFolder = path == null ? AppDomain.CurrentDomain.BaseDirectory : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path);
		}

		public abstract string TemplateFile { get; }
		public abstract bool ToPdf { get; }

		protected Stream GenerateDocument(params object[] data)
		{
			var file = Path.Combine(DocumentFolder, TemplateFile);
			if (!File.Exists(file))
				throw new IOException("Can't find template document: " + TemplateFile);
			var ext = Path.GetExtension(TemplateFile);
			var cms = ChunkedMemoryStream.Create();
			using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read))
			{
				fs.CopyTo(cms);
			}
			cms.Position = 0;
			using (var document = Factory.Open(cms, ext))
			{
				if (data != null)
					foreach (var d in data)
						document.Process(d);
			}
			cms.Position = 0;
			return ToPdf ? PdfConverter.Convert(cms, ext, true) : cms;
		}
	}
}
