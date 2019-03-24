using System;
using System.IO;
using DyflexisExport.Models;
using Newtonsoft.Json;

namespace DyflexisExport.Services
{
	public static class SettingsService
	{
		private const string SettingsFileName = "settings.json";

		public static Settings Settings => SettingsLazy.Value;

		public static readonly Lazy<Settings> SettingsLazy = new Lazy<Settings>(GetSettings);

		private static Settings GetSettings()
		{
			var json = File.ReadAllText(SettingsFileName);
			return JsonConvert.DeserializeObject<Settings>(json);
		}
	}
}
