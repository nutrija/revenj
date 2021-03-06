﻿using System;
using System.Collections;
using System.Collections.Generic;
using NGS.DatabasePersistence.Oracle.Converters;
using Oracle.DataAccess.Client;

namespace NGS.DatabasePersistence.Oracle
{
	public interface IOracleConverterRepository
	{
		void RegisterConverter(Type type, IOracleTypeConverter converter, IOracleVarrayConverter varrayConverter);
	}

	public interface IOracleConverterFactory
	{
		//Func<object, IServiceLocator, object> GetInstanceFactory(Type type);
		Func<object, string> GetStringFactory(Type type);
		Func<IEnumerable, string> GetVarrayStringFactory(Type type);
		Func<object, OracleParameter> GetParameterFactory(Type type);
		Func<IEnumerable, OracleParameter> GetVarrayParameterFactory(Type type);
	}

	public class OracleObjectFactory : IOracleConverterRepository, IOracleConverterFactory
	{
		private Dictionary<Type, KeyValuePair<IOracleTypeConverter, IOracleVarrayConverter>> TypeConverters =
			new Dictionary<Type, KeyValuePair<IOracleTypeConverter, IOracleVarrayConverter>>();

		public OracleObjectFactory()
		{
			RegisterConverter(typeof(bool), new BoolArrayConverter(), new BoolArrayConverter());
			RegisterConverter(typeof(decimal), new DecimalArrayConverter(), new DecimalArrayConverter());
			RegisterConverter(typeof(double), new DoubleArrayConverter(), new DoubleArrayConverter());
			RegisterConverter(typeof(float), new FloatArrayConverter(), new FloatArrayConverter());
			RegisterConverter(typeof(Guid), new GuidArrayConverter(), new GuidArrayConverter());
			RegisterConverter(typeof(int), new IntegerArrayConverter(), new IntegerArrayConverter());
			RegisterConverter(typeof(long), new LongArrayConverter(), new LongArrayConverter());
			RegisterConverter(typeof(string), new StringArrayConverter(), new StringArrayConverter());
			RegisterConverter(typeof(DateTime), new TimestampArrayConverter(), new TimestampArrayConverter());
		}

		public void RegisterConverter(Type type, IOracleTypeConverter converter, IOracleVarrayConverter arrayConverter)
		{
			TypeConverters[type] = new KeyValuePair<IOracleTypeConverter, IOracleVarrayConverter>(converter, arrayConverter);
		}

		/*public Func<object, IServiceLocator, object> GetInstanceFactory(Type type)
		{
			IOracleTypeConverter converter;
			if (TypeConverters.TryGetValue(type, out converter))
				return converter.CreateInstance;
			return null;
		}*/

		public Func<object, string> GetStringFactory(Type type)
		{
			KeyValuePair<IOracleTypeConverter, IOracleVarrayConverter> kv;
			if (TypeConverters.TryGetValue(type, out kv))
				return kv.Key.ToString;
			return null;
		}

		public Func<IEnumerable, string> GetVarrayStringFactory(Type type)
		{
			KeyValuePair<IOracleTypeConverter, IOracleVarrayConverter> kv;
			if (TypeConverters.TryGetValue(type, out kv))
				return kv.Value.ToStringVarray;
			return null;
		}

		public Func<object, OracleParameter> GetParameterFactory(Type type)
		{
			KeyValuePair<IOracleTypeConverter, IOracleVarrayConverter> kv;
			if (TypeConverters.TryGetValue(type, out kv))
				return kv.Key.ToParameter;
			return null;
		}

		public Func<IEnumerable, OracleParameter> GetVarrayParameterFactory(Type type)
		{
			KeyValuePair<IOracleTypeConverter, IOracleVarrayConverter> kv;
			if (TypeConverters.TryGetValue(type, out kv))
				return kv.Value.ToParameterVarray;
			return null;
		}
	}
}
