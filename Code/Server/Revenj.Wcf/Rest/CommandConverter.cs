﻿using System;
using System.Diagnostics.Contracts;
using System.IO;
using System.Net;
using System.Xml.Linq;
using NGS.Extensibility;
using NGS.Serialization;
using NGS.Utility;
using Revenj.Api;
using Revenj.Processing;

namespace Revenj.Wcf
{
	public class CommandConverter : ICommandConverter
	{
		private readonly IRestApplication Application;
		private readonly IObjectFactory ObjectFactory;
		private readonly IProcessingEngine ProcessingEngine;
		private readonly IWireSerialization Serialization;
		private readonly ISerialization<Stream> Protobuf;
		private readonly ISerialization<XElement> Xml;
		private readonly ISerialization<StreamReader> Json;

		public CommandConverter(
			IRestApplication application,
			IObjectFactory objectFactory,
			IProcessingEngine processingEngine,
			IWireSerialization serialization,
			ISerialization<Stream> protobuf,
			ISerialization<XElement> xml,
			ISerialization<StreamReader> json)
		{
			Contract.Requires(application != null);
			Contract.Requires(objectFactory != null);
			Contract.Requires(processingEngine != null);
			Contract.Requires(serialization != null);
			Contract.Requires(protobuf != null);
			Contract.Requires(xml != null);
			Contract.Requires(json != null);

			this.Application = application;
			this.ObjectFactory = objectFactory;
			this.ProcessingEngine = processingEngine;
			this.Serialization = serialization;
			this.Protobuf = protobuf;
			this.Xml = xml;
			this.Json = json;
		}

		public Stream PassThrough<TCommand, TArgument>(TArgument argument, string accept)
		{
			var sessionID = ThreadContext.Request.GetHeader("X-NGS-Session-ID") ?? string.Empty;
			var scope = ObjectFactory.FindScope(sessionID);
			if (!string.IsNullOrEmpty(sessionID) && scope == null)
				return Utility.ReturnError("Unknown session: " + sessionID, HttpStatusCode.BadRequest);
			var engine = scope != null ? scope.Resolve<IProcessingEngine>() : ProcessingEngine;
			return
				RestApplication.ExecuteCommand<object>(
					engine,
					Serialization,
					new ObjectCommandDescription { Data = argument, CommandType = typeof(TCommand) },
					accept);
		}

		public Stream ConvertStream<TCommand, TArgument>(TArgument argument)
		{
			var match = new UriTemplateMatch();
			match.RelativePathSegments.Add(typeof(TCommand).FullName);
			ThreadContext.Request.UriTemplateMatch = match;
			if (argument == null)
				return Application.Get();
			switch (ThreadContext.Request.ContentType)
			{
				case "application/x-protobuf":
					return Application.Post(Protobuf.Serialize(argument));
				case "application/json":
					return Application.Post(Json.Serialize(argument).BaseStream);
			}
			using (var ms = ChunkedMemoryStream.Create())
			{
				var sw = ms.GetWriter();
				sw.Write(Xml.Serialize(argument));
				sw.Flush();
				ms.Position = 0;
				return Application.Post(ms);
			}
		}
	}
}
