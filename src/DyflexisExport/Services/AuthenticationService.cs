using System;
using System.Threading;
using System.Threading.Tasks;
using DyflexisExport.Providers;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Util.Store;
using NLog;

namespace DyflexisExport.Services
{
	public class AuthenticationService
	{
		private readonly UserCredentialProvider _userCredentialProvider;
		private readonly CalendarServiceProvider _calendarServiceProvider;
		private readonly IDataStore _customDataStoreService;
		private readonly ILogger _logger = LogManager.GetCurrentClassLogger();

		public AuthenticationService(
			UserCredentialProvider userCredentialProvider,
			CalendarServiceProvider calendarServiceProvider,
			IDataStore customDataStoreService)
		{
			_userCredentialProvider = userCredentialProvider;
			_calendarServiceProvider = calendarServiceProvider;
			_customDataStoreService = customDataStoreService;
		}

		private async Task<UserCredential> GetUserCredentials()
		{
			var clientSecrets = new ClientSecrets
			{
				ClientId = SettingsService.Settings.GoogleClientId,
				ClientSecret = SettingsService.Settings.GoogleClientSecret
			};

			// No user token exists yet, open browser to authenticate and return credentials
			if (SettingsService.Settings.GoogleApiTokenResponse == null)
			{
				if (!SettingsService.Settings.IsRunningSetup)
				{
					_logger.Error("No user token found.");
					return null;
				}

				_logger.Warn("No user token found, opening browser to request permission.");

				return await GoogleWebAuthorizationBroker.AuthorizeAsync(
					clientSecrets,
					new[] {"https://www.googleapis.com/auth/calendar"},
					"user",
					CancellationToken.None,
					_customDataStoreService
				);
			}

			// Token exists, return credentials.
			_logger.Info("Using existing user token.");
			var initializer = new GoogleAuthorizationCodeFlow.Initializer
			{
				ClientSecrets = clientSecrets
			};

			return new UserCredential(new GoogleAuthorizationCodeFlow(initializer), "user", SettingsService.Settings.GoogleApiTokenResponse);
		}

		private async Task<bool> CanGetPrimaryCalendar()
		{
			try
			{
				using (var service = _calendarServiceProvider.GetCalendarService())
					return (await service.Calendars.Get("primary").ExecuteAsync())?.Id != null;
			}
			catch (Exception ex)
			{
				_logger.Error(ex, "Could not get default calendar ID.");
				return false;
			}
		}

		public async Task<bool> EnsureAuthenticated()
		{
			_logger.Trace("Ensuring authentication.");

			_userCredentialProvider.SetUserCredential(await GetUserCredentials());

			if (_userCredentialProvider.GetUserCredential() == null)
			{
				_logger.Error("User credentials are null, authentication not successful.");
				return false;
			}

			if (!await CanGetPrimaryCalendar())
			{
				_logger.Warn("Failed to get primary calendar ID first time, re-authenticating.");
				SettingsService.Settings.GoogleApiTokenResponse = null;
				_userCredentialProvider.SetUserCredential(await GetUserCredentials());
			}
			else
			{
				_logger.Info("Authenticated successfully.");
				return true;
			}

			if (await CanGetPrimaryCalendar())
			{
				_logger.Info("Authentication successful on second attempt.");
				return true;
			}

			_logger.Error("Could not get primary calender a second time, authentication unsuccessful.");
			return false;
		}
	}
}
