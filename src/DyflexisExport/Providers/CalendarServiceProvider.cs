using Google.Apis.Calendar.v3;
using Google.Apis.Services;

namespace DyflexisExport.Providers
{
	public class CalendarServiceProvider
	{
		private readonly UserCredentialProvider _userCredentialProvider;

		public CalendarServiceProvider(UserCredentialProvider userCredentialProvider)
		{
			_userCredentialProvider = userCredentialProvider;
		}

		public CalendarService GetCalendarService()
		{
			return new CalendarService(new BaseClientService.Initializer
			{
				HttpClientInitializer = _userCredentialProvider.GetUserCredential(),
				ApplicationName = "DyflexisExport"
			});
		}
	}
}
