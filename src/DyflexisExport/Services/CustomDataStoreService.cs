using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Util.Store;
using Newtonsoft.Json;

namespace DyflexisExport.Services
{
	public class CustomDataStoreService : IDataStore
	{
		private readonly Dictionary<string, dynamic> _data = new Dictionary<string, dynamic>();

		public Task StoreAsync<T>(string key, T value)
		{
			Console.WriteLine($"Store: [{key}][{value?.GetType().FullName}]: {JsonConvert.SerializeObject(value)}");

			if (key == "user")
			{
				SettingsService.Settings.GoogleApiTokenResponse = value as TokenResponse;
				SettingsService.Save();

				Console.WriteLine("Stored user token/refresh token");
			}

			_data[key] = value;

			return Task.CompletedTask;
		}

		public Task DeleteAsync<T>(string key)
		{
			if (_data.ContainsKey(key))
				_data.Remove(key);

			return Task.CompletedTask;
		}

		public Task<T> GetAsync<T>(string key)
		{
			object value = null;

			if (key == "user")
				value = SettingsService.Settings.GoogleApiTokenResponse;
			else if (_data.ContainsKey(key))
				value = (T)_data[key];

			Console.WriteLine($"Get: [{key}][{value?.GetType().FullName}]: {JsonConvert.SerializeObject(value)}");

			return Task.FromResult(default(T));
		}

		public Task ClearAsync()
		{
			_data.Clear();

			return Task.CompletedTask;
		}
	}
}