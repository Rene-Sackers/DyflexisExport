using Google.Apis.Auth.OAuth2;

namespace DyflexisExport.Providers
{
	public class UserCredentialProvider
	{
		private UserCredential _userCredential;

		public void SetUserCredential(UserCredential userCredential) =>
			_userCredential = userCredential;

		public UserCredential GetUserCredential() =>
			_userCredential;
	}
}
