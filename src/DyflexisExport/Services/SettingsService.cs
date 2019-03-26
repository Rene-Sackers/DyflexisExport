using System;
using System.IO;
using DyflexisExport.Models;
using Newtonsoft.Json;

namespace DyflexisExport.Services
{
	public static class SettingsService
	{
		private const string SettingsFileName = "settings.json";

		private static readonly Lazy<Settings> SettingsLazy = new Lazy<Settings>(GetSettings);

		public static Settings Settings => SettingsLazy.Value;

		private static Settings GetSettings()
		{
			var json = File.ReadAllText(SettingsFileName);
			var settings = JsonConvert.DeserializeObject<Settings>(json);

			if (!settings.Url.EndsWith("/"))
				settings.Url += "/";

			return settings;
		}

		public static void Save()
		{
			var json = JsonConvert.SerializeObject(Settings);
			File.WriteAllText(SettingsFileName, json);
		}
	}
}
